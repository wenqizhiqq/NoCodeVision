using System.Collections.ObjectModel;
using NoCodeVision.Hardware;
using OpenCvSharp;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.IO;
using NoCodeVision.Services;
using GrayMatch;

namespace NoCodeVision
{
    /// <summary>叠加框描述（图像像素坐标，支持旋转），供 RoiImageView 渲染。</summary>
    public class OverlayItem
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public double AngleDeg { get; set; }
        public string Color { get; set; } = "#34C759";
        public string Label { get; set; } = "";
        public bool Dashed { get; set; }
    }
}

namespace NoCodeVision.ViewModels
{
    #region 视觉工具（模板匹配 / 缺陷检测）

    public class VisionToolViewModel : ViewModelBase
    {
        private readonly RotatedTemplateMatcher _matcher = new();
        private List<MatchResult>? _lastResults;
        private readonly DispatcherTimer _defectTimer;

        public string[] ToolModes { get; } = { "模板匹配", "缺陷检测" };
        public string SelectedTool
        {
            get => _selectedTool;
            set
            {
                if (!SetField(ref _selectedTool, value)) return;
                if (value == "缺陷检测")
                {
                    if (HasImage && _lastResults != null && _lastResults.Count > 0)
                        RedetectDefects();
                }
                else
                {
                    DefectOverlayImage = null;
                    DefectResults.Clear();
                    DefectSummaryText = "请先运行检测";
                }
            }
        }
        private string _selectedTool = "模板匹配";

        public ImageSource? DisplayImage { get => _displayImage; set => SetField(ref _displayImage, value); }
        private ImageSource? _displayImage;

        public ImageSource? DefectOverlayImage { get => _defectOverlayImage; set => SetField(ref _defectOverlayImage, value); }
        private ImageSource? _defectOverlayImage;

        public bool HasImage { get => _hasImage; set => SetField(ref _hasImage, value); }
        private bool _hasImage;

        // ROI（图像像素坐标）
        public double RoiX { get => _roiX; set => SetField(ref _roiX, value); }
        private double _roiX = 80;
        public double RoiY { get => _roiY; set => SetField(ref _roiY, value); }
        private double _roiY = 80;
        public double RoiW { get => _roiW; set => SetField(ref _roiW, value); }
        private double _roiW = 160;
        public double RoiH { get => _roiH; set => SetField(ref _roiH, value); }
        private double _roiH = 120;

        // 匹配参数
        public double AngleStart { get => _angleStart; set => SetField(ref _angleStart, value); }
        private double _angleStart;
        public double AngleEnd { get => _angleEnd; set => SetField(ref _angleEnd, value); }
        private double _angleEnd = 360;
        public double AngleStep { get => _angleStep; set => SetField(ref _angleStep, value); }
        private double _angleStep = 1;
        public double NccThreshold { get => _nccThreshold; set => SetField(ref _nccThreshold, value); }
        private double _nccThreshold = 0.7;
        public int PyramidLevels { get => _pyramidLevels; set => SetField(ref _pyramidLevels, value); }
        private int _pyramidLevels = 3;
        public double ScaleRange { get => _scaleRange; set => SetField(ref _scaleRange, value); }
        private double _scaleRange;
        public int TopN { get => _topN; set => SetField(ref _topN, value); }
        private int _topN = 50;
        public bool DenseMode { get => _denseMode; set => SetField(ref _denseMode, value); }
        private bool _denseMode;

        // 缺陷参数
        public double DiffThreshold { get => _diffThreshold; set { if (SetField(ref _diffThreshold, value)) ScheduleDefectRefresh(); } }
        private double _diffThreshold = 45;
        public double MinAreaFrac { get => _minAreaFrac; set { if (SetField(ref _minAreaFrac, value)) ScheduleDefectRefresh(); } }
        private double _minAreaFrac = 0.004;
        public double GlobalBrightnessThresh { get => _globalBrightnessThresh; set { if (SetField(ref _globalBrightnessThresh, value)) ScheduleDefectRefresh(); } }
        private double _globalBrightnessThresh = 28;
        public int EdgeTolerance { get => _edgeTolerance; set { if (SetField(ref _edgeTolerance, value)) ScheduleDefectRefresh(); } }
        private int _edgeTolerance;
        public double EdgeGradThresh { get => _edgeGradThresh; set { if (SetField(ref _edgeGradThresh, value)) ScheduleDefectRefresh(); } }
        private double _edgeGradThresh = 30;
        public int ErodeSize { get => _erodeSize; set { if (SetField(ref _erodeSize, value)) ScheduleDefectRefresh(); } }
        private int _erodeSize = 2;
        public int DilateSize { get => _dilateSize; set { if (SetField(ref _dilateSize, value)) ScheduleDefectRefresh(); } }
        private int _dilateSize = 3;

        public ObservableCollection<MatchResult> MatchResults { get; } = new();
        public ObservableCollection<DefectResult> DefectResults { get; } = new();
        public ObservableCollection<OverlayItem> Overlays { get; } = new();

        public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }
        private string _statusText = "请加载源图像并框选模板区域";
        public double LastMatchMs { get => _lastMatchMs; set => SetField(ref _lastMatchMs, value); }
        private double _lastMatchMs;
        public double LastDefectMs { get => _lastDefectMs; set => SetField(ref _lastDefectMs, value); }
        private double _lastDefectMs;
        public string DefectSummaryText { get => _defectSummaryText; set => SetField(ref _defectSummaryText, value); }
        private string _defectSummaryText = "请先运行检测";

        public ICommand LoadImageCmd { get; }
        public ICommand RunCmd { get; }
        public ICommand ClearCmd { get; }
        public ICommand ResetDefectCmd { get; }

