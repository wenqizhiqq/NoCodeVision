using System.Windows.Media.Imaging;

namespace NoCodeVision.Hardware;

/// <summary>
/// 相机抽象。无论真实 SDK（Basler Pylon / 海康 MVS / 大恒 …）还是模拟相机，都实现此接口。
/// 真实相机到位后新增一个实现并在 HardwareManager 中替换即可，UI 与流程无需改动。
/// </summary>
public interface ICamera : IDisposable
{
    bool IsGrabbing { get; }

    /// <summary>每取到一帧触发，传 WPF 可直接显示的位图。</summary>
    event Action<BitmapSource>? FrameReady;

    /// <summary>状态/错误日志。</summary>
    event Action<string>? Log;

    void Start(string? serial = null);
    void Stop();

    /// <summary>单帧抓取（用于软触发）。返回 null 表示无图。</summary>
    BitmapSource? GrabOne();
}
