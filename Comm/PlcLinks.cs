using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NoCodeVision.ViewModels;

namespace NoCodeVision.Comm;

#region 三菱 MC（3E 二进制帧，TCP 5007）

public sealed class MelsecMcLink : ICommLink
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private CommConfigItem? _cfg;
    public bool IsOpen { get; private set; }
    public event Action<string>? Log;
    public event Action<string>? DataReceived;

    private static readonly Dictionary<string, byte> Dev = new()
    {
        { "D", 0xA8 }, { "M", 0x90 }, { "X", 0x9C }, { "Y", 0x9D }, { "B", 0xA0 },
        { "W", 0xB4 }, { "L", 0x92 }, { "R", 0xB0 }, { "ZR", 0xB0 }, { "S", 0x98 },
        { "C", 0xC8 }, { "TN", 0xC8 }, { "TS", 0xC9 }, { "TC", 0xC9 }
    };

    public async Task ConnectAsync(CommConfigItem c, CancellationToken ct = default)
    {
        _cfg = c;
        int port = int.TryParse(c.NetPort, out var p) && p > 0 ? p : 5007;
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(IPAddress.Parse(c.NetIp), port, ct);
        _stream = _tcp.GetStream();
        IsOpen = true;
        Log?.Invoke($"[三菱MC] 已连接 {c.NetIp}:{port}");
        _cts = new CancellationTokenSource();
        _ = ReceiveLoop(_cts.Token);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        try
        {
            var buf = new byte[4096];
            while (!ct.IsCancellationRequested && _stream != null)
            {
                int got = await ReadExact(_stream, buf, 0, 10, ct);
                if (got < 10) break; // header(2)+routing(6)+len(2)
                int len = (buf[8] << 8) | buf[9];
                got = await ReadExact(_stream, buf, 10, len, ct);
                if (got < len) break;
                int end = (buf[10] << 8) | buf[11];
                if (end != 0) { Log?.Invoke($"[三菱MC 异常] 结束码 {end:X4}"); continue; }
                var words = new List<ushort>();
                for (int i = 12; i + 1 < 10 + len; i += 2)
                    words.Add((ushort)((buf[i] << 8) | buf[i + 1]));
                DataReceived?.Invoke($"[读取] {string.Join(",", words)}");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log?.Invoke("[三菱MC 接收错误] " + ex.Message); }
    }

    private static async Task<int> ReadExact(Stream s, byte[] buf, int off, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int n = await s.ReadAsync(buf, off + total, count - total, ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    private byte DevCode(string element) => Dev.TryGetValue(element.ToUpperInvariant(), out var b) ? b : (byte)0xA8;

    private async Task SendFrame(List<byte> data)
    {
        if (_stream == null) return;
        var f = new List<byte> { 0x50, 0x00, 0x00, 0xFF, 0x03, 0xFF, 0x00 };
        ushort len = (ushort)data.Count;
        f.Add((byte)(len & 0xFF)); f.Add((byte)(len >> 8));
        f.AddRange(data);
        await _stream.WriteAsync(f.ToArray(), 0, f.Count, default);
        await _stream.FlushAsync();
    }

    public async Task SendAsync(string text)
    {
        if (_cfg == null) return;
        var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        string cmd = parts[0].ToLowerInvariant();
        ParseAddr(parts.Length > 1 ? parts[1] : "", _cfg.Element, out var element, out int addr);
        try
        {
            if (cmd is "r" or "i")
            {
                int count = parts.Length > 2 ? int.Parse(parts[2]) : 1;
                var data = new List<byte> { 0x10, 0x00, 0x04, 0x01, 0x00, 0x00, DevCode(element), 0x00,
                    (byte)(addr & 0xFF), (byte)((addr >> 8) & 0xFF), (byte)((addr >> 16) & 0xFF),
                    (byte)(count & 0xFF), (byte)((count >> 8) & 0xFF) };
                await SendFrame(data);
                Log?.Invoke($"[发送] 读 {element}{addr} 数量{count}");
            }
            else if (cmd == "w")
            {
                var vals = new List<ushort>();
                for (int i = 2; i < parts.Length; i++) vals.Add(ushort.Parse(parts[i]));
                var data = new List<byte> { 0x10, 0x00, 0x14, 0x01, 0x00, 0x00, DevCode(element), 0x00,
                    (byte)(addr & 0xFF), (byte)((addr >> 8) & 0xFF), (byte)((addr >> 16) & 0xFF),
                    (byte)(vals.Count & 0xFF), (byte)((vals.Count >> 8) & 0xFF) };
                foreach (var v in vals) { data.Add((byte)(v >> 8)); data.Add((byte)(v & 0xFF)); }
                await SendFrame(data);
                Log?.Invoke($"[发送] 写 {element}{addr} ={string.Join(",", vals)}");
            }
            else Log?.Invoke("[提示] 命令: r 软元件地址 [数量] | w 软元件地址 值...");
        }
        catch (Exception ex) { Log?.Invoke("[三菱MC 命令错误] " + ex.Message); }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;
        _stream?.Close(); _tcp?.Close(); _stream = null; _tcp = null; IsOpen = false;
        await Task.CompletedTask;
    }

    public void Dispose() { try { DisconnectAsync().GetAwaiter().GetResult(); } catch { } }

    private static void ParseAddr(string s, string defElement, out string element, out int addr)
    {
        if (System.Text.RegularExpressions.Regex.Match(s, @"^([A-Za-z]+)\s*(\d+)$") is { Success: true } m)
        {
            element = m.Groups[1].Value; addr = int.Parse(m.Groups[2].Value);
        }
        else { element = defElement; addr = int.TryParse(s, out var a) ? a : 0; }
    }
}

#endregion

#region 欧姆龙 FINS（UDP 9600）

public sealed class FinsLink : ICommLink
{
    private UdpClient? _client;
    private CancellationTokenSource? _cts;
    private CommConfigItem? _cfg;
    public bool IsOpen { get; private set; }
    public event Action<string>? Log;
    public event Action<string>? DataReceived;

    private static readonly Dictionary<string, byte> FinsArea = new()
    {
        { "D", 0x82 }, { "CIO", 0x80 }, { "W", 0x81 }, { "H", 0x84 }, { "A", 0x83 }, { "E", 0x85 }, { "T", 0x89 }, { "C", 0x8A }
    };

    public async Task ConnectAsync(CommConfigItem c, CancellationToken ct = default)
    {
        _cfg = c;
        int port = int.TryParse(c.NetPort, out var p) && p > 0 ? p : 9600;
        _client = new UdpClient(0);
        IsOpen = true;
        Log?.Invoke($"[欧姆龙FINS] 已就绪 {c.NetIp}:{port} 目标节点{c.FinsDna} 源节点{c.FinsSna}");
        _cts = new CancellationTokenSource();
        _ = ReceiveLoop(_cts.Token);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _client != null)
            {
                var res = await _client.ReceiveAsync(ct);
                var b = res.Buffer;
                int end = (b[16] << 8) | b[17];
                if (end != 0) { Log?.Invoke($"[FINS 异常] 结束码 {end:X4}"); continue; }
                var words = new List<ushort>();
                for (int i = 18; i + 1 < b.Length; i += 2)
                    words.Add((ushort)((b[i] << 8) | b[i + 1]));
                DataReceived?.Invoke($"[读取] {string.Join(",", words)}");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log?.Invoke("[FINS 接收错误] " + ex.Message); }
    }

    private static async Task<int> ReadExact(Stream s, byte[] buf, int off, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int n = await s.ReadAsync(buf, off + total, count - total, ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    private byte Area(string element) => FinsArea.TryGetValue(element.ToUpperInvariant(), out var b) ? b : (byte)0x82;

    private async Task SendFrame(byte mrc, byte src, List<byte> tail)
    {
        if (_client == null || _cfg == null) return;
        var f = new List<byte> { 0x80, 0x00, 0x00, 0x00,
            0x80, 0x00, 0x02, 0x00, byte.Parse(_cfg.FinsDna), 0x00, 0x00, byte.Parse(_cfg.FinsSna), 0x00, 0x00,
            mrc, src };
        f.AddRange(tail);
        var ep = new IPEndPoint(IPAddress.Parse(_cfg.NetIp), int.TryParse(_cfg.NetPort, out var p) && p > 0 ? p : 9600);
        await _client.SendAsync(f.ToArray(), f.Count, ep);
    }

    public async Task SendAsync(string text)
    {
        if (_cfg == null) return;
        var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        string cmd = parts[0].ToLowerInvariant();
        ParseAddr(parts.Length > 1 ? parts[1] : "", _cfg.Element, out var element, out int addr);
        try
        {
            if (cmd is "r" or "i")
            {
                int count = parts.Length > 2 ? int.Parse(parts[2]) : 1;
                var tail = new List<byte> { Area(element), (byte)(addr >> 8), (byte)addr, 0x00, (byte)(count >> 8), (byte)count };
                await SendFrame(0x01, 0x01, tail);
                Log?.Invoke($"[发送] 读 {element}{addr} 数量{count}");
            }
            else if (cmd == "w")
            {
                var vals = new List<ushort>();
                for (int i = 2; i < parts.Length; i++) vals.Add(ushort.Parse(parts[i]));
                var tail = new List<byte> { Area(element), (byte)(addr >> 8), (byte)addr, 0x00, (byte)(vals.Count >> 8), (byte)(vals.Count & 0xFF) };
                foreach (var v in vals) { tail.Add((byte)(v >> 8)); tail.Add((byte)(v & 0xFF)); }
                await SendFrame(0x01, 0x02, tail);
                Log?.Invoke($"[发送] 写 {element}{addr} ={string.Join(",", vals)}");
            }
            else Log?.Invoke("[提示] 命令: r 软元件地址 [数量] | w 软元件地址 值...");
        }
        catch (Exception ex) { Log?.Invoke("[FINS 命令错误] " + ex.Message); }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;
        _client?.Close(); _client = null; IsOpen = false;
        await Task.CompletedTask;
    }

    public void Dispose() { try { DisconnectAsync().GetAwaiter().GetResult(); } catch { } }

    private static void ParseAddr(string s, string defElement, out string element, out int addr)
    {
        if (System.Text.RegularExpressions.Regex.Match(s, @"^([A-Za-z]+)\s*(\d+)$") is { Success: true } m)
        {
            element = m.Groups[1].Value; addr = int.Parse(m.Groups[2].Value);
        }
        else { element = defElement; addr = int.TryParse(s, out var a) ? a : 0; }
    }
}

#endregion

#region 西门子 S7（ISO-TSAP + S7 读写 DB，best-effort，未接硬件验证）

public sealed class S7Link : ICommLink
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private CommConfigItem? _cfg;
    public bool IsOpen { get; private set; }
    public event Action<string>? Log;
    public event Action<string>? DataReceived;

    // 已知连接请求帧（标准 S7 握手）
    private byte[] ConnectReq(int rack, int slot)
    {
        int called = 0x0100 + (rack << 5) + slot;
        return new byte[] {
            0x03,0x00,0x00,0x16, 0x11,0xE0,0x00,0x00,0x00,0x01,0x00,
            0xC1,0x02,0x01,0x00,
            0xC2,0x02,(byte)(called>>8),(byte)called
        };
    }

    public async Task ConnectAsync(CommConfigItem c, CancellationToken ct = default)
    {
        _cfg = c;
        int port = int.TryParse(c.NetPort, out var p) && p > 0 ? p : 102;
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(IPAddress.Parse(c.NetIp), port, ct);
        _stream = _tcp.GetStream();
        await WriteAsync(ConnectReq(int.TryParse(c.Rack, out var r) ? r : 0, int.TryParse(c.Slot, out var s) ? s : 1));
        var ack = new byte[22];
        await ReadExact(_stream, ack, 0, 22, ct);
        IsOpen = true;
        Log?.Invoke($"[西门子S7] 已连接 {c.NetIp}:{port} 机架{c.Rack} 槽{c.Slot}");
        _cts = new CancellationTokenSource();
        _ = ReceiveLoop(_cts.Token);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        try
        {
            var buf = new byte[4096];
            while (!ct.IsCancellationRequested && _stream != null)
            {
                int got = await ReadExact(_stream, buf, 0, 4, ct);
                if (got < 4) break;
                int len = (buf[2] << 8) | buf[3];
                got = await ReadExact(_stream, buf, 4, len, ct);
                // S7 读响应：数据区按常见偏移 21 解析（TPKT(4)+COTP(3)+S7头(10)+参数(4) 之后）
                int off = 21;
                if (off + 1 < 4 + len)
                {
                    var words = new List<ushort>();
                    for (int i = off; i + 1 < 4 + len; i += 2)
                        words.Add((ushort)((buf[i] << 8) | buf[i + 1]));
                    DataReceived?.Invoke($"[读取] {string.Join(",", words)}");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log?.Invoke("[S7 接收错误] " + ex.Message); }
    }

    private static async Task<int> ReadExact(Stream s, byte[] buf, int off, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int n = await s.ReadAsync(buf, off + total, count - total, ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    private Task WriteAsync(byte[] data) => _stream!.WriteAsync(data, 0, data.Length, default);

    private async Task SendJob(bool write, int db, int addr, List<ushort> vals)
    {
        if (_stream == null) return;
        int count = write ? vals.Count : (vals.Count > 0 ? vals.Count : 1);
        var param = new List<byte>();
        byte func = write ? (byte)0x05 : (byte)0x04;
        param.Add(func); param.Add(0x01); // item count
        param.Add(0x12); param.Add(0x0A); param.Add(0x10); // variable spec
        param.Add(0x02); // transport size byte
        int bits = count * 8;
        param.Add((byte)(bits >> 8)); param.Add((byte)bits);
        param.Add((byte)(db >> 8)); param.Add((byte)db);
        param.Add(0x84); // area DB
        int a = addr * 8;
        param.Add((byte)(a >> 16)); param.Add((byte)(a >> 8)); param.Add((byte)a);
        var data = new List<byte>();
        if (write) { data.Add(0x00); data.Add(0x04); foreach (var v in vals) { data.Add((byte)(v >> 8)); data.Add((byte)v); } }

        // S7 头：32 01 0000 0000 paramLen dataLen
        var head = new List<byte> { 0x32, 0x01, 0x00, 0x00, 0x00, 0x00,
            (byte)(param.Count >> 8), (byte)param.Count, (byte)(data.Count >> 8), (byte)data.Count };
        var s7 = new List<byte>(); s7.AddRange(head); s7.AddRange(param); s7.AddRange(data);
        var cotp = new List<byte> { 0x02, (byte)(s7.Count + 1), 0xF0 }; cotp.AddRange(s7);
        var tpkt = new List<byte> { 0x03, 0x00, (byte)((cotp.Count + 4) >> 8), (byte)((cotp.Count + 4) & 0xFF) };
        tpkt.AddRange(cotp);
        await _stream.WriteAsync(tpkt.ToArray(), 0, tpkt.Count, default);
        await _stream.FlushAsync();
    }

    public async Task SendAsync(string text)
    {
        if (_cfg == null) return;
        var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        string cmd = parts[0].ToLowerInvariant();
        int db = int.TryParse(_cfg.Db, out var d) ? d : 1;
        try
        {
            if (cmd is "r" or "i")
            {
                int addr = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                int count = parts.Length > 2 ? int.Parse(parts[2]) : 1;
                var vals = new List<ushort>(); for (int i = 0; i < count; i++) vals.Add(0);
                await SendJob(false, db, addr, vals);
                Log?.Invoke($"[发送] 读 DB{db}.DBW{addr} 数量{count}");
            }
            else if (cmd == "w")
            {
                int addr = int.TryParse(parts[1], out var a) ? a : 0;
                var vals = new List<ushort>();
                for (int i = 2; i < parts.Length; i++) vals.Add(ushort.Parse(parts[i]));
                await SendJob(true, db, addr, vals);
                Log?.Invoke($"[发送] 写 DB{db}.DBW{addr} ={string.Join(",", vals)}");
            }
            else Log?.Invoke("[提示] 命令: r 地址 [数量] | w 地址 值...");
        }
        catch (Exception ex) { Log?.Invoke("[S7 命令错误] " + ex.Message); }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;
        _stream?.Close(); _tcp?.Close(); _stream = null; _tcp = null; IsOpen = false;
        await Task.CompletedTask;
    }

    public void Dispose() { try { DisconnectAsync().GetAwaiter().GetResult(); } catch { } }
}

#endregion
