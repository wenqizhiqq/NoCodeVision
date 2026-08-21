using System.IO.Ports;

namespace NoCodeVision.Hardware;

/// <summary>
/// 真实串口通道（System.IO.Ports，.NET 原生，可连真实 RS232/485 设备）。
/// </summary>
public sealed class SerialCommChannel : ICommChannel
{
    private SerialPort? _port;
    private CancellationTokenSource? _recvCts;

    public string PortName { get; }
    public int BaudRate { get; }
    public int DataBits { get; }
    public Parity Parity { get; }
    public StopBits StopBits { get; }
    public Handshake Handshake { get; }

    public bool IsOpen => _port?.IsOpen ?? false;
    public event Action<string>? DataReceived;
    public event Action<string>? Log;

    public SerialCommChannel(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits, Handshake handshake)
    {
        PortName = portName;
        BaudRate = baudRate;
        DataBits = dataBits;
        Parity = parity;
        StopBits = stopBits;
        Handshake = handshake;
    }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        if (_port?.IsOpen == true) return;
        _port = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
        {
            Handshake = Handshake,
            ReadTimeout = 500,
            WriteTimeout = 500,
            NewLine = "\r\n",
            Encoding = System.Text.Encoding.UTF8
        };
        _port.DataReceived += OnPortDataReceived;
        try
        {
            _port.Open();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[错误] 打开 {PortName} 失败：{ex.Message}");
            throw;
        }
        Log?.Invoke($"[连接] 串口已打开 {PortName} @ {BaudRate}");

        _recvCts = new CancellationTokenSource();
        _ = Task.Run(() => DrainLoop(_recvCts.Token), _recvCts.Token);
        await Task.CompletedTask;
    }

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is not { IsOpen: true }) return;
        try
        {
            var line = _port.ReadExisting();
            if (!string.IsNullOrEmpty(line))
                DataReceived?.Invoke(line);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[错误] 串口读取异常：{ex.Message}");
        }
    }

    // 兜底读取循环，确保没有 DataReceived 事件时也能收（部分虚拟串口不触发事件）
    private async Task DrainLoop(CancellationToken ct)
    {
        var buf = new byte[4096];
        while (!ct.IsCancellationRequested && _port is { IsOpen: true })
        {
            try
            {
                if (_port.BytesToRead > 0)
                {
                    var n = _port.Read(buf, 0, buf.Length);
                    if (n > 0)
                        DataReceived?.Invoke(System.Text.Encoding.UTF8.GetString(buf, 0, n));
                }
            }
            catch { /* 暂时忽略超时 */ }
            await Task.Delay(20, ct);
        }
    }

    public async Task SendAsync(string text, CancellationToken ct = default)
    {
        if (_port is not { IsOpen: true })
        {
            Log?.Invoke("[错误] 通道未打开，无法发送");
            return;
        }
        var payload = text.EndsWith("\r\n") || text.EndsWith("\n") ? text : text + "\r\n";
        try
        {
            _port.Write(payload);
            Log?.Invoke($"[发送] {text.TrimEnd()}");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[错误] 发送失败：{ex.Message}");
        }
        await Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        _recvCts?.Cancel();
        _recvCts?.Dispose();
        _recvCts = null;
        if (_port is not null)
        {
            try { if (_port.IsOpen) _port.Close(); } catch { }
            _port.DataReceived -= OnPortDataReceived;
            _port.Dispose();
            _port = null;
        }
        Log?.Invoke("[断开] 串口已关闭");
        return Task.CompletedTask;
    }

    public void Dispose() => CloseAsync().Wait(500);
}
