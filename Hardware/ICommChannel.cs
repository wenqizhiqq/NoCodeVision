namespace NoCodeVision.Hardware;

/// <summary>
/// 通讯通道接口。真实串口 / 网口都实现它，UI 与流程只依赖此抽象。
/// 后续接入 PLC / 视觉控制器等真实设备时，只需新增一个 ICommChannel 实现并注入。
/// </summary>
public interface ICommChannel : IDisposable
{
    bool IsOpen { get; }

    /// <summary>收到数据时触发（UTF-8 文本）。</summary>
    event Action<string>? DataReceived;

    /// <summary>状态变化日志。</summary>
    event Action<string>? Log;

    Task OpenAsync(CancellationToken ct = default);
    Task CloseAsync();

    /// <summary>发送文本（自动按行结束符补 \r\n）。</summary>
    Task SendAsync(string text, CancellationToken ct = default);
}
// 温启志：18719361399  混淆: 温y启y志u：a1x8k7d1a9t3t6b1x3j9z9
