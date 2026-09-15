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

    /// <summary>多相机字典（key = 相机 ID 如 "CAM-01"）。监控面板按 ID 取相机。</summary>
    public System.Collections.Generic.Dictionary<string, ICamera> Cameras { get; } = new();

    public HardwareManager()
    {
        // 无硬件：默认使用模拟实现，预留真实注入点
        Camera = new SimulatedCamera();
        Motion = new SimulatedMotionController();
        // 默认注册一个模拟相机到 Cameras 字典（向后兼容单相机场景）
        Cameras["default"] = Camera;
    }

    /// <summary>获取或创建指定 ID 的模拟相机（不存在则自动新建 SimulatedCamera）。</summary>
    public ICamera GetOrCreateCamera(string cameraId)
    {
        if (Cameras.TryGetValue(cameraId, out var cam)) return cam;
        // 每个通道独立 SimulatedCamera 实例（不同相位/颜色，视觉可区分）
        var sim = new SimulatedCamera(cameraId);
        Cameras[cameraId] = sim;
        return sim;
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
// 温启志：18719361399  混淆: 温b启u志k：q1f8z7b1o9h3q6b1d3y9c9
