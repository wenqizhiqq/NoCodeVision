using System.Net.Sockets;
using System.Text;

namespace NoCodeVision.Hardware;

/// <summary>
/// 真实网口（TCP）通道（System.Net.Sockets，.NET 原生，可连真实上位机 / PLC / 控制器）。
/// </summary>
public sealed class TcpCommChannel : ICommChannel
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _recvCts;

    public string Host { get; }
    public int Port { get; }

    public bool IsOpen => _client?.Connected ?? false;
    public event Action<string>? DataReceived;
    public event Action<string>? Log;

    public TcpCommChannel(string host, int port)
    {
        Host = host;
        Port = port;
    }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        if (_client?.Connected == true) return;
        _client = new TcpClient();
        try
        {
            Log?.Invoke($"[连接] 正在连接 {Host}:{Port} …");
            await _client.ConnectAsync(Host, Port, ct);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[错误] 连接 {Host}:{Port} 失败：{ex.Message}");
            throw;
        }
        _stream = _client.GetStream();
        Log?.Invoke($"[连接] 网口已连接 {Host}:{Port}");

        _recvCts = new CancellationTokenSource();
        _ = Task.Run(() => RecvLoop(_recvCts.Token), _recvCts.Token);
    }

    private async Task RecvLoop(CancellationToken ct)
    {
        if (_stream == null) return;
        var buf = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested && _stream.CanRead)
            {
                var n = await _stream.ReadAsync(buf, 0, buf.Length, ct);
                if (n == 0) { Log?.Invoke("[断开] 远端关闭连接"); break; }
                var text = Encoding.UTF8.GetString(buf, 0, n);
                DataReceived?.Invoke(text);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log?.Invoke($"[错误] 网口读取异常：{ex.Message}");
        }
    }

    public async Task SendAsync(string text, CancellationToken ct = default)
    {
        if (_stream == null || !_client?.Connected == true)
        {
            Log?.Invoke("[错误] 通道未打开，无法发送");
            return;
        }
        var payload = text.EndsWith("\r\n") || text.EndsWith("\n") ? text : text + "\r\n";
        var bytes = Encoding.UTF8.GetBytes(payload);
        try
        {
            await _stream.WriteAsync(bytes, 0, bytes.Length, ct);
            Log?.Invoke($"[发送] {text.TrimEnd()}");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[错误] 发送失败：{ex.Message}");
        }
    }

    public async Task CloseAsync()
    {
        _recvCts?.Cancel();
        _recvCts?.Dispose();
        _recvCts = null;
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
        _client = null;
        Log?.Invoke("[断开] 已关闭网口");
        await Task.CompletedTask;
    }

    public void Dispose() => CloseAsync().Wait(500);
}
