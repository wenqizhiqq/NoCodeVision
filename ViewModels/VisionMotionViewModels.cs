using System.Collections.ObjectModel;
using NoCodeVision.Hardware;
using OpenCvSharp;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
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
        public string Status { get; set; } = "";
        public double Value { get; set; }
        public string Unit { get; set; } = "";
        public bool Enabled { get; set; }
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

    public class PointRow : ViewModelBase
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Desc { get; set; } = "";
        // 扩展参数
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double OffsetZ { get; set; }
        public double Speed { get; set; }
        public string Note { get; set; } = "";
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

    public class MotionControlViewModel : ViewModelBase
    {
        public string[] Tabs { get; } = { "轴", "IO", "气缸", "轴点位表", "料盘" };
        /// <summary>共享单例：供工程师调试页等其它页面访问同一份轴/IO/气缸数据。</summary>
        public static MotionControlViewModel? Instance { get; private set; }
        public string SelectedTab { get => _selectedTab; set => SetField(ref _selectedTab, value); }
        private string _selectedTab = "轴";

        public ObservableCollection<MotionRow> Axes { get; }
        public ObservableCollection<MotionRow> IoPoints { get; }
        public ObservableCollection<MotionRow> Cylinders { get; }
        public ObservableCollection<PointTableGroup> PointTables { get; }
        public ObservableCollection<TrayGroup> Trays { get; }

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

        public ICommand AddCmd { get; }
        public ICommand DeleteCmd { get; }
        public ICommand RenameCmd { get; }
        public ICommand AddPointCmd { get; }
        public ICommand DeletePointCmd { get; }
        public ICommand RenamePointCmd { get; }

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
                        new() { Name = "取料点", X = 10.0, Y = 20.0, Z = -5.0, Desc = "从料盘抓取" },
                        new() { Name = "放料点", X = 120.0, Y = 80.0, Z = 0.0, Desc = "放入工位" },
                        new() { Name = "安全点", X = 0.0, Y = 0.0, Z = 50.0, Desc = "抬高处过渡" },
                        new() { Name = "拍照点", X = 60.0, Y = 40.0, Z = 10.0, Desc = "视觉定位" },
                        new() { Name = "待机点", X = 0.0, Y = 100.0, Z = 30.0, Desc = "回零上方" },
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
                }
            }, _ => SelectedTab switch { "轴" => SelectedAxis != null, "IO" => SelectedIo != null, "气缸" => SelectedCylinder != null, "轴点位表" => SelectedPointTable != null, "料盘" => SelectedTray != null, _ => false });
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
    }

    #endregion
}
