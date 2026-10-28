using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(100) }; // 后台线程跑流程，UI 定时刷新
    private FlowProductResult? _pendingResult; // 后台线程写入、UI 定时器消费的最新单件结果
    private CancellationTokenSource? _cts;
    private readonly ManualResetEventSlim _pauseGate = new(true); // true=放行（运行），false=暂停
    private Task? _loopTask;
    private CancellationTokenSource? _flowCts;
    private bool _flowStartedByOp;
    private bool _flowSubscribed;
    private int _chRot;

    /// <summary>当前已订阅步骤变化事件的流程（切换流程时退订旧流程）。</summary>
    private readonly System.Collections.Generic.List<VisionFlow> _watchedFlows = new();

    /// <summary>会产生图像的视觉步骤类型：图像采集 / 模板匹配 / 几何测量 / 缺陷检测。</summary>

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

        InitCameraMonitor();
        InitProductionStats();

        Channels.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ImageCount));

        _uiTimer.Tick += (_, _) => OnUiTimerTick();
    }

    #region 多通道视觉监控（操作员页右侧，通道数来自项目相机配置）

    /// <summary>所有监控通道（图像数量与图像内容由所选流程的视觉步骤决定）。</summary>
    public ObservableCollection<VisionChannel> Channels { get; } = new();

    /// <summary>图像数量：当前监控通道数，等于所选流程中视觉步骤（采集/匹配/测量/缺陷）的数量；流程变化即变化。</summary>
    public int ImageCount => Channels.Count;

    /// <summary>监控网格行数（根据相机数自动适配）。</summary>
    public int LayoutRows { get => _layoutRows; set => SetField(ref _layoutRows, value); }
    private int _layoutRows = 2;

    /// <summary>监控网格列数（根据相机数自动适配）。</summary>
    public int LayoutCols { get => _layoutCols; set => SetField(ref _layoutCols, value); }
    private int _layoutCols = 2;

    /// <summary>每个画面里图像的宽高比：宽 = 高 × 1.2（画面区域跟着格子宽度走，不再被拉成横条）。</summary>
    public const double CardAspect = 1.2;

    /// <summary>卡片上除图像外的固定高度：标题行 + 三列指标 + 内外边距。</summary>
    private const double CardChrome = 96;

    /// <summary>卡片左右占掉的总宽度：卡片外边距 + 图像区左右边距。</summary>
    private const double CardHMargin = 26;

    /// <summary>卡片宽度（图像 = 宽度 × 0.8333 高）；由监控区尺寸算出来，NaN = 跟随格子拉伸。</summary>
    public double CardWidth { get => _cardWidth; private set => SetField(ref _cardWidth, value); }
    private double _cardWidth = double.NaN;

    /// <summary>按行列算出每格能放多大的画面，并定下卡片宽度。</summary>
    private void ComputeLayout(int rows, int cols)
    {
        LayoutRows = rows;
        LayoutCols = cols;
        if (_viewWidth <= 0 || _viewHeight <= 0) { CardWidth = double.NaN; return; }
        double cellW = _viewWidth / cols;
        double cellH = _viewHeight / rows;
        double byWidth = cellW - CardHMargin;
        double byHeight = (cellH - CardChrome) * CardAspect;
        // 卡片宽度 = 图像宽度 + 图像区左右边距（卡片自身的 7+7 外边距靠居中来留）
        CardWidth = System.Math.Max(90, System.Math.Min(byWidth, byHeight) + 12);
    }

    /// <summary>监控区实际可用尺寸（视图 SizeChanged 时回填），用来算每行每列能放多大。</summary>
    private double _viewWidth, _viewHeight;

    /// <summary>监控区尺寸变了就重排（窗口放大 → 画面跟着变大）。</summary>
    public void OnMonitorViewportChanged(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        if (System.Math.Abs(width - _viewWidth) < 1 && System.Math.Abs(height - _viewHeight) < 1) return;
        _viewWidth = width;
        _viewHeight = height;
        if (IsAutoFit) AutoFitLayout();
    }

    /// <summary>是否自适应布局（true=按相机数量自动最大化卡片；false=使用手动基准密度）。</summary>
    public bool IsAutoFit { get => _isAutoFit; set => SetField(ref _isAutoFit, value); }
    private bool _isAutoFit = true;

    /// <summary>手动布局基准行列（仅 IsAutoFit=false 时生效）。</summary>
    private int _manualRows = 2;
    private int _manualCols = 3;

    public ICommand AutoFitCmd { get; private set; }
    public ICommand Layout12Cmd { get; private set; }
    public ICommand Layout23Cmd { get; private set; }
    public ICommand Layout34Cmd { get; private set; }

    private readonly DispatcherTimer _camTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(100)
    };

    private void InitCameraMonitor()
    {
        AutoFitCmd = new RelayCommand(_ => { IsAutoFit = true; AutoFitLayout(); });
        Layout12Cmd = new RelayCommand(_ => { IsAutoFit = false; _manualRows = 1; _manualCols = 2; ApplyManualLayout(); });
        Layout23Cmd = new RelayCommand(_ => { IsAutoFit = false; _manualRows = 2; _manualCols = 3; ApplyManualLayout(); });
        Layout34Cmd = new RelayCommand(_ => { IsAutoFit = false; _manualRows = 3; _manualCols = 4; ApplyManualLayout(); });


        // 监控通道来自流程：图像数量与图像内容由所选流程的视觉步骤（采集/匹配/测量/缺陷）决定；
        // 切换流程或增删步骤时操作员页自动同步（需求：流程有变化，图像就要变化）
        if (FlowViewModel.Instance != null)
        {
            // 流程数量变了就重新出画面：有几个流程就有几路图
            FlowViewModel.Instance.Flows.CollectionChanged += (_, _) => RebuildChannelsFromFlow();
            RebuildChannelsFromFlow();
        }
        else if (CameraViewModel.Instance != null)
        {
            // 无流程数据时回退到相机列表
            RebuildChannelsFromCameras();
            CameraViewModel.Instance.Cameras.CollectionChanged += (_, _) => RebuildChannelsFromCameras();
        }

        _camTimer.Tick += PollCameras;
        _camTimer.Start();
    }

    /// <summary>根据 CameraViewModel.Instance.Cameras 重建通道列表。</summary>
    private void RebuildChannelsFromCameras()
    {
        var camVm = CameraViewModel.Instance;
        if (camVm == null) return;

        Channels.Clear();
        for (int i = 0; i < camVm.Cameras.Count; i++)
        {
            var cam = camVm.Cameras[i];
            // 从 CameraItem.Name 提取相机 ID（如 "Camera_0 (左视野)" -> "Camera_0"）
            var camId = cam.Name.Contains(' ') ? cam.Name.Split(' ')[0] : cam.Name;
            var displayName = cam.Name;
            Channels.Add(new VisionChannel(displayName, camId));
        }

        // 数量变化时重新排布（自适应或手动均保证容纳）
        if (IsAutoFit) AutoFitLayout();
        else ApplyManualLayout();

    }

    /// <summary>有几个流程就出几路画面：每一路显示那个流程最近一次的结果图。</summary>
    private void RebuildChannelsFromFlow()
    {
        foreach (var f in _watchedFlows) f.PropertyChanged -= OnFlowRenamed;
        _watchedFlows.Clear();

        Channels.Clear();
        var fvm = FlowViewModel.Instance;
        if (fvm != null)
        {
            foreach (var f in fvm.Flows)
            {
                f.PropertyChanged += OnFlowRenamed;
                _watchedFlows.Add(f);
                Channels.Add(new VisionChannel(f.Name, FlowKindText(f)));
            }
        }

        if (IsAutoFit) AutoFitLayout(); else ApplyManualLayout();

        // 若正在运行，新通道直接进入运行态（图像由流程引擎 ProductInspected 推送，不单独启硬件相机）
        if (_state == MachineState.Running) EnsureChannelsRunning();
        OnPropertyChanged(nameof(ImageCount));
    }

    /// <summary>流程改名时只改画面标题，不重建整张表（重建会丢掉已显示的图）。</summary>
    private void OnFlowRenamed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(VisionFlow.Name)) return;
        var fvm = FlowViewModel.Instance;
        if (fvm == null) return;
        for (int i = 0; i < fvm.Flows.Count && i < Channels.Count; i++)
        {
            if (Channels[i].Name != fvm.Flows[i].Name) Channels[i].Name = fvm.Flows[i].Name;
        }
    }

    private static string FlowKindText(VisionFlow f) => f.FlowKind switch
    {
        "Script" => "脚本流程",
        "NodeGraph" => "节点图",
        _ => "逻辑视觉"
    };

    /// <summary>让所有通道进入运行态（仅更新状态，图像由流程引擎推送，不单独创建硬件相机）。</summary>
    private void EnsureChannelsRunning()
    {
        foreach (var c in Channels)
        {
            if (c.IsRunning) continue;
            c.IsRunning = true;
            c.Status = "运行中";
            c.IsConnected = true;
        }
    }

    /// <summary>自适应布局：每一种行列都算一遍，挑「画面画得最大」的那种。</summary>
    private void AutoFitLayout()
    {
        int n = Channels.Count;
        if (n <= 0) { ComputeLayout(1, 1); return; }

        // 还不知道面板多大时，先按接近正方形排
        if (_viewWidth <= 0 || _viewHeight <= 0)
        {
            int c0 = (int)System.Math.Ceiling(System.Math.Sqrt(n));
            ComputeLayout((n + c0 - 1) / c0, c0);
            return;
        }

        int bestRows = 1, bestCols = n;
        double bestImage = 0;
        for (int r = 1; r <= n; r++)
        {
            int c = (n + r - 1) / r;
            double cellW = _viewWidth / c;
            double cellH = _viewHeight / r;
            double imageW = System.Math.Min(cellW - CardHMargin, (cellH - CardChrome) * CardAspect);
            if (imageW <= 0) continue;
            if (imageW > bestImage) { bestImage = imageW; bestRows = r; bestCols = c; }
        }

        ComputeLayout(bestRows, bestCols);
    }

    /// <summary>手动布局：以用户选定的基准行列为准，但保证能容纳所有相机（不足则自动加行）。</summary>
    private void ApplyManualLayout()
    {
        int n = Channels.Count;
        if (n <= 0) { ComputeLayout(_manualRows, _manualCols); return; }
        int rows = _manualRows;
        while (rows * _manualCols < n) rows++;
        ComputeLayout(rows, _manualCols);
    }

    /// <summary>实时数据改由流程引擎 ProductInspected 事件驱动（真实匹配分数/缺陷/图像），此处不再轮询模拟。</summary>
    private void PollCameras(object? sender = null, EventArgs? e = null) { }

    #endregion


    #region 生产统计（UPH / 运行时长 / 产量折线图）

    /// <summary>本次运行开始时间（用于计算 UPH 与运行时长）。</summary>
    private DateTime? _runStart;

    /// <summary>本次运行结束时间（停止后冻结统计，null 表示仍在运行）。</summary>
    private DateTime? _runEnd;

    /// <summary>实时 UPH（Units Per Hour，每小时产量）。</summary>
    public double UPH { get => _uph; private set => SetField(ref _uph, value); }
    private double _uph;

    /// <summary>运行时长（hh:mm:ss），停止后冻结。</summary>
    public string RunDurationText
    {
        get
        {
            if (_runStart == null) return "00:00:00";
            var end = _runEnd ?? DateTime.Now;
            return (end - _runStart.Value).ToString(@"hh\:mm\:ss");
        }
    }

    /// <summary>产量采样序列（每 2 秒记录一次累计产量，用于折线图）。</summary>
    public ObservableCollection<double> ProductionSeries { get; } = new();

    /// <summary>折线图 Polyline 点串（逻辑坐标 300×120，供 Canvas 绑定）。</summary>
    public string ProductionPolylinePoints { get => _prodPoints; private set => SetField(ref _prodPoints, value); }
    private string _prodPoints = "";

    private readonly DispatcherTimer _prodTimer = new()
    {
        Interval = TimeSpan.FromSeconds(2)
    };

    private void InitProductionStats()
    {
        _prodTimer.Tick += (_, _) => SampleProduction();
        _prodTimer.Start();

        // 总产量/良品/不良变化时联动刷新 UPH 与折线
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Total)) RefreshUpd();
        };
    }

    /// <summary>每 2 秒采样一次：更新 UPH、记录产量序列、重绘折线。</summary>
    private void SampleProduction()
    {
        if (_runStart == null || _runEnd != null) return;
        ProductionSeries.Add(Total);
        if (ProductionSeries.Count > 60) ProductionSeries.RemoveAt(0);
        RefreshUpd();
        RebuildPolyline();
        OnPropertyChanged(nameof(RunDurationText));
    }

    private void RefreshUpd()
    {
        if (_runStart == null) { UPH = 0; return; }
        var end = _runEnd ?? DateTime.Now;
        var secs = (end - _runStart.Value).TotalSeconds;
        UPH = secs > 1 ? Total / secs * 3600 : 0;
    }

    /// <summary>将产量序列映射为 300×120 逻辑坐标的折线点串。</summary>
    private void RebuildPolyline()
    {
        const double W = 300, H = 120, pad = 8;
        if (ProductionSeries.Count < 2) { ProductionPolylinePoints = ""; return; }
        double max = ProductionSeries.Max();
        double min = ProductionSeries.Min();
        double range = max - min;
        if (range < 1e-6) range = 1;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < ProductionSeries.Count; i++)
        {
            double x = pad + (ProductionSeries.Count == 1 ? 0 : (double)i / (ProductionSeries.Count - 1) * (W - 2 * pad));
            double y = H - pad - (ProductionSeries[i] - min) / range * (H - 2 * pad);
            sb.Append(x.ToString("F1")).Append(',').Append(y.ToString("F1")).Append(' ');
        }
        ProductionPolylinePoints = sb.ToString().Trim();
    }

    #endregion


    private void StartRun()
    {
        if (_state != MachineState.Idle) return;

        _sharedTemplate = TemplatePath;
        _sharedScore = ScoreThreshold;

        State = MachineState.Running;
        AlarmText = "";
        LastResult = "";
        _pauseGate.Set(); // 放行

        // 重置生产统计
        _runStart = DateTime.Now;
        _runEnd = null;
        ProductionSeries.Clear();
        UPH = 0;
        RefreshUpd();
        OnPropertyChanged(nameof(RunDurationText));

        // 真实硬件：上电/回零、开始取图、通知 PLC 启动
        try
        {
            HardwareManager.Instance.Motion.Connect();
        }
        catch (Exception ex) { AppendLog("运控连接失败：" + ex.Message); }

        _ = EnsureCommAndSendAsync("MACHINE:RUN");

        // 启动真实生产：优先驱动流程引擎（视觉流程）真实生产，否则退回相机自检
        StartRealProduction();

        // 监控通道统一由机台「运行」控制：进入运行态，图像由流程引擎推送
        EnsureChannelsRunning();

        // UI 定时刷新（后台线程跑流程，UI 线程按节拍刷新画面与统计，避免卡顿）
        if (!_uiTimer.IsEnabled) _uiTimer.Start();
    }

    /// <summary>启动真实生产：优先驱动流程引擎（视觉流程）真实生产，否则退回相机自检（真实取图 + 外部注入的 InspectionHook）。</summary>
    private void StartRealProduction()
    {
        if (FlowViewModel.Instance != null)
        {
            if (!_flowSubscribed)
            {
                FlowViewModel.Instance.ProductInspected += OnFlowProductInspected;
                _flowSubscribed = true;
            }
            if (!FlowViewModel.Instance.IsRunning)
            {
                _flowCts = new CancellationTokenSource();
                _flowStartedByOp = true;
                // 在后台线程跑流程循环，避免 RunStep 的视觉计算阻塞 UI 线程
                _ = Task.Run(async () =>
                {
                    try { await FlowViewModel.Instance.RunAllFlowsLoopAsync(_flowCts.Token); }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { AppendLog("流程运行异常：" + ex.Message); }
                });
            }
        }
        else
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _loopTask = Task.Run(() => ProductionLoop(token), token);
        }
    }

    /// <summary>后台流程线程回调：仅暂存最新单件结果（写字段，无 UI 操作），由 UI 定时器统一刷新界面。</summary>
    private void OnFlowProductInspected(FlowProductResult e)
    {
        if (_state != MachineState.Running) return;
        _pendingResult = e;
        _ = EnsureCommAndSendAsync(e.IsOk ? "RESULT:OK" : "RESULT:NG");
    }

    /// <summary>UI 定时器节拍：把后台线程暂存的最新结果刷新到绑定属性，避免流程线程直接触碰 UI 造成卡顿。</summary>
    private void OnUiTimerTick()
    {
        var e = _pendingResult;
        if (e == null) return;
        _pendingResult = null;

        if (_state != MachineState.Running) return;
        Total++;
        if (e.IsOk) Ok++; else Ng++;
        LastResult = e.IsOk ? "OK" : "NG";
        if (Channels.Count > 0)
        {
            // 按流程名投到对应画面；取不到名字时轮流转发
            var ch = string.IsNullOrEmpty(e.FlowName)
                ? null
                : System.Linq.Enumerable.FirstOrDefault(Channels, c => c.Name == e.FlowName);
            ch ??= Channels[_chRot % Channels.Count];
            _chRot++;
            ch.LastImage = e.Image;
            ch.MatchScore = e.Score;
            ch.DefectCount = e.DefectCount;
            ch.Status = e.IsOk ? "通过" : "失败";
            ch.FrameCount++;
        }
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
        if (_flowStartedByOp) { _flowCts?.Cancel(); _flowStartedByOp = false; }
        _runEnd = DateTime.Now;
        RefreshUpd();
        OnPropertyChanged(nameof(RunDurationText));
        _ = EnsureCommAndSendAsync("MACHINE:STOP");
        try { HardwareManager.Instance.Camera.Stop(); } catch { }
        // 监控通道统一由机台「停止」控制：全部停止取图
        foreach (var c in Channels)
            if (c.IsRunning) c.Stop();
        _uiTimer.Stop();
        State = MachineState.Idle;
    }

    private void EStop()
    {
        // 急停：立即切断并锁存，任何状态都可触发
        State = MachineState.Faulted;
        _pauseGate.Set();
        AlarmText = "急停已触发，必须复位后才能再次运行";
        _cts?.Cancel();
        if (_flowStartedByOp) { _flowCts?.Cancel(); _flowStartedByOp = false; }
        _runEnd = DateTime.Now;
        RefreshUpd();
        OnPropertyChanged(nameof(RunDurationText));
        _ = EnsureCommAndSendAsync("MACHINE:ESTOP");
        try { HardwareManager.Instance.Camera.Stop(); } catch { }
        // 急停同时停止全部监控通道
        foreach (var c in Channels)
            if (c.IsRunning) c.Stop();
        _uiTimer.Stop();
        AppendLog("⛔ 急停！");
    }

    private void Reset()
    {
        if (_state != MachineState.Faulted) return;
        State = MachineState.Idle;
        AlarmText = "";
        _runEnd = null;
        _runStart = null;
        UPH = 0;
        ProductionSeries.Clear();
        ProductionPolylinePoints = "";
        OnPropertyChanged(nameof(RunDurationText));
        _ = EnsureCommAndSendAsync("MACHINE:RESET");
        try { HardwareManager.Instance.Motion.Connect(); } catch { }
        // 复位流程：只运行一次（主流程由「运行」循环驱动，与复位流程无关），后台线程执行避免卡顿
        if (FlowViewModel.Instance != null)
        {
            AppendLog("执行复位流程（一次）…");
            _ = Task.Run(async () =>
            {
                try { await FlowViewModel.Instance.RunResetFlowsOnceAsync(CancellationToken.None); }
                catch (Exception ex) { AppendLog("复位流程异常：" + ex.Message); }
            });
        }
        AppendLog("已复位");
    }

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
// 温启志：18719361399  混淆: 温u启p志s：z1b8n7r1i9q3x6s1y3i9a9
