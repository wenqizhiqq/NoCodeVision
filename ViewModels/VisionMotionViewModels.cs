using System.Collections.ObjectModel;
using OpenCvSharp;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        public string[] ToolModes { get; } = { "模板匹配", "缺陷检测" };
        public string SelectedTool { get => _selectedTool; set => SetField(ref _selectedTool, value); }
        private string _selectedTool = "模板匹配";

        public ImageSource? DisplayImage { get => _displayImage; set => SetField(ref _displayImage, value); }
        private ImageSource? _displayImage;

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
        public double DiffThreshold { get => _diffThreshold; set => SetField(ref _diffThreshold, value); }
        private double _diffThreshold = 45;
        public double MinAreaFrac { get => _minAreaFrac; set => SetField(ref _minAreaFrac, value); }
        private double _minAreaFrac = 0.004;
        public double GlobalBrightnessThresh { get => _globalBrightnessThresh; set => SetField(ref _globalBrightnessThresh, value); }
        private double _globalBrightnessThresh = 28;
        public int EdgeTolerance { get => _edgeTolerance; set => SetField(ref _edgeTolerance, value); }
        private int _edgeTolerance;
        public double EdgeGradThresh { get => _edgeGradThresh; set => SetField(ref _edgeGradThresh, value); }
        private double _edgeGradThresh = 30;
        public int ErodeSize { get => _erodeSize; set => SetField(ref _erodeSize, value); }
        private int _erodeSize = 2;
        public int DilateSize { get => _dilateSize; set => SetField(ref _dilateSize, value); }
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

        public ICommand LoadImageCmd { get; }
        public ICommand RunCmd { get; }
        public ICommand ClearCmd { get; }

        public VisionToolViewModel()
        {
            LoadImageCmd = new RelayCommand(_ => LoadImage());
            RunCmd = new RelayCommand(_ => Run(), _ => HasImage);
            ClearCmd = new RelayCommand(_ => Clear());
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
                    StatusText = $"匹配 {results.Count} 个，缺陷 {defects.Count} 个；匹配 {LastMatchMs:F1}ms，缺陷 {LastDefectMs:F1}ms";
                }
                else
                {
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
            StatusText = "已清空结果";
        }
    }

    #endregion

    #region 运控页面（轴 / IO / 气缸 / 轴点位表 / 料盘）

    public class MotionRow
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public double Value { get; set; }
        public string Unit { get; set; } = "";
        public bool Enabled { get; set; }
        public string Address { get; set; } = "";
        public string Type { get; set; } = "";
        public string Action { get; set; } = "";
    }

    public class PointRow
    {
        public string Name { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Desc { get; set; } = "";
    }

    public class TrayCell
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public string Label { get; set; } = "";
        public bool Occupied { get; set; }
        public string Product { get; set; } = "";
    }

    public class MotionControlViewModel : ViewModelBase
    {
        public string[] Tabs { get; } = { "轴", "IO", "气缸", "轴点位表", "料盘" };
        public string SelectedTab { get => _selectedTab; set => SetField(ref _selectedTab, value); }
        private string _selectedTab = "轴";

        public ObservableCollection<MotionRow> Axes { get; }
        public ObservableCollection<MotionRow> IoPoints { get; }
        public ObservableCollection<MotionRow> Cylinders { get; }
        public ObservableCollection<PointRow> PointTable { get; }
        public ObservableCollection<TrayCell> TrayCells { get; }

        public int TrayRows { get; } = 6;
        public int TrayCols { get; } = 8;

        public MotionControlViewModel()
        {
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

            PointTable = new ObservableCollection<PointRow>
            {
                new() { Name = "取料点", X = 10.0, Y = 20.0, Z = -5.0, Desc = "从料盘抓取" },
                new() { Name = "放料点", X = 120.0, Y = 80.0, Z = 0.0, Desc = "放入工位" },
                new() { Name = "安全点", X = 0.0, Y = 0.0, Z = 50.0, Desc = "抬高处过渡" },
                new() { Name = "拍照点", X = 60.0, Y = 40.0, Z = 10.0, Desc = "视觉定位" },
                new() { Name = "待机点", X = 0.0, Y = 100.0, Z = 30.0, Desc = "回零上方" },
            };

            TrayCells = new ObservableCollection<TrayCell>();
            for (int r = 0; r < TrayRows; r++)
                for (int c = 0; c < TrayCols; c++)
                    TrayCells.Add(new TrayCell
                    {
                        Row = r,
                        Col = c,
                        Label = $"R{r + 1}C{c + 1}",
                        Occupied = (r + c) % 3 == 0,
                        Product = (r + c) % 3 == 0 ? "料号A" : "",
                    });
        }
    }

    #endregion
}
