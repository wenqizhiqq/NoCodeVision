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

    /// <summary>读到输入点状态。</summary>
    bool ReadIo(string name) => false;

    /// <summary>绝对运动到某位置。</summary>
    void MoveAbsolute(string name, double position, double speed) { }

    /// <summary>相对运动一段距离。</summary>
    void MoveRelative(string name, double delta, double speed) { }

    /// <summary>回机械原点。</summary>
    void Home(string name, double offset = 0) { }

    /// <summary>减速停止。</summary>
    void Stop(string name) { }

    /// <summary>所有轴急停。</summary>
    void EmergencyStopAll() { }

    /// <summary>两轴直线联动。</summary>
    void MoveLinear(string xAxis, string yAxis, double x, double y, double speed) { }

    /// <summary>两轴圆弧联动。</summary>
    void MoveArc(string xAxis, string yAxis, double cx, double cy, double radius, double spanDeg, double speed) { }

    /// <summary>读外部编码器位置。</summary>
    double GetEncoder(string name) => 0;

    /// <summary>设防硬件高速比较器。</summary>
    void ArmCompare(string name, double triggerAt, double spacing) { }
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
// 温启志：18719361399  混淆: 温l启q志i：s1w8t7z1f9z3v6a1l3u9x9
