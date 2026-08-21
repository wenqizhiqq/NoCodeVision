using System.IO;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GrayMatch;
using NoCodeVision.Hardware;
using OpenCvSharp;

namespace NoCodeVision.ViewModels;

/// <summary>
/// 操作员机台运行控制器。
/// 在原有 OperatorViewModel（批次/良率统计）基础上，增加真实机台控制状态机：
/// 运行 / 暂停 / 停止 / 急停 / 复位。所有按钮都通过 HardwareManager 真正驱动机台：
///   - 相机：Camera.Start / Stop / GrabOne（真实取图）
///   - PLC/通讯：Comm.SendAsync 发送 MACHINE:RUN / PAUSE / RESUME / STOP / ESTOP / RESET 与 RESULT:OK/NG
///   - 运控：Motion.Connect（上电/回零）
/// 急停会立即切断并锁存故障，必须复位后才能再次运行。
/// </summary>
public class OperatorRunViewModel : OperatorViewModel
{
    #region 状态机

    public enum MachineState
    {
        Idle,      // 就绪（待机）
        Running,   // 运行中
        Paused,    // 已暂停
        Stopping,  // 停止中
        Faulted    // 急停故障（已锁存，需复位）
    }

    private MachineState _state = MachineState.Idle;
    public MachineState State
    {
        get => _state;
        private set
        {
            if (!SetField(ref _state, value)) return;
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(IsStopping));
            OnPropertyChanged(nameof(IsFaulted));
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(CanStop));
            OnPropertyChanged(nameof(CanEStop));
            OnPropertyChanged(nameof(CanReset));
            OnPropertyChanged(nameof(RunButtonText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string StateText => _state switch
    {
        MachineState.Idle => "就绪",
        MachineState.Running => "▶ 运行中",
        MachineState.Paused => "⏸ 已暂停",
        MachineState.Stopping => "■ 停止中",
        MachineState.Faulted => "⛔ 急停故障",
        _ => "未知"
    };

    public bool IsIdle => _state == MachineState.Idle;
    public new bool IsRunning => _state == MachineState.Running;
    public bool IsPaused => _state == MachineState.Paused;
    public bool IsStopping => _state == MachineState.Stopping;
    public bool IsFaulted => _state == MachineState.Faulted;

    // 按钮可用性（真实 HMI 行为）
    public bool CanRun => _state == MachineState.Idle || _state == MachineState.Paused;
    public bool CanPause => _state == MachineState.Running;
    public bool CanStop => _state == MachineState.Running || _state == MachineState.Paused;
    public bool CanEStop => true;                       // 急停在任何状态都可拍
    public bool CanReset => _state == MachineState.Faulted;

    public new string RunButtonText => _state == MachineState.Paused ? "继续" : "运行";

    private string _alarmText = "";
    public string AlarmText
    {
        get => _alarmText;
        private set => SetField(ref _alarmText, value);
    }

    private string _lastResult = "";
    public string LastResult
    {
        get => _lastResult;
        private set => SetField(ref _lastResult, value);
    }

    private string _machineLog = "";
    public string MachineLog
    {
        get => _machineLog;
        private set => SetField(ref _machineLog, value);
    }

    #endregion

    #region 运行参数（可由通讯页/配置注入）

    /// <summary>相机序列号，null 表示使用默认相机。</summary>
    public string? CameraSerial { get; set; }

    /// <summary>PLC/通讯类型，默认串口。</summary>
    public string CommType { get; set; } = "串口";

    /// <summary>PLC/通讯端口。</summary>
    public string CommPort { get; set; } = "COM3";

    /// <summary>波特率。</summary>
    public int CommBaud { get; set; } = 9600;

    /// <summary>每个检测周期之间的间隔（毫秒），用于在没有外部触发时连续运行。</summary>
    public int CycleDelayMs { get; set; } = 300;

    /// <summary>视觉模板路径；设置后运行循环会对每帧做 NCC 模板匹配判定合格/不合格。</summary>
    public string? TemplatePath { get; set; }

    /// <summary>匹配合格阈值（NCC 分数）。</summary>
    public double ScoreThreshold { get; set; } = 0.80;

    #endregion

    #region 检测钩子

    /// <summary>
    /// 单帧检测钩子。默认实现：真实抓取一帧并通过 RotatedTemplateMatcher 判定合格/不合格；
    /// 外部（如流程引擎）可替换为更复杂的检测逻辑。
    /// </summary>
    public static Func<System.Windows.Media.Imaging.BitmapSource?, CancellationToken, Task<(bool ok, string detail)>> InspectionHook
        = async (frame, ct) =>
        {
            if (frame == null) return (false, "无图像");

            var tpl = _sharedTemplate;
            if (string.IsNullOrEmpty(tpl) || !File.Exists(tpl))
                return (true, "未配置模板，放行");

            try
            {
                using var matcher = new RotatedTemplateMatcher();
                using var src = BitmapSourceToMat(frame);
                if (src.Empty()) return (false, "图像为空");
                matcher.SetSource(src);
                using var tplMat = Cv2.ImRead(tpl, ImreadModes.Grayscale);
                matcher.SetTemplate(tplMat);
                var results = matcher.Match(
                    pyramidLevels: 2,
                    angleStart: -180, angleEnd: 180, angleStep: 2,
                    nccThreshold: _sharedScore,
                    maxOverlap: 0.3, topN: 1);
                bool ok = results.Count > 0 && results[0].Score >= _sharedScore;
                return (ok, ok ? $"匹配合格 Score={results[0].Score:F3}" : "匹配失败");
            }
            catch (Exception ex)
            {
                return (true, "检测异常，放行：" + ex.Message);
            }
        };

    /// <summary>BitmapSource → BGR Mat（手写像素拷贝转换，避免依赖不确定的扩展命名空间）。</summary>
    private static Mat BitmapSourceToMat(System.Windows.Media.Imaging.BitmapSource frame)
    {
        var wb = frame as WriteableBitmap ?? new WriteableBitmap(frame);
        int w = wb.PixelWidth, h = wb.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[h * stride];
        wb.CopyPixels(pixels, stride, 0);
        using var src = new Mat(h, w, MatType.CV_8UC4, pixels);
        var bgr = new Mat();
        Cv2.CvtColor(src, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }

    // 供默认钩子读取的共享配置（静态，避免构造整个 VM）
    private static string? _sharedTemplate;
    private static double _sharedScore = 0.80;

    #endregion

    #region 字段

    private readonly SynchronizationContext? _uiCtx = SynchronizationContext.Current;
    private CancellationTokenSource? _cts;
    private readonly ManualResetEventSlim _pauseGate = new(true); // true=放行（运行），false=暂停
    private Task? _loopTask;

    #endregion

    #region 命令

    public ICommand RunCmd { get; }
    public ICommand PauseCmd { get; }
    public new ICommand StopCmd { get; }
    public ICommand EmergencyStopCmd { get; }
    public ICommand ResetCmd { get; }

    #endregion

    public OperatorRunViewModel()
    {
        // 同步共享配置，供默认检测钩子使用
        _sharedTemplate = TemplatePath;
        _sharedScore = ScoreThreshold;

        RunCmd = new RelayCommand(_ =>
        {
            if (_state == MachineState.Paused) Resume();
            else StartRun();
        }, _ => CanRun);

        PauseCmd = new RelayCommand(_ => Pause(), _ => CanPause);

        StopCmd = new RelayCommand(_ => Stop(), _ => CanStop);

        EmergencyStopCmd = new RelayCommand(_ => EStop(), _ => CanEStop);

        ResetCmd = new RelayCommand(_ => Reset(), _ => CanReset);
    }

    #region 状态机动作

    private void StartRun()
    {
        if (_state != MachineState.Idle) return;

        _sharedTemplate = TemplatePath;
        _sharedScore = ScoreThreshold;

        State = MachineState.Running;
        AlarmText = "";
        LastResult = "";
        _pauseGate.Set(); // 放行

        // 真实硬件：上电/回零、开始取图、通知 PLC 启动
        try
        {
            HardwareManager.Instance.Motion.Connect();
        }
        catch (Exception ex) { AppendLog("运控连接失败：" + ex.Message); }

        try
        {
            HardwareManager.Instance.Camera.Start(CameraSerial);
        }
        catch (Exception ex) { AppendLog("相机启动失败：" + ex.Message); }

        _ = EnsureCommAndSendAsync("MACHINE:RUN");

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loopTask = Task.Run(() => ProductionLoop(token), token);
    }

    private void Resume()
    {
        if (_state != MachineState.Paused) return;
        State = MachineState.Running;
        _pauseGate.Set();
        _ = EnsureCommAndSendAsync("MACHINE:RESUME");
    }

    private void Pause()
    {
        if (_state != MachineState.Running) return;
        State = MachineState.Paused;
        _pauseGate.Reset(); // 挂起循环
        _ = EnsureCommAndSendAsync("MACHINE:PAUSE");
    }

    private void Stop()
    {
        if (_state != MachineState.Running && _state != MachineState.Paused) return;
        State = MachineState.Stopping;
        _pauseGate.Set();
        _cts?.Cancel();
        _ = EnsureCommAndSendAsync("MACHINE:STOP");
        try { HardwareManager.Instance.Camera.Stop(); } catch { }
    }

    private void EStop()
    {
        // 急停：立即切断并锁存，任何状态都可触发
        State = MachineState.Faulted;
        _pauseGate.Set();
        AlarmText = "急停已触发，必须复位后才能再次运行";
        _cts?.Cancel();
        _ = EnsureCommAndSendAsync("MACHINE:ESTOP");
        try { HardwareManager.Instance.Camera.Stop(); } catch { }
        AppendLog("⛔ 急停！");
    }

    private void Reset()
    {
        if (_state != MachineState.Faulted) return;
        State = MachineState.Idle;
        AlarmText = "";
        _ = EnsureCommAndSendAsync("MACHINE:RESET");
        try { HardwareManager.Instance.Motion.Connect(); } catch { }
        AppendLog("已复位");
    }

    #endregion

    #region 生产循环（真实取图 + 检测 + 结果下发 PLC）

    private async Task ProductionLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // 暂停时在此阻塞；急停/停止时 _pauseGate 为 Set 且 ct 取消，循环退出
                _pauseGate.Wait(ct);
                if (ct.IsCancellationRequested) break;

                System.Windows.Media.Imaging.BitmapSource? frame = null;
                try { frame = HardwareManager.Instance.Camera.GrabOne(); }
                catch (Exception ex) { AppendLog("取图失败：" + ex.Message); }

                var (ok, detail) = await InspectionHook(frame, ct);
                if (ct.IsCancellationRequested) break;

                // 统计（回到 UI 线程更新）
                _uiCtx?.Post(_ =>
                {
                    Total++;
                    if (ok) Ok++; else Ng++;
                    LastResult = ok ? "OK" : "NG";
                }, null);

                AppendLog($"检测结果：{(ok ? "OK" : "NG")} · {detail}");

                // 真实下发结果到 PLC
                await EnsureCommAndSendAsync(ok ? "RESULT:OK" : "RESULT:NG");

                if (ct.IsCancellationRequested) break;
                try { await Task.Delay(CycleDelayMs, ct); }
                catch (TaskCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppendLog("运行异常：" + ex.Message);
        }
        finally
        {
            // 收尾：停止相机、回到就绪（除非是急停，急停保持 Faulted）
            try { HardwareManager.Instance.Camera.Stop(); } catch { }
            if (_state != MachineState.Faulted)
            {
                _uiCtx?.Post(_ => State = MachineState.Idle, null);
            }
        }
    }

    #endregion

    #region 通讯辅助

    private async Task EnsureCommAndSendAsync(string cmd)
    {
        try
        {
            var comm = HardwareManager.Instance.Comm;
            if (!comm.IsOpen)
            {
                await comm.ConnectAsync(CommType, CommPort, CommBaud.ToString(), "8", "无校验", "1", "无", "", "");
            }
            await comm.SendAsync(cmd);
            AppendLog("[PLC] " + cmd);
        }
        catch (Exception ex)
        {
            AppendLog("通讯发送失败：" + cmd + " · " + ex.Message);
        }
    }

    private void AppendLog(string line)
    {
        _uiCtx?.Post(_ =>
        {
            MachineLog = $"[{DateTime.Now:HH:mm:ss}] {line}\n" + MachineLog;
            // 仅保留最近 200 行
            if (MachineLog.Length > 4000) MachineLog = MachineLog.Substring(0, 4000);
        }, null);
    }

    #endregion
}
