using System.Collections.ObjectModel;

namespace NoCodeVision.Hardware;

/// <summary>
/// 模拟运控：维护轴 / IO 的实时状态，按后台线程持续刷新（模拟真实控制器反馈）。
/// 无硬件时使用。接入真实控制器后新建 IMotionController 实现替换。
/// </summary>
public sealed class SimulatedMotionController : IMotionController
{
    private readonly Thread _thread;
    private volatile bool _run;
    private readonly Dictionary<string, AxisState> _axes = new();
    private readonly Dictionary<string, IoState> _ios = new();
    private readonly object _lock = new();

    public bool IsConnected { get; private set; }
    public event Action<string>? Log;

    public ObservableCollection<AxisState> Axes { get; } = new();
    public ObservableCollection<IoState> Ios { get; } = new();

    public SimulatedMotionController()
    {
        foreach (var a in new[]
        {
            new AxisState { Name = "X 轴", Position = 12.34, Unit = "mm", Enabled = true },
            new AxisState { Name = "Y 轴", Position = -3.10, Unit = "mm", Enabled = true },
            new AxisState { Name = "Z 轴", Position = 0.00, Unit = "mm", Enabled = false },
            new AxisState { Name = "A 轴", Position = 45.0, Unit = "°", Enabled = true },
            new AxisState { Name = "B 轴", Position = 0.00, Unit = "°", Enabled = false },
        })
        {
            _axes[a.Name] = a;
            Axes.Add(a);
        }
        foreach (var io in new[]
        {
            new IoState { Name = "光幕", Address = "0.0", Type = "输入", On = true },
            new IoState { Name = "原点感应", Address = "0.1", Type = "输入", On = false },
            new IoState { Name = "启动按钮", Address = "0.2", Type = "输入", On = true },
            new IoState { Name = "蜂鸣器", Address = "1.0", Type = "输出", On = false },
            new IoState { Name = "绿灯", Address = "1.1", Type = "输出", On = true },
            new IoState { Name = "真空阀", Address = "1.2", Type = "输出", On = false },
        })
        {
            _ios[io.Name] = io;
            Ios.Add(io);
        }

        _thread = new Thread(Loop) { IsBackground = true, Name = "SimMotion" };
    }

    public void Connect()
    {
        if (IsConnected) return;
        IsConnected = true;
        _run = true;
        _thread.Start();
        Log?.Invoke("[运控] 模拟控制器已连接");
    }

    public void Disconnect()
    {
        IsConnected = false;
        _run = false;
        Log?.Invoke("[运控] 模拟控制器已断开");
    }

    public void EnableAxis(string name, bool enable)
    {
        lock (_lock)
            if (_axes.TryGetValue(name, out var a)) a.Enabled = enable;
        Log?.Invoke($"[运控] {name} {(enable ? "使能" : "禁用")}");
    }

    public void Jog(string name, double delta)
    {
        lock (_lock)
            if (_axes.TryGetValue(name, out var a) && a.Enabled) a.Position += delta;
        Log?.Invoke($"[运控] {name} 运动 {delta:+0.00;-0.00}{(_axes.TryGetValue(name, out var ax) ? ax.Unit : "")}");
    }

    public void SetIo(string name, bool on)
    {
        lock (_lock)
            if (_ios.TryGetValue(name, out var io) && io.Type == "输出") io.On = on;
        Log?.Invoke($"[运控] {name} = {(on ? "ON" : "OFF")}");
    }

    private void Loop()
    {
        while (_run)
        {
            // 模拟真实控制器：使能轴带轻微漂移，IO 输入受运动影响
            lock (_lock)
            {
                foreach (var ax in _axes.Values)
                    if (ax.Enabled) ax.Position += 0.01 * Math.Sin(DateTime.Now.Ticks / 1e8 + ax.Name.Length);
            }
            Thread.Sleep(120);
        }
    }

    public void Dispose() => Disconnect();
}
