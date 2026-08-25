using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NoCodeVision.ViewModels;

namespace NoCodeVision.Comm;

#region UDP

public sealed class UdpLink : ICommLink
{
    private UdpClient? _client;
    private CancellationTokenSource? _cts;
    private CommConfigItem? _cfg;
    public bool IsOpen { get; private set; }
    public event Action<string>? Log;
    public event Action<string>? DataReceived;

    public async Task ConnectAsync(CommConfigItem c, CancellationToken ct = default)
    {
        _cfg = c;
        int localPort = 0; int.TryParse(c.LocalPort, out localPort);
        _client = new UdpClient(localPort == 0 ? new IPEndPoint(IPAddress.Any, 0) : new IPEndPoint(IPAddress.Any, localPort));
        if (c.Broadcast) _client.EnableBroadcast = true;
        IsOpen = true;
        var local = (IPEndPoint)_client.Client.LocalEndPoint;
        Log?.Invoke($"[UDP] 本地端口 {local.Port} 就绪，目标 {c.NetIp}:{c.NetPort}{(c.Broadcast ? "（广播）" : "")}");
        _cts = new CancellationTokenSource();
        _ = ReceiveLoop(_cts.Token);
        await Task.CompletedTask;
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _client != null)
            {
                var res = await _client.ReceiveAsync(ct);
                var s = Encoding.UTF8.GetString(res.Buffer);
                DataReceived?.Invoke($"{res.RemoteEndPoint} : {s.TrimEnd()}");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log?.Invoke("[UDP 接收错误] " + ex.Message); }
    }

    public async Task SendAsync(string text)
    {
        if (_client == null || _cfg == null) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        var ep = new IPEndPoint(IPAddress.Parse(_cfg.NetIp), int.Parse(_cfg.NetPort));
        await _client.SendAsync(bytes, bytes.Length, ep);
        Log?.Invoke($"[发送] -> {_cfg.NetIp}:{_cfg.NetPort} : {text}");
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose(); _cts = null;
        _client?.Close(); _client = null;
        IsOpen = false;
        await Task.CompletedTask;
    }

    public void Dispose() { try { DisconnectAsync().GetAwaiter().GetResult(); } catch { } }
}

#endregion

#region Modbus-TCP

