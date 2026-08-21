using System.IO.Ports;

namespace NoCodeVision.Hardware;

/// <summary>
/// 通讯服务：根据配置创建并管理一个真实通道（串口或网口），统一收发与日志。
/// 流程引擎 / UI 只调 Connect / Send / Disconnect，不关心底层是串口还是网口。
/// </summary>
public sealed class CommService : IDisposable
{
    private ICommChannel? _channel;
    private CancellationTokenSource? _lifeCts;

    public bool IsOpen => _channel?.IsOpen ?? false;

    public event Action<string>? DataReceived;
    public event Action<string>? Log;

    private void Hook(ICommChannel ch)
    {
        ch.DataReceived += s => DataReceived?.Invoke(s);
        ch.Log += s => Log?.Invoke(s);
    }

    public async Task ConnectAsync(string kind, string port, string baud,
        string dataBits, string parity, string stopBits, string flow,
        string netIp, string netPort)
    {
        await DisconnectAsync();

        if (kind == "网口")
        {
            if (!int.TryParse(netPort, out var p)) p = 5000;
            _channel = new TcpCommChannel(netIp, p);
        }
        else
        {
            var b = int.TryParse(baud, out var bv) ? bv : 115200;
            var db = int.TryParse(dataBits, out var dv) ? dv : 8;
            var pb = parity == "奇校验" ? Parity.Odd : parity == "偶校验" ? Parity.Even : Parity.None;
            var sb = stopBits == "1.5" ? StopBits.OnePointFive : stopBits == "2" ? StopBits.Two : StopBits.One;
            var hs = flow == "RTS/CTS" ? Handshake.RequestToSend
                   : flow == "XON/XOFF" ? Handshake.XOnXOff : Handshake.None;
            _channel = new SerialCommChannel(port, b, db, pb, sb, hs);
        }
        Hook(_channel);
        _lifeCts = new CancellationTokenSource();
        await _channel.OpenAsync(_lifeCts.Token);
    }

    public async Task SendAsync(string text)
    {
        if (_channel == null)
        {
            Log?.Invoke("[错误] 请先连接再发送");
            return;
        }
        await _channel.SendAsync(text);
    }

    public async Task DisconnectAsync()
    {
        _lifeCts?.Cancel();
        _lifeCts?.Dispose();
        _lifeCts = null;
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
            _channel = null;
        }
    }

    public void Dispose() => DisconnectAsync().Wait(1000);
}
