using System;
using System.Threading;
using System.Threading.Tasks;
using NoCodeVision.ViewModels;

namespace NoCodeVision.Comm;

/// <summary>
/// 统一通讯链路接口：串口/网口 之外的「实际通讯方式」都实现本接口，
/// 由 CommHub 按 CommType 选择并接入通讯页调试终端。
/// 串口/网口 仍走 HardwareManager.Instance.Comm（加密服务），不实现本接口。
/// </summary>
public interface ICommLink : IDisposable
{
    bool IsOpen { get; }
    Task ConnectAsync(CommConfigItem cfg, CancellationToken ct = default);
    Task DisconnectAsync();
    Task SendAsync(string text);
    event Action<string>? Log;
    event Action<string>? DataReceived;
}