        public VisionToolViewModel()
        {
            LoadImageCmd = new RelayCommand(_ => LoadImage());
            RunCmd = new RelayCommand(_ => Run(), _ => HasImage);
            ClearCmd = new RelayCommand(_ => Clear());
            ResetDefectCmd = new RelayCommand(_ =>
            {
                DiffThreshold = 45;
                MinAreaFrac = 0.004;
                GlobalBrightnessThresh = 28;
                EdgeTolerance = 0;
                EdgeGradThresh = 30;
                ErodeSize = 2;
                DilateSize = 3;
                RedetectDefects();
            }, _ => HasImage);
            _defectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _defectTimer.Tick += (_, __) => { _defectTimer.Stop(); RedetectDefects(); };
        }

        private void LoadImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图像|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff",
                Title = "选择源图像"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _matcher.LoadSource(dlg.FileName);
                _lastResults = null;
                DefectResults.Clear();
                DefectOverlayImage = null;
                DefectSummaryText = "请先运行检测";
                DisplayImage = new BitmapImage(new Uri(dlg.FileName));
                HasImage = true;
                StatusText = $"已加载：{System.IO.Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                StatusText = "加载失败：" + ex.Message;
            }
        }

        private void Run()
        {
            if (!HasImage) return;
            try
            {
                var roi = new OpenCvSharp.Rect((int)RoiX, (int)RoiY, Math.Max(4, (int)RoiW), Math.Max(4, (int)RoiH));
                _matcher.SetTemplateFromRoi(roi);

                double aStart = Math.Min(AngleStart, AngleEnd);
                double aEnd = Math.Max(AngleStart, AngleEnd);

                var results = _matcher.Match(PyramidLevels, aStart, aEnd, AngleStep, NccThreshold, 0.3, TopN, DenseMode ? 1 : 0);
                LastMatchMs = _matcher.LastMatchMs;
                _lastResults = results;

                MatchResults.Clear();
                Overlays.Clear();
                foreach (var r in results)
                {
                    MatchResults.Add(r);
                    Overlays.Add(new OverlayItem
                    {
                        X = r.CenterX,
                        Y = r.CenterY,
                        W = r.TemplateWidth,
                        H = r.TemplateHeight,
                        AngleDeg = -r.Angle,
                        Color = "#34C759",
                        Label = $"#{r.Index} {r.Score:F2}"
                    });
                }

                if (SelectedTool == "缺陷检测")
                {
                    _matcher.DefectOptions = new DefectOptions
                    {
                        DiffThreshold = DiffThreshold,
                        MinAreaFrac = MinAreaFrac,
                        GlobalBrightnessThresh = GlobalBrightnessThresh,
                        EdgeTolerance = EdgeTolerance,
                        EdgeGradThresh = EdgeGradThresh,
                        ErodeSize = ErodeSize,
                        DilateSize = DilateSize,
                    };
                    var defects = _matcher.DetectDefects(results);
                    LastDefectMs = _matcher.LastDefectMs;
                    DefectResults.Clear();
                    foreach (var d in defects)
                    {
                        DefectResults.Add(d);
                        Overlays.Add(new OverlayItem
                        {
                            X = d.ImgCx,
                            Y = d.ImgCy,
                            W = d.W,
                            H = d.H,
                            AngleDeg = d.RectAngle,
                            Color = "#FF3B30",
                            Label = d.Type
                        });
                    }
                    DefectSummaryText = defects.Count == 0
                        ? "未发现缺陷"
                        : $"发现 {defects.Count} 处缺陷（耗时 {LastDefectMs:F1} ms）";
                    DefectOverlayImage = BuildDefectOverlay();
                    StatusText = $"匹配 {results.Count} 个，缺陷 {defects.Count} 个；匹配 {LastMatchMs:F1}ms，缺陷 {LastDefectMs:F1}ms";
                }
                else
                {
                    DefectOverlayImage = null;
                    StatusText = $"匹配 {results.Count} 个，耗时 {LastMatchMs:F1} ms";
                }
            }
            catch (Exception ex)
            {
                StatusText = "运行失败：" + ex.Message;
            }
        }

        private void Clear()
        {
            MatchResults.Clear();
            DefectResults.Clear();
            Overlays.Clear();
            DefectOverlayImage = null;
            StatusText = "已清空结果";
        }

        /// <summary>参数改动 1 秒后自动重检缺陷（仅复用已缓存的匹配结果，不再重新匹配）。</summary>
        private void ScheduleDefectRefresh()
        {
            if (SelectedTool != "缺陷检测" || !HasImage || _lastResults == null) return;
            _defectTimer.Stop();
            _defectTimer.Start();
        }

        /// <summary>用当前参数对缓存的匹配结果重新跑缺陷检测并刷新红框叠加与列表。</summary>
        private void RedetectDefects()
        {
            if (SelectedTool != "缺陷检测" || _lastResults == null || _lastResults.Count == 0) return;
            try
            {
                _matcher.DefectOptions = new DefectOptions
                {
                    DiffThreshold = DiffThreshold,
                    MinAreaFrac = MinAreaFrac,
                    GlobalBrightnessThresh = GlobalBrightnessThresh,
                    EdgeTolerance = EdgeTolerance,
                    EdgeGradThresh = EdgeGradThresh,
                    ErodeSize = ErodeSize,
                    DilateSize = DilateSize,
                };
                var defects = _matcher.DetectDefects(_lastResults);
                LastDefectMs = _matcher.LastDefectMs;
                DefectResults.Clear();
                Overlays.Clear();
                foreach (var r in _lastResults)
                    Overlays.Add(new OverlayItem
                    {
                        X = r.CenterX,
                        Y = r.CenterY,
                        W = r.TemplateWidth,
                        H = r.TemplateHeight,
                        AngleDeg = -r.Angle,
                        Color = "#34C759",
                        Label = $"#{r.Index} {r.Score:F2}"
                    });
                foreach (var d in defects)
                {
                    DefectResults.Add(d);
                    Overlays.Add(new OverlayItem
                    {
                        X = d.ImgCx,
                        Y = d.ImgCy,
                        W = d.W,
                        H = d.H,
                        AngleDeg = d.RectAngle,
                        Color = "#FF3B30",
                        Label = d.Type
                    });
                }
                DefectSummaryText = defects.Count == 0
                    ? "未发现缺陷"
                    : $"发现 {defects.Count} 处缺陷（耗时 {LastDefectMs:F1} ms）";
                DefectOverlayImage = BuildDefectOverlay();
                StatusText = $"参数刷新：匹配 {_lastResults.Count} 个，缺陷 {defects.Count} 个；缺陷 {LastDefectMs:F1}ms";
            }
            catch (Exception ex)
            {
                StatusText = "重检失败：" + ex.Message;
            }
        }

        /// <summary>按 GrayMatch.Wpf 的方式，把每个缺陷的逐像素掩码（DefectResult.Pixels）映射回图像坐标并染红，生成一张透明叠加层。</summary>
        private ImageSource? BuildDefectOverlay()
        {
            if (DefectResults.Count == 0) return null;
            if (DisplayImage is not BitmapImage bmp) return null;
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            if (w <= 0 || h <= 0) return null;

            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            wb.Lock();
            try
            {
                int stride = wb.BackBufferStride;
                var px = new byte[stride * h];
                foreach (var d in DefectResults)
                {
                    if (d.Pixels == null || d.Pw <= 0 || d.Ph <= 0) continue;
                    double phi = -d.Angle * System.Math.PI / 180.0;
                    double cosv = System.Math.Cos(phi), sinv = System.Math.Sin(phi);
                    double tw = d.Tw, th = d.Th;
                    for (int ly = 0; ly < d.Ph; ly++)
                    {
                        int baseOff = ly * d.Pw;
                        for (int lx = 0; lx < d.Pw; lx++)
                        {
                            if (d.Pixels[baseOff + lx] == 0) continue;
                            double ux = lx - tw / 2.0;
                            double uy = ly - th / 2.0;
                            int ix = (int)System.Math.Round(d.CenterX + (ux * cosv - uy * sinv));
                            int iy = (int)System.Math.Round(d.CenterY + (ux * sinv + uy * cosv));
                            if (ix < 0 || iy < 0 || ix >= w || iy >= h) continue;
                            int idx = iy * stride + ix * 4;
                            px[idx] = 0;
                            px[idx + 1] = 0;
                            px[idx + 2] = 255;
                            px[idx + 3] = 220;
                        }
                    }
                }
                Marshal.Copy(px, 0, wb.BackBuffer, px.Length);
            }
            finally
            {
                wb.AddDirtyRect(new Int32Rect(0, 0, w, h));
                wb.Unlock();
            }
            wb.Freeze();
            return wb;
        }
    }

    #endregion

    #region 运控页面（轴 / IO / 气缸 / 轴点位表 / 料盘）

    public class MotionRow : ViewModelBase
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }
        private string _status = "";
        public string Status { get => _status; set => SetField(ref _status, value); }
        private double _value;
        public double Value { get => _value; set => SetField(ref _value, value); }
        public string Unit { get; set; } = "";
        private bool _enabled;
        public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
        public string Address { get; set; } = "";
        public string Type { get; set; } = "";
        public string Action { get; set; } = "";
        // 扩展参数
        public double Speed { get; set; }
        public double Acceleration { get; set; }
        public double Deceleration { get; set; }
        public double HomeOffset { get; set; }
        public double SoftLimitPos { get; set; }
        public double SoftLimitNeg { get; set; }
        public string Note { get; set; } = "";
        public bool Polarity { get; set; }
        public double Delay { get; set; }
        public double ExtendTime { get; set; }
        public double RetractTime { get; set; }
    }

    /// <summary>点位中单个轴槽的目标值：位置 + 速度（对齐 NoCodeMotion PointAxis）。</summary>
    public class AxisPos : ViewModelBase
    {
        private double _position;
        public double Position { get => _position; set => SetField(ref _position, value); }
        private double _speed;
        public double Speed { get => _speed; set => SetField(ref _speed, value); }
    }

    /// <summary>单个点位：含 4 个轴槽的目标位置/速度，以及专利所需的时序标记/同步组（对齐 NoCodeMotion PointItem）。</summary>
    public class PointRow : ViewModelBase
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }
        public string Desc { get; set; } = "";
        public string Note { get; set; } = "";
        // 专利：时序标记（如 "T+0ms"）与同步组（如 "GroupA"）
        private string _timingMark = "";
        public string TimingMark { get => _timingMark; set => SetField(ref _timingMark, value); }
        private string _syncGroup = "";
        public string SyncGroup { get => _syncGroup; set => SetField(ref _syncGroup, value); }
        // 4 个轴槽（位置 + 速度），与所属点位表的 AxisNames 一一对应
        public ObservableCollection<AxisPos> Axes { get; } = new();
        public PointRow()
        {
            while (Axes.Count < 4) Axes.Add(new AxisPos());
        }
        /// <summary>点位位置摘要（用于列表紧凑展示，如 "轴1:12.3 轴2:20.0"）。</summary>
        public string AxisSummary
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < Axes.Count; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append("轴").Append(i + 1).Append(':').Append(Axes[i].Position.ToString("F1"));
                }
                return sb.ToString();
            }
        }
    }

    public class TrayCell : ViewModelBase
    {
        public int Row { get; set; }
        public int Col { get; set; }
        private string _label = "";
        public string Label { get => _label; set => SetField(ref _label, value); }
        public bool Occupied { get; set; }
        private string _product = "";
        public string Product { get => _product; set => SetField(ref _product, value); }
        // 扩展参数
        public double Height { get; set; }
        public string State { get; set; } = "";
    }

    public class PointTableGroup : ViewModelBase
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }
        // 该工位所选的 4 个轴名（按槽位 0..3），决定轴列 / 属性面板的轴标签
        public ObservableCollection<string> AxisNames { get; } = new() { "X", "Y", "Z", "A" };
        public ObservableCollection<PointRow> Points { get; } = new();
    }

    public class TrayGroup : ViewModelBase
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }
        public int Rows { get; set; } = 6;
        public int Cols { get; set; } = 8;
        public string GridText => $"{Rows}×{Cols}";
        public ObservableCollection<TrayCell> Cells { get; } = new();
    }

    /// <summary>控制卡（控制器）模型，对齐 NoCodeMotion 的 AxisControllerPage。</summary>
    public class ControllerCard : ViewModelBase
    {
        private string _kind = "运动控制卡";
        public string Kind { get => _kind; set => SetField(ref _kind, value); }
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }
        private string _vendor = "";
        public string Vendor { get => _vendor; set => SetField(ref _vendor, value); }
        private string _busType = "EtherCAT";
        public string BusType { get => _busType; set => SetField(ref _busType, value); }
        private string _cardType = "";
        public string CardType { get => _cardType; set => SetField(ref _cardType, value); }
        private string _cardNo = "0";
        public string CardNo { get => _cardNo; set => SetField(ref _cardNo, value); }
        private string _axisCount = "4";
        public string AxisCount { get => _axisCount; set => SetField(ref _axisCount, value); }
        private string _connection = "网口";
        public string Connection { get => _connection; set => SetField(ref _connection, value); }
        private string _description = "";
        public string Description { get => _description; set => SetField(ref _description, value); }
    }

    public class MotionControlViewModel : ViewModelBase
    {
        public string[] Tabs { get; } = { "轴", "IO", "气缸", "轴点位表", "料盘", "控制器" };
        /// <summary>共享单例：供工程师调试页等其它页面访问同一份轴/IO/气缸数据。</summary>
        public static MotionControlViewModel? Instance { get; private set; }
        public string SelectedTab { get => _selectedTab; set => SetField(ref _selectedTab, value); }
        private string _selectedTab = "轴";

        public ObservableCollection<MotionRow> Axes { get; }
        public ObservableCollection<MotionRow> IoPoints { get; }
        public ObservableCollection<MotionRow> Cylinders { get; }
        public ObservableCollection<PointTableGroup> PointTables { get; }
        public ObservableCollection<TrayGroup> Trays { get; }
        public ObservableCollection<ControllerCard> Controllers { get; }
        public ControllerCard? SelectedController { get => _selectedController; set => SetField(ref _selectedController, value); }
        private ControllerCard? _selectedController;
        public string NewControllerName { get => _newControllerName; set => SetField(ref _newControllerName, value); }
        private string _newControllerName = "";

        public MotionRow? SelectedAxis { get => _selectedAxis; set => SetField(ref _selectedAxis, value); }
        private MotionRow? _selectedAxis;
        public MotionRow? SelectedIo { get => _selectedIo; set => SetField(ref _selectedIo, value); }
        private MotionRow? _selectedIo;
        public MotionRow? SelectedCylinder { get => _selectedCylinder; set => SetField(ref _selectedCylinder, value); }
        private MotionRow? _selectedCylinder;
        public PointRow? SelectedPoint { get => _selectedPoint; set => SetField(ref _selectedPoint, value); }
        private PointRow? _selectedPoint;
        public TrayCell? SelectedTrayCell { get => _selectedTrayCell; set => SetField(ref _selectedTrayCell, value); }
        private TrayCell? _selectedTrayCell;
        public PointTableGroup? SelectedPointTable { get => _selectedPointTable; set => SetField(ref _selectedPointTable, value); }
        private PointTableGroup? _selectedPointTable;
        public TrayGroup? SelectedTray { get => _selectedTray; set => SetField(ref _selectedTray, value); }
        private TrayGroup? _selectedTray;

        public string NewItemName { get => _newItemName; set => SetField(ref _newItemName, value); }
        private string _newItemName = "";

        // Excel 批量编辑：导出到 Excel 编辑后，按路径回读导入
        public string ExcelPath { get => _excelPath; set => SetField(ref _excelPath, value); }
        private string _excelPath = "";
        public string ExcelStatus { get => _excelStatus; set => SetField(ref _excelStatus, value); }
        private string _excelStatus = "";

        public ICommand AddCmd { get; }
        public ICommand DeleteCmd { get; }
        public ICommand RenameCmd { get; }
        public ICommand AddPointCmd { get; }
        public ICommand DeletePointCmd { get; }
        public ICommand RenamePointCmd { get; }
        public ICommand ExportPointsCmd { get; }
        public ICommand ImportPointsCmd { get; }
        public double JogStep { get => _jogStep; set => SetField(ref _jogStep, value); }
        private double _jogStep = 1.0;
        public ICommand JogPlusCmd { get; }
        public ICommand JogMinusCmd { get; }
        public ICommand HomeAxisCmd { get; }
        public ICommand EnableAxisCmd { get; }
        public ICommand StopAxisCmd { get; }

        public int TrayRows { get; } = 6;
        public int TrayCols { get; } = 8;

        public MotionControlViewModel()
        {
            Instance = this;
            Axes = new ObservableCollection<MotionRow>
            {
                new() { Name = "X 轴", Status = "使能", Value = 12.34, Unit = "mm", Enabled = true },
                new() { Name = "Y 轴", Status = "使能", Value = -3.10, Unit = "mm", Enabled = true },
                new() { Name = "Z 轴", Status = "禁用", Value = 0.00, Unit = "mm", Enabled = false },
                new() { Name = "A 轴", Status = "使能", Value = 45.0, Unit = "°", Enabled = true },
                new() { Name = "B 轴", Status = "报警", Value = 0.00, Unit = "°", Enabled = false },
            };

            IoPoints = new ObservableCollection<MotionRow>
            {
                new() { Name = "光幕", Address = "0.0", Type = "输入", Status = "ON" },
                new() { Name = "原点感应", Address = "0.1", Type = "输入", Status = "OFF" },
                new() { Name = "启动按钮", Address = "0.2", Type = "输入", Status = "ON" },
                new() { Name = "蜂鸣器", Address = "1.0", Type = "输出", Status = "OFF" },
                new() { Name = "绿灯", Address = "1.1", Type = "输出", Status = "ON" },
                new() { Name = "真空阀", Address = "1.2", Type = "输出", Status = "OFF" },
            };

            Cylinders = new ObservableCollection<MotionRow>
            {
                new() { Name = "夹爪气缸", Status = "伸出", Action = "缩回" },
                new() { Name = "顶升气缸", Status = "缩回", Action = "伸出" },
                new() { Name = "推料气缸", Status = "缩回", Action = "伸出" },
                new() { Name = "压合气缸", Status = "伸出", Action = "缩回" },
                new() { Name = "分料气缸", Status = "缩回", Action = "伸出" },
            };

            PointTables = new ObservableCollection<PointTableGroup>
            {
                new PointTableGroup
                {
                    Name = "默认点位表",
                    Points =
                    {
                        new() { Name = "取料点", Desc = "从料盘抓取", Axes = { new AxisPos { Position = 10.0, Speed = 50 }, new AxisPos { Position = 20.0, Speed = 50 }, new AxisPos { Position = -5.0, Speed = 30 }, new AxisPos { Position = 0 } } },
                        new() { Name = "放料点", Desc = "放入工位", Axes = { new AxisPos { Position = 120.0, Speed = 50 }, new AxisPos { Position = 80.0, Speed = 50 }, new AxisPos { Position = 0, Speed = 30 }, new AxisPos { Position = 0 } } },
                        new() { Name = "安全点", Desc = "抬高处过渡", Axes = { new AxisPos { Position = 0, Speed = 50 }, new AxisPos { Position = 0, Speed = 50 }, new AxisPos { Position = 50.0, Speed = 30 }, new AxisPos { Position = 0 } } },
                        new() { Name = "拍照点", Desc = "视觉定位", Axes = { new AxisPos { Position = 60.0, Speed = 40 }, new AxisPos { Position = 40.0, Speed = 40 }, new AxisPos { Position = 10.0, Speed = 20 }, new AxisPos { Position = 0 } } },
                        new() { Name = "待机点", Desc = "回零上方", Axes = { new AxisPos { Position = 0, Speed = 50 }, new AxisPos { Position = 100.0, Speed = 50 }, new AxisPos { Position = 30.0, Speed = 30 }, new AxisPos { Position = 0 } } },
                    }
                }
            };
            SelectedPointTable = PointTables[0];

            Trays = new ObservableCollection<TrayGroup>();
            var _tray0 = new TrayGroup { Name = "默认料盘" };
            for (int r = 0; r < _tray0.Rows; r++)
                for (int c = 0; c < _tray0.Cols; c++)
                    _tray0.Cells.Add(new TrayCell
                    {
                        Row = r,
                        Col = c,
                        Label = $"R{r + 1}C{c + 1}",
                        Occupied = (r + c) % 3 == 0,
                        Product = (r + c) % 3 == 0 ? "料号A" : "",
                    });
            Trays.Add(_tray0);
            SelectedTray = Trays[0];

            Controllers = new ObservableCollection<ControllerCard>
            {
                new() { Name = "雷赛控制卡", Vendor = "Leadshine", CardType = "DMC", CardNo = "0", AxisCount = "4", Connection = "网口", BusType = "EtherCAT", Description = "四轴脉冲控制卡" },
                new() { Name = "固高控制卡", Vendor = "Googol", CardType = "GTS", CardNo = "1", AxisCount = "8", Connection = "PCI", BusType = "PCI", Description = "八轴总线控制卡" },
            };
            SelectedController = Controllers[0];

            JogPlusCmd = new RelayCommand(_ => JogAxis(+JogStep), _ => SelectedAxis != null && SelectedAxis.Enabled);
            JogMinusCmd = new RelayCommand(_ => JogAxis(-JogStep), _ => SelectedAxis != null && SelectedAxis.Enabled);
            HomeAxisCmd = new RelayCommand(_ => HomeSelectedAxis(), _ => SelectedAxis != null);
            EnableAxisCmd = new RelayCommand(_ => ToggleAxisEnable(), _ => SelectedAxis != null);
            StopAxisCmd = new RelayCommand(_ => StopSelectedAxis(), _ => SelectedAxis != null);

            // 列表操作命令
            AddCmd = new RelayCommand(_ =>
            {
                switch (SelectedTab)
                {
                    case "轴": Axes.Add(new MotionRow { Name = string.IsNullOrWhiteSpace(NewItemName) ? $"轴_{Axes.Count + 1}" : NewItemName, Status = "禁用", Value = 0, Unit = "mm", Enabled = false }); break;
                    case "IO": IoPoints.Add(new MotionRow { Name = string.IsNullOrWhiteSpace(NewItemName) ? $"IO_{IoPoints.Count + 1}" : NewItemName, Address = "0.0", Type = "输入", Status = "OFF" }); break;
                    case "气缸": Cylinders.Add(new MotionRow { Name = string.IsNullOrWhiteSpace(NewItemName) ? $"气缸_{Cylinders.Count + 1}" : NewItemName, Status = "缩回", Action = "伸出" }); break;
                    case "轴点位表": PointTables.Add(new PointTableGroup { Name = string.IsNullOrWhiteSpace(NewItemName) ? $"点位表_{PointTables.Count + 1}" : NewItemName }); break;
                    case "料盘":
                        {
                            var _tg = new TrayGroup { Name = string.IsNullOrWhiteSpace(NewItemName) ? $"料盘_{Trays.Count + 1}" : NewItemName };
                            for (int r = 0; r < _tg.Rows; r++)
                                for (int c = 0; c < _tg.Cols; c++)
                                    _tg.Cells.Add(new TrayCell { Row = r, Col = c, Label = $"R{r + 1}C{c + 1}" });
                            Trays.Add(_tg);
                            break;
                        }
                    case "控制器": Controllers.Add(new ControllerCard { Name = string.IsNullOrWhiteSpace(NewItemName) ? $"控制器_{Controllers.Count + 1}" : NewItemName }); break;
                }
                NewItemName = "";
                OnPropertyChanged(nameof(NewItemName));
            });
            DeleteCmd = new RelayCommand(_ =>
            {
                switch (SelectedTab)
                {
                    case "轴": if (SelectedAxis != null) Axes.Remove(SelectedAxis); break;
                    case "IO": if (SelectedIo != null) IoPoints.Remove(SelectedIo); break;
                    case "气缸": if (SelectedCylinder != null) Cylinders.Remove(SelectedCylinder); break;
                    case "轴点位表": if (SelectedPointTable != null) PointTables.Remove(SelectedPointTable); break;
                    case "料盘": if (SelectedTray != null) Trays.Remove(SelectedTray); break;
                case "控制器": if (SelectedController != null) Controllers.Remove(SelectedController); break;
                }
            }, _ => SelectedTab switch { "轴" => SelectedAxis != null, "IO" => SelectedIo != null, "气缸" => SelectedCylinder != null, "轴点位表" => SelectedPointTable != null, "料盘" => SelectedTray != null, "控制器" => SelectedController != null, _ => false });
            RenameCmd = new RelayCommand(_ =>
            {
                if (string.IsNullOrWhiteSpace(NewItemName)) return;
                switch (SelectedTab)
                {
                    case "轴": if (SelectedAxis != null) SelectedAxis.Name = NewItemName; break;
                    case "IO": if (SelectedIo != null) SelectedIo.Name = NewItemName; break;
                    case "气缸": if (SelectedCylinder != null) SelectedCylinder.Name = NewItemName; break;
                    case "轴点位表": if (SelectedPointTable != null) SelectedPointTable.Name = NewItemName; break;
                    case "料盘": if (SelectedTray != null) SelectedTray.Name = NewItemName; break;
                case "控制器": if (SelectedController != null) SelectedController.Name = NewItemName; break;
                }
                NewItemName = "";
                OnPropertyChanged(nameof(NewItemName));
            }, _ => !string.IsNullOrWhiteSpace(NewItemName));

            AddPointCmd = new RelayCommand(_ =>
            {
                if (SelectedPointTable == null) return;
                SelectedPointTable.Points.Add(new PointRow { Name = string.IsNullOrWhiteSpace(NewItemName) ? $"点位_{SelectedPointTable.Points.Count + 1}" : NewItemName });
                NewItemName = "";
                OnPropertyChanged(nameof(NewItemName));
            }, _ => SelectedPointTable != null);
            DeletePointCmd = new RelayCommand(_ =>
            {
                if (SelectedPointTable != null && SelectedPoint != null)
                    SelectedPointTable.Points.Remove(SelectedPoint);
            }, _ => SelectedPointTable != null && SelectedPoint != null);
            RenamePointCmd = new RelayCommand(_ =>
            {
                if (string.IsNullOrWhiteSpace(NewItemName) || SelectedPointTable == null || SelectedPoint == null) return;
                SelectedPoint.Name = NewItemName;
                NewItemName = "";
                OnPropertyChanged(nameof(NewItemName));
            }, _ => !string.IsNullOrWhiteSpace(NewItemName) && SelectedPointTable != null && SelectedPoint != null);

            ExportPointsCmd = new RelayCommand(_ =>
            {
                if (SelectedPointTable == null) return;
                var path = string.IsNullOrWhiteSpace(ExcelPath)
                    ? ExcelBatchEdit.ExportPoints(SelectedPointTable.Points)
                    : ExcelBatchEdit.ExportPoints(SelectedPointTable.Points, Path.GetFileNameWithoutExtension(ExcelPath));
                if (string.IsNullOrWhiteSpace(ExcelPath)) ExcelBatchEdit.OpenInExcel(path);
                ExcelStatus = "已导出 " + SelectedPointTable.Points.Count + " 个点位 → " + path;
            }, _ => SelectedPointTable != null);
            ImportPointsCmd = new RelayCommand(_ =>
            {
                var path = ExcelPath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    ExcelStatus = "请先在「Excel 路径」填写有效文件，或先点「导出Excel」生成文件。";
                    return;
                }
                var rows = ExcelBatchEdit.ImportPoints(path);
                if (SelectedPointTable != null)
                {
                    SelectedPointTable.Points.Clear();
                    foreach (var r in rows) SelectedPointTable.Points.Add(r);
                }
                ExcelStatus = "已导入 " + rows.Count + " 个点位（来自 " + path + "）";
            }, _ => !string.IsNullOrWhiteSpace(ExcelPath) && File.Exists(ExcelPath));

            // Connect real motion controller (simulated for now); refresh axis positions on a timer
            HardwareManager.Instance.Motion.Connect();
            var _baseAxes = Axes.ToList();
            var _rnd = new System.Random();
            var _mt = new System.Threading.Timer(_ =>
            {
                try
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var ax in _baseAxes)
                            if (ax.Enabled) ax.Value += (_rnd.NextDouble() - 0.5) * 0.06;
                        Axes.Clear();
                        foreach (var ax in _baseAxes) Axes.Add(ax);
                    });
                }
                catch { }
            }, null, 0, 400);
        }

        // ===== 运控实时控制（JOG / 回零 / 使能 / 停止），对齐 NoCodeMotion 轴控交互 =====
        private void JogAxis(double delta)
        {
            if (SelectedAxis == null) return;
            try
            {
                HardwareManager.Instance.Motion.Jog(SelectedAxis.Name, delta);
                if (SelectedAxis.Enabled) SelectedAxis.Value += delta;
                OnPropertyChanged(nameof(Axes));
            }
            catch { }
        }
        private void HomeSelectedAxis()
        {
            if (SelectedAxis == null) return;
            try
            {
                HardwareManager.Instance.Motion.EnableAxis(SelectedAxis.Name, true);
                SelectedAxis.Enabled = true;
                SelectedAxis.Status = "使能";
                SelectedAxis.Value = 0;
                OnPropertyChanged(nameof(Axes));
            }
            catch { }
        }
        private void ToggleAxisEnable()
        {
            if (SelectedAxis == null) return;
            try
            {
                SelectedAxis.Enabled = !SelectedAxis.Enabled;
                HardwareManager.Instance.Motion.EnableAxis(SelectedAxis.Name, SelectedAxis.Enabled);
                SelectedAxis.Status = SelectedAxis.Enabled ? "使能" : "禁用";
                OnPropertyChanged(nameof(Axes));
            }
            catch { }
        }
        private void StopSelectedAxis()
        {
            if (SelectedAxis == null) return;
            try { HardwareManager.Instance.Motion.Jog(SelectedAxis.Name, 0); }
            catch { }
        }
    }

    #region 多通道视觉监控（多相机同时检测状态面板）

    /// <summary>单通道视觉检测状态，供多通道监控仪表盘使用。</summary>
    public class VisionChannel : ViewModelBase
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }

        private string _cameraId = "";
        public string CameraId { get => _cameraId; set => SetField(ref _cameraId, value); }

        private string _status = "空闲";
        public string Status { get => _status; set => SetField(ref _status, value); }

        private System.Windows.Media.ImageSource? _lastImage;
        public System.Windows.Media.ImageSource? LastImage { get => _lastImage; set => SetField(ref _lastImage, value); }

        private double _matchScore;
        public double MatchScore { get => _matchScore; set => SetField(ref _matchScore, value); }

        private int _defectCount;
        public int DefectCount { get => _defectCount; set => SetField(ref _defectCount, value); }

        private double _cycleTime;
        public double CycleTime { get => _cycleTime; set => SetField(ref _cycleTime, value); }

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetField(ref _isConnected, value); }

        private bool _isRunning;
        public bool IsRunning { get => _isRunning; set => SetField(ref _isRunning, value); }

        private int _frameCount;
        public int FrameCount { get => _frameCount; set => SetField(ref _frameCount, value); }

        public ICommand StartCmd { get; }
        public ICommand StopCmd { get; }

        public VisionChannel(string name, string cameraId)
        {
            Name = name;
            CameraId = cameraId;
            StartCmd = new RelayCommand(_ => Start(), _ => !IsRunning);
            StopCmd = new RelayCommand(_ => Stop(), _ => IsRunning);
        }

        /// <summary>启动通道：通过 HardwareManager 获取/创建真实（或模拟）相机并开始采集。</summary>
        internal void Start()
        {
            try
            {
                var cam = HardwareManager.Instance.GetOrCreateCamera(CameraId);
                cam.FrameReady += OnFrameReady;
                cam.Start(CameraId);
                IsRunning = true;
                Status = "运行中";
                IsConnected = true;
            }
            catch (Exception ex)
            {
                Status = "错误";
                System.Diagnostics.Debug.WriteLine($"[通道{CameraId} 启动失败] {ex.Message}");
            }
        }

        /// <summary>停止通道采集。</summary>
        internal void Stop()
        {
            try
            {
                if (HardwareManager.Instance.Cameras.TryGetValue(CameraId, out var cam))
                {
                    cam.FrameReady -= OnFrameReady;
                    cam.Stop();
                }
            }
            catch { }
            IsRunning = false;
            Status = "已停止";
        }

        /// <summary>相机帧回调：在 UI 线程更新图像和帧计数。</summary>
        private void OnFrameReady(System.Windows.Media.Imaging.BitmapSource frame)
        {
            // 通过 Dispatcher 回到 UI 线程更新 INPC 属性
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                LastImage = frame;
                FrameCount++;
                // 模拟匹配分数波动（0.80 ~ 0.99，真实场景替换为算法结果）
                if (FrameCount % 10 == 0)
                {
                    var rnd = Random.Shared.Next(8000, 9990) * 0.0001;
                    MatchScore = MatchScore > 0 ? Math.Round(MatchScore * 0.7 + rnd * 0.3, 3) : rnd;
                    // 模拟缺陷检测（低分时随机出现缺陷）
                    DefectCount = MatchScore < 0.85 ? Random.Shared.Next(0, 4) : 0;
                    // 模拟周期时间（30~65ms）
                    CycleTime = 30 + Random.Shared.NextDouble() * 35;
                    // 根据分数自动切换状态
                    Status = MatchScore >= 0.85 ? "通过" : (MatchScore > 0 ? "失败" : "运行中");
                }
            }));
        }
    }

    /// <summary>多通道视觉监控仪表盘 VM：同时展示 N 个相机通道的检测状态。</summary>
    public class MultiChannelMonitorViewModel : ViewModelBase
    {
        public ObservableCollection<VisionChannel> Channels { get; } = new();
        public VisionChannel? SelectedChannel { get => _selectedChannel; set => SetField(ref _selectedChannel, value); }
        private VisionChannel? _selectedChannel;

        /// <summary>是否显示通道详情浮层（点击卡片放大）。</summary>
        public bool ShowDetail { get => _showDetail; set => SetField(ref _showDetail, value); }
        private bool _showDetail;

        public int LayoutColumns { get => _layoutColumns; set => SetField(ref _layoutColumns, value); }
        private int _layoutColumns = 3;

        public string GlobalStatus { get => _globalStatus; set => SetField(ref _globalStatus, value); }
        private string _globalStatus = "全部就绪";

        public ICommand AddChannelCmd { get; }
        public ICommand RemoveChannelCmd { get; }
        public ICommand StartAllCmd { get; }
        public ICommand StopAllCmd { get; }
        public ICommand Layout2Cmd { get; }
        public ICommand Layout3Cmd { get; }
        public ICommand Layout4Cmd { get; }
        /// <summary>关闭详情浮层。</summary>
        public ICommand CloseDetailCmd { get; }

        private int _channelCounter;
        private readonly System.Windows.Threading.DispatcherTimer _pollTimer;
        private VisionChannel? _dragSource;

        public MultiChannelMonitorViewModel()
        {
            AddChannelCmd = new RelayCommand(_ => AddChannel());
            RemoveChannelCmd = new RelayCommand(_ => { if (SelectedChannel != null) Channels.Remove(SelectedChannel); }, _ => SelectedChannel != null);
            StartAllCmd = new RelayCommand(_ => StartAll());
            StopAllCmd = new RelayCommand(_ => StopAll());
            Layout2Cmd = new RelayCommand(_ => LayoutColumns = 2);
            Layout3Cmd = new RelayCommand(_ => LayoutColumns = 3);
            Layout4Cmd = new RelayCommand(_ => LayoutColumns = 4);
            CloseDetailCmd = new RelayCommand(_ => ShowDetail = false);

            // 预置 4 个示例通道（对齐用户「多个通道」场景）
            AddChannel("通道 1 · 上表面检测", "CAM-01");
            AddChannel("通道 2 · 下表面检测", "CAM-02");
            AddChannel("通道 3 · 侧面定位", "CAM-03");
            AddChannel("通道 4 · 缺陷复检", "CAM-04");

            // 定时器：100ms 轮询一次各运行中通道（作为 FrameReady 事件的补充/兜底）
            _pollTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _pollTimer.Tick += PollChannels;
        }

        /// <summary>启动定时器（在 View Loaded 时调用）。</summary>
        internal void StartPolling() { if (!_pollTimer.IsEnabled) _pollTimer.Start(); }

        /// <summary>停止定时器（在 View Unloaded 时调用）。</summary>
        internal void StopPolling() { _pollTimer.Stop(); }

        /// <summary>轮询所有运行中通道：对没有通过 FrameReady 更新的通道主动 GrabOne。</summary>
        private void PollChannels(object? sender = null, EventArgs? e = null)
        {
            foreach (var ch in Channels)
            {
                if (!ch.IsRunning) continue;
                // 如果相机支持 FrameReady 事件则依赖事件推送；否则主动轮询
                if (HardwareManager.Instance.Cameras.TryGetValue(ch.CameraId, out var cam))
                {
                    try
                    {
                        var frame = cam.GrabOne();
                        if (frame != null)
                        {
                            ch.LastImage = frame;
                            ch.FrameCount++;
                        }
                    }
                    catch { /* 轮询失败静默跳过 */ }
                }
                // 模拟匹配指标波动（每 10 次轮询更新一次，避免过于频繁）
                if (ch.FrameCount % 10 == 0)
                {
                    var rnd = Random.Shared.Next(8000, 9990) * 0.0001;
                    ch.MatchScore = ch.MatchScore > 0 ? Math.Round(ch.MatchScore * 0.7 + rnd * 0.3, 3) : rnd;
                    ch.DefectCount = ch.MatchScore < 0.85 ? Random.Shared.Next(0, 4) : 0;
                    ch.CycleTime = 30 + Random.Shared.NextDouble() * 35;
                    ch.Status = ch.MatchScore >= 0.85 ? "通过" : (ch.MatchScore > 0 ? "失败" : "运行中");
                }
            }
            UpdateGlobalStatus();
        }

        private void AddChannel(string? name = null, string? cameraId = null)
        {
            _channelCounter++;
            var ch = new VisionChannel(name ?? $"通道 {_channelCounter}", cameraId ?? $"CAM-{_channelCounter:D2}");
            // 模拟不同状态让界面更真实
            var states = new[] { ("空闲", 0.0, 0, 0.0), ("运行中", 0.92, 0, 45.2), ("通过", 0.87, 0, 38.6), ("失败", 0.0, 3, 52.1) };
            var s = states[(Channels.Count) % states.Length];
            ch.Status = s.Item1; ch.MatchScore = s.Item2; ch.DefectCount = s.Item3; ch.CycleTime = s.Item4;
            ch.IsConnected = true;
            Channels.Add(ch);
            UpdateGlobalStatus();
        }

        private void StartAll()
        {
            foreach (var ch in Channels) if (!ch.IsRunning) ch.Start();
            UpdateGlobalStatus();
        }

        private void StopAll()
        {
            foreach (var ch in Channels) if (ch.IsRunning) ch.Stop();
            UpdateGlobalStatus();
        }

        private void UpdateGlobalStatus()
        {
            var running = Channels.Count(c => c.IsRunning);
            var total = Channels.Count;
            GlobalStatus = running > 0 ? $"{running}/{total} 通道运行中" : $"{total} 通道就绪";
        }

        // ===== 拖拽排序（供 XAML code-behind 调用） =====

        /// <summary>记录拖拽源通道。</summary>
        internal void OnDragStart(VisionChannel ch) { _dragSource = ch; }

        /// <summary>拖拽放入目标通道：交换两者在集合中的位置。</summary>
        internal void OnDrop(VisionChannel target)
        {
            if (_dragSource == null || _dragSource == target) return;
            var srcIdx = Channels.IndexOf(_dragSource);
            var tgtIdx = Channels.IndexOf(target);
            if (srcIdx < 0 || tgtIdx < 0) return;
            Channels.Move(srcIdx, tgtIdx);
            _dragSource = null;
        }

        /// <summary>点击卡片：选中并打开详情浮层。</summary>
        internal void OnChannelClick(VisionChannel ch)
        {
            SelectedChannel = ch;
            ShowDetail = true;
        }
    }

    #endregion

    #endregion
}
// 温启志：18719361399  混淆: 温g启j志n：a1b8f7w1e9q3z6p1z3f9a9
