namespace NoCodeVision.Hardware;

/// <summary>
/// 硬件管理器：单例，统管相机 / 运控 / 通讯三类设备。
/// 真实 SDK 到位后，只需在 Init 中把 _camera / _motion 换成真实实现，其余代码不变。
/// </summary>
public sealed class HardwareManager
{
    private static readonly HardwareManager _instance = new();
    public static HardwareManager Instance => _instance;

    public CommService Comm { get; } = new();
    public ICamera Camera { get; private set; }
    public IMotionController Motion { get; private set; }

    public HardwareManager()
    {
        // 无硬件：默认使用模拟实现，预留真实注入点
        Camera = new SimulatedCamera();
        Motion = new SimulatedMotionController();
    }

    /// <summary>真实相机 SDK 注入点（硬件到位后调用）。</summary>
    public void UseRealCamera(ICamera camera)
    {
        Camera.Dispose();
        Camera = camera;
    }

    /// <summary>真实运控 SDK 注入点（硬件到位后调用）。</summary>
    public void UseRealMotion(IMotionController motion)
    {
        Motion.Dispose();
        Motion = motion;
    }
}