public sealed class ModbusTcpLink : ICommLink
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private CommConfigItem? _cfg;
    private ushort _tid;
    public bool IsOpen { get; private set; }
    public event Action<string>? Log;
    public event Action<string>? DataReceived;

    public async Task ConnectAsync(CommConfigItem c, CancellationToken ct = default)
    {
        _cfg = c;
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(IPAddress.Parse(c.NetIp), int.Parse(c.NetPort), ct);
        _stream = _tcp.GetStream();
        IsOpen = true;
        Log?.Invoke($"[Modbus-TCP] 已连接 {c.NetIp}:{c.NetPort} 单元ID={c.UnitId}");
        _cts = new CancellationTokenSource();
        _ = ReceiveLoop(_cts.Token);
    }

    private byte UnitId => byte.TryParse(_cfg?.UnitId, out var u) ? (byte)u : (byte)1;

    private async Task ReceiveLoop(CancellationToken ct)
    {
        try
        {
            var buf = new byte[256];
            while (!ct.IsCancellationRequested && _stream != null)
            {
                // 先读 MBAP 头 6 字节（transId2 + proto2 + len2 + unit1 之后续 len 由 len 字段给出）
                int got = await ReadExact(_stream, buf, 0, 6, ct);
                if (got < 6) break;
                int len = (buf[4] << 8) | buf[5];
                got = await ReadExact(_stream, buf, 6, len, ct);
                if (got < len) break;
                int func = buf[7];
                if (func >= 0x80) { Log?.Invoke($"[Modbus 异常] 功能 {buf[7]:X2} 错误码 {buf[8]:X2}"); continue; }
                if (func is 0x03 or 0x04)
                {
                    int bc = buf[8];
                    var vals = new System.Collections.Generic.List<ushort>();
                    for (int i = 0; i + 1 < bc; i += 2)
                        vals.Add((ushort)((buf[9 + i] << 8) | buf[10 + i]));
                    DataReceived?.Invoke($"[读取] 功能 {func:X2} : {string.Join(",", vals)}");
                }
                else
                {
                    DataReceived?.Invoke($"[响应] 功能 {func:X2}");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log?.Invoke("[Modbus 接收错误] " + ex.Message); }
    }

    private static async Task<int> ReadExact(Stream s, byte[] buf, int offset, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int n = await s.ReadAsync(buf, offset + total, count - total, ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    private async Task SendPdu(byte func, byte[] pdu)
    {
        if (_stream == null) return;
        ushort tid = ++_tid;
        byte uid = UnitId;
        int len = 1 + pdu.Length;
        var frame = new byte[7 + pdu.Length];
        frame[0] = (byte)(tid >> 8); frame[1] = (byte)(tid & 0xFF);
        frame[2] = 0; frame[3] = 0;
        frame[4] = (byte)(len >> 8); frame[5] = (byte)(len & 0xFF);
        frame[6] = uid;
        Array.Copy(pdu, 0, frame, 7, pdu.Length);
        await _stream.WriteAsync(frame, 0, frame.Length);
        await _stream.FlushAsync();
    }

    public async Task SendAsync(string text)
    {
        if (_cfg == null) return;
        var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        string cmd = parts[0].ToLowerInvariant();
        try
        {
            if (cmd is "r" or "i") // 读保持/输入寄存器  r addr [count]
            {
                ushort addr = ushort.Parse(parts[1]);
                ushort cnt = parts.Length > 2 ? ushort.Parse(parts[2]) : (ushort)1;
                byte func = cmd == "r" ? (byte)0x03 : (byte)0x04;
                await SendPdu(func, new byte[] { func, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(cnt >> 8), (byte)(cnt & 0xFF) });
                Log?.Invoke($"[发送] 读寄存器 地址{addr} 数量{cnt}");
            }
            else if (cmd == "w") // 写：w addr val [val2...]  多个走 FC16，单个走 FC06
            {
                ushort addr = ushort.Parse(parts[1]);
                var vals = new System.Collections.Generic.List<ushort>();
                for (int i = 2; i < parts.Length; i++) vals.Add(ushort.Parse(parts[i]));
                if (vals.Count == 1)
                {
                    ushort v = vals[0];
                    await SendPdu(0x06, new byte[] { 0x06, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(v >> 8), (byte)(v & 0xFF) });
                }
                else
                {
                    var pdu = new System.Collections.Generic.List<byte> { 0x10, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(vals.Count >> 8), (byte)(vals.Count & 0xFF), (byte)(vals.Count * 2) };
                    foreach (var v in vals) { pdu.Add((byte)(v >> 8)); pdu.Add((byte)(v & 0xFF)); }
                    await SendPdu(0x10, pdu.ToArray());
                }
                Log?.Invoke($"[发送] 写寄存器 地址{addr} 值{string.Join(",", vals)}");
            }
            else if (cmd == "c") // 写单个线圈  c addr 0|1
            {
                ushort addr = ushort.Parse(parts[1]);
                ushort on = (ushort)(int.Parse(parts[2]) != 0 ? 0xFF00 : 0x0000);
                await SendPdu(0x05, new byte[] { 0x05, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(on >> 8), (byte)(on & 0xFF) });
                Log?.Invoke($"[发送] 写线圈 地址{addr} ={parts[2]}");
            }
            else
            {
                Log?.Invoke("[提示] 命令: r 地址 [数量] | w 地址 值... | c 地址 0/1 | i 地址 [数量]");
            }
        }
        catch (Exception ex) { Log?.Invoke("[Modbus 命令错误] " + ex.Message); }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;
        _stream?.Close(); _tcp?.Close();
        _stream = null; _tcp = null; IsOpen = false;
        await Task.CompletedTask;
    }

    public void Dispose() { try { DisconnectAsync().GetAwaiter().GetResult(); } catch { } }
}

#endregion

#region HTTP / REST

public sealed class HttpClientLink : ICommLink
{
    private readonly HttpClient _http = new();
    private CommConfigItem? _cfg;
    public bool IsOpen { get; private set; }
    public event Action<string>? Log;
    public event Action<string>? DataReceived;

    public async Task ConnectAsync(CommConfigItem c, CancellationToken ct = default)
    {
        _cfg = c;
        IsOpen = true;
        Log?.Invoke($"[HTTP] 已就绪 方法={c.Method} Url={c.Url}");
    }

    public async Task SendAsync(string text)
    {
        if (_cfg == null) return;
        string url = text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? text : _cfg.Url;
        if (string.IsNullOrWhiteSpace(url)) { Log?.Invoke("[HTTP] 未配置 Url"); return; }
        try
        {
            HttpResponseMessage resp = _cfg.Method.ToUpperInvariant() switch
            {
                "POST" => await _http.PostAsync(url, new StringContent(text, Encoding.UTF8, "application/json"), CancellationToken.None),
                "PUT" => await _http.PutAsync(url, new StringContent(text, Encoding.UTF8, "application/json"), CancellationToken.None),
                "DELETE" => await _http.DeleteAsync(url, CancellationToken.None),
                _ => await _http.GetAsync(url, CancellationToken.None)
            };
            var body = await resp.Content.ReadAsStringAsync();
            if (body.Length > 2000) body = body.Substring(0, 2000) + "…(截断)";
            Log?.Invoke($"[HTTP] {(int)resp.StatusCode} {resp.ReasonPhrase}");
            DataReceived?.Invoke($"[{resp.StatusCode}] {body}");
        }
        catch (Exception ex) { Log?.Invoke("[HTTP 错误] " + ex.Message); }
    }

    public async Task DisconnectAsync() { IsOpen = false; await Task.CompletedTask; }
    public void Dispose() { try { _http.Dispose(); } catch { } }
}

#endregion

#region WebSocket

public sealed class WebSocketLink : ICommLink
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private CommConfigItem? _cfg;
    public bool IsOpen { get; private set; }
    public event Action<string>? Log;
    public event Action<string>? DataReceived;

    public async Task ConnectAsync(CommConfigItem c, CancellationToken ct = default)
    {
        _cfg = c;
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri(c.Url), ct);
        IsOpen = _ws.State == WebSocketState.Open;
        Log?.Invoke($"[WebSocket] 已连接 {c.Url}");
        _cts = new CancellationTokenSource();
        _ = ReceiveLoop(_cts.Token);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buf = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult r;
                do
                {
                    r = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                } while (!r.EndOfMessage);
                if (sb.Length > 0) DataReceived?.Invoke(sb.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log?.Invoke("[WebSocket 接收错误] " + ex.Message); }
    }

    public async Task SendAsync(string text)
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, default);
        Log?.Invoke($"[发送] {text}");
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", default); } catch { }
        }
        _ws?.Dispose(); _ws = null; IsOpen = false;
    }

    public void Dispose() { try { DisconnectAsync().GetAwaiter().GetResult(); } catch { } }
}

#endregion
