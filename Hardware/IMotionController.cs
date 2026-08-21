namespace NoCodeVision.Hardware;

/// <summary>
/// 运控抽象。真实运动控制器（EtherCAT / Modbus / 固高 / 雷赛 / ACS …）实现此接口即可接入。
/// 模拟实现用于无硬件时演示轴位置 / IO 实时反馈。
/// </summary>
public interface IMotionController : IDisposable
{
    bool IsConnected { get; }

    event Action<string>? Log;

    void Connect();
    void Disconnect();

    /// <summary>使能单轴。</summary>
    void EnableAxis(string name, bool enable);

    /// <summary>相对运动（单位 mm / °）。</summary>
    void Jog(string name, double delta);

    /// <summary>设置 IO 输出点状态。</summary>
    void SetIo(string name, bool on);
}

/// <summary>轴实时状态。</summary>
public sealed class AxisState
{
    public string Name { get; set; } = "";
    public double Position { get; set; }
    public string Unit { get; set; } = "mm";
    public bool Enabled { get; set; }
    public string Status => Enabled ? "使能" : "禁用";
}

/// <summary>IO 实时状态。</summary>
public sealed class IoState
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Type { get; set; } = ""; // 输入 / 输出
    public bool On { get; set; }
    public string Status => On ? "ON" : "OFF";
}
