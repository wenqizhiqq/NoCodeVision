using System;
using System.Threading;
using System.Threading.Tasks;
using NoCodeVision.Hardware;
using NoCodeVision.ViewModels;

namespace NoCodeVision.Comm;

/// <summary>
/// 通讯中枢（单例）：通讯页所有连接/收发都经由此处。
/// - 串口 / 网口：委托给 HardwareManager.Instance.Comm（加密服务，真实驱动），转发其事件。
/// - 其它类型（UDP / Modbus-TCP / MQTT / HTTP / WebSocket / PLC 协议）：使用本程序集内的明文 ICommLink 实现。
/// 对外统一暴露 Log / DataReceived / StateChanged，供 CommunicationViewModel 接入调试终端。
/// </summary>
public sealed class CommHub : IDisposable
{
    private static readonly CommHub _instance = new();
    public static CommHub Instance => _instance;

    public event Action<string>? Log;
    public event Action<string>? DataReceived;
    public event Action<bool>? StateChanged;

    private ICommLink? _link;
    private bool _delegatingToSvc;
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    public async Task ConnectAsync(CommConfigItem c)
    {
        await DisconnectAsync();
        if (c.CommType == "串口" || c.CommType == "网口")
        {
            var svc = HardwareManager.Instance.Comm;
            svc.Log += OnSvcLog;
            svc.DataReceived += OnSvcData;
            _delegatingToSvc = true;
            try
            {
                await svc.ConnectAsync(c.CommType, c.Port, c.Baud, c.DataBits, c.Parity, c.StopBits, c.Flow, c.NetIp, c.NetPort);
                _isOpen = svc.IsOpen;
            }
            catch (Exception ex) { _isOpen = false; Log?.Invoke("[错误] " + ex.Message); }
        }
        else
        {
            _link = CreateLink(c.CommType);
            _link.Log += OnLinkLog;
            _link.DataReceived += OnLinkData;
            try
            {
                await _link.ConnectAsync(c);
                _isOpen = _link.IsOpen;
            }
            catch (Exception ex) { _isOpen = false; Log?.Invoke("[错误] " + ex.Message); }
        }
        Log?.Invoke(_isOpen ? $"[连接] {c.CommType} 已连接" : $"[连接] {c.CommType} 连接失败");
        StateChanged?.Invoke(_isOpen);
    }

    public async Task DisconnectAsync()
    {
        if (_delegatingToSvc)
        {
            var svc = HardwareManager.Instance.Comm;
            svc.Log -= OnSvcLog;
            svc.DataReceived -= OnSvcData;
            try { await svc.DisconnectAsync(); } catch { }
            _delegatingToSvc = false;
        }
        if (_link != null)
        {
            _link.Log -= OnLinkLog;
            _link.DataReceived -= OnLinkData;
            try { await _link.DisconnectAsync(); } catch { }
            _link.Dispose();
            _link = null;
        }
        _isOpen = false;
        StateChanged?.Invoke(false);
    }

    public async Task SendAsync(string text)
    {
        if (_delegatingToSvc)
        {
            try { await HardwareManager.Instance.Comm.SendAsync(text); }
            catch (Exception ex) { Log?.Invoke("[错误] " + ex.Message); }
        }
        else if (_link != null)
        {
            try { await _link.SendAsync(text); }
            catch (Exception ex) { Log?.Invoke("[错误] " + ex.Message); }
        }
    }

    private void OnSvcLog(string m) => Log?.Invoke(m);
    private void OnSvcData(string m) => DataReceived?.Invoke(m);
    private void OnLinkLog(string m) => Log?.Invoke(m);
    private void OnLinkData(string m) => DataReceived?.Invoke(m);

    private static ICommLink CreateLink(string type) => type switch
    {
        "UDP" => new UdpLink(),
        "Modbus-TCP" => new ModbusTcpLink(),
        "MQTT" => new MqttLink(),
        "HTTP/REST" => new HttpClientLink(),
        "WebSocket" => new WebSocketLink(),
        "西门子S7" => new S7Link(),
        "三菱MC" => new MelsecMcLink(),
        "欧姆龙FINS" => new FinsLink(),
        _ => throw new NotSupportedException("未支持的通讯类型: " + type)
    };

    public void Dispose()
    {
        try { DisconnectAsync().GetAwaiter().GetResult(); } catch { }
    }
}
