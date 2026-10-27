using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NoCodeVision.Views.Controls
{
    /// <summary>
    /// 源图显示 + ROI 绘制 + 匹配/缺陷框叠加。
    /// 采用与 GrayMatch.Wpf 一致的做法：图像以原生像素尺寸渲染在 PixelCanvas 中，
    /// 整体用 RenderTransform 缩放/平移适配显示区；ROI 与叠加框均在「图像像素」坐标系下绘制，
    /// 因此随变换一起缩放，无需手动计算 letterbox 偏移。
    /// </summary>
    public partial class RoiImageView : UserControl
    {
        public RoiImageView()
        {
            InitializeComponent();
            // 用 handledEventsToo 注册：属性面板外面套着 ScrollViewer，中键事件可能被上层标记为已处理，
            // XAML 里的 PreviewMouseDown="..." 这种写法就再也收不到，表现就是"中键拖不动"。
            AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(OnViewPreviewMouseDown), true);
            AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(OnViewPreviewMouseMove), true);
            AddHandler(PreviewMouseUpEvent, new MouseButtonEventHandler(OnViewPreviewMouseUp), true);
            AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler(OnViewPreviewMouseWheel), true);
            SizeChanged += (_, _) => UpdateFit();
            Loaded += (_, _) =>
            {
                if (Measures == null) Measures = new ObservableCollection<MeasureItem>();
                UpdateModeButtons();
                UpdateFit();
            };
        }

        #region DependencyProperties

        public ImageSource? SourceImage
        {
            get => (ImageSource?)GetValue(SourceImageProperty);
            set => SetValue(SourceImageProperty, value);
        }
        public static readonly DependencyProperty SourceImageProperty =
            DependencyProperty.Register(nameof(SourceImage), typeof(ImageSource), typeof(RoiImageView),
                new PropertyMetadata(null, OnSourceChanged));

        public double ImagePixelWidth
        {
            get => (double)GetValue(ImagePixelWidthProperty);
            set => SetValue(ImagePixelWidthProperty, value);
        }
        public static readonly DependencyProperty ImagePixelWidthProperty =
            DependencyProperty.Register(nameof(ImagePixelWidth), typeof(double), typeof(RoiImageView), new PropertyMetadata(0.0));

        public double ImagePixelHeight
        {
            get => (double)GetValue(ImagePixelHeightProperty);
            set => SetValue(ImagePixelHeightProperty, value);
        }
        public static readonly DependencyProperty ImagePixelHeightProperty =
            DependencyProperty.Register(nameof(ImagePixelHeight), typeof(double), typeof(RoiImageView), new PropertyMetadata(0.0));

        public double RoiX { get => (double)GetValue(RoiXProperty); set => SetValue(RoiXProperty, value); }
        public static readonly DependencyProperty RoiXProperty =
            DependencyProperty.Register(nameof(RoiX), typeof(double), typeof(RoiImageView), new PropertyMetadata(0.0, OnRoiChanged));

        public double RoiY { get => (double)GetValue(RoiYProperty); set => SetValue(RoiYProperty, value); }
        public static readonly DependencyProperty RoiYProperty =
            DependencyProperty.Register(nameof(RoiY), typeof(double), typeof(RoiImageView), new PropertyMetadata(0.0, OnRoiChanged));

        public double RoiW { get => (double)GetValue(RoiWProperty); set => SetValue(RoiWProperty, value); }
        public static readonly DependencyProperty RoiWProperty =
            DependencyProperty.Register(nameof(RoiW), typeof(double), typeof(RoiImageView), new PropertyMetadata(120.0, OnRoiChanged));

        public double RoiH { get => (double)GetValue(RoiHProperty); set => SetValue(RoiHProperty, value); }
        public static readonly DependencyProperty RoiHProperty =
            DependencyProperty.Register(nameof(RoiH), typeof(double), typeof(RoiImageView), new PropertyMetadata(80.0, OnRoiChanged));

        /// <summary>旋转矩形工具的角度（度），仅 RotatedRect 模式使用。</summary>
        public double RoiAngle { get => (double)GetValue(RoiAngleProperty); set => SetValue(RoiAngleProperty, value); }
        public static readonly DependencyProperty RoiAngleProperty =
            DependencyProperty.Register(nameof(RoiAngle), typeof(double), typeof(RoiImageView), new PropertyMetadata(0.0, OnRoiChanged));

        public IEnumerable? Overlays
        {
            get => (IEnumerable?)GetValue(OverlaysProperty);
            set => SetValue(OverlaysProperty, value);
        }
        public static readonly DependencyProperty OverlaysProperty =
            DependencyProperty.Register(nameof(Overlays), typeof(IEnumerable), typeof(RoiImageView),
                new PropertyMetadata(null, OnOverlaysChanged));

        public ImageSource? DefectOverlay
        {
            get => (ImageSource?)GetValue(DefectOverlayProperty);
            set => SetValue(DefectOverlayProperty, value);
        }
        public static readonly DependencyProperty DefectOverlayProperty =
            DependencyProperty.Register(nameof(DefectOverlay), typeof(ImageSource), typeof(RoiImageView),
                new PropertyMetadata(null, OnDefectOverlayChanged));

        private static void OnDefectOverlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiImageView v)
            {
                v.DefectLayer.Source = v.DefectOverlay;
                // 与主图同样按像素钉尺寸（1 像素 = 1 DIP），保证缺陷叠加层与底图严格对齐
                if (v.DefectOverlay is System.Windows.Media.Imaging.BitmapSource bs && bs.PixelWidth > 0)
                {
                    v.DefectLayer.Width = bs.PixelWidth;
                    v.DefectLayer.Height = bs.PixelHeight;
                }
            }
        }

        #endregion

        #region 交互模式 / 测量

        /// <summary>交互模式：Roi=框选 ROI；Line=绘制线段测距；Circle=绘制圆测半径。</summary>
        public string MeasureMode
        {
            get => (string)GetValue(MeasureModeProperty);
            set => SetValue(MeasureModeProperty, value);
        }
        public static readonly DependencyProperty MeasureModeProperty =
            DependencyProperty.Register(nameof(MeasureMode), typeof(string), typeof(RoiImageView),
                new PropertyMetadata("Roi", OnMeasureModeChanged));

        private static void OnMeasureModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiImageView v)
            {
                v._measureDragging = false;
                v._roiDragging = false;
                v._rotating = false;
                if (v.PixelCanvas.IsMouseCaptured) v.PixelCanvas.ReleaseMouseCapture();
                v.UpdateModeButtons();
            }
        }

        /// <summary>测量结果集合（MeasureItem）。由外部绑定（如 ViewModel）或控件内部自行维护。</summary>
        public IEnumerable? Measures
        {
            get => (IEnumerable?)GetValue(MeasuresProperty);
            set => SetValue(MeasuresProperty, value);
        }
        public static readonly DependencyProperty MeasuresProperty =
            DependencyProperty.Register(nameof(Measures), typeof(IEnumerable), typeof(RoiImageView),
                new PropertyMetadata(null, OnMeasuresChanged));

        private static void OnMeasuresChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiImageView v)
            {
                if (e.OldValue is INotifyCollectionChanged oldC) oldC.CollectionChanged -= v.OnMeasuresCollectionChanged;
                if (e.NewValue is INotifyCollectionChanged newC) newC.CollectionChanged += v.OnMeasuresCollectionChanged;
                v.RenderMeasures();
            }
        }

        private void OnMeasuresCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RenderMeasures();

        #endregion

        #region 视图变换：中键滚轮缩放（光标锚点）+ 中键拖动平移 + 复位适配
        //
        // 结构：图像按原生像素画在 PixelCanvas 里，PixelCanvas 挂 LayoutTransform(Scale+Translate)：
        //     屏幕坐标 = scale × 图像像素坐标 + translate
        // 缩放/平移只改这两个值；ROI/测量/叠加框都画在图像像素坐标里，天然跟随变换。
        //
        // 事件挂在 UserControl 的 Preview 级：光标在图像上或四周暗区都响应，
        // 并抢在外层 ScrollViewer 之前处理滚轮；左键绘制走 PixelCanvas 自己的冒泡事件，互不干扰。

        private const double ZoomStep = 1.2;  // 每格滚轮的缩放倍率
        private double _fitScale = 1.0;       // 适配显示区的缩放比例
        private double _zoomFactor = 1.0;     // 用户在适配基础上的额外倍数（限制 0.5~10）
        private bool _userTransformed;        // 用户手动缩放/平移过 → 尺寸变化时不再自动复位
        private bool _panning;                // 中键拖动进行中
        private Point _panOrigin;             // 拖动起点（Root 坐标）
        private double _panBaseX, _panBaseY;  // 拖动起点时的平移量

        private double CurrentScale => PixelScale.ScaleX <= 0 ? 1 : PixelScale.ScaleX;

        /// <summary>统一入口：scale 和 translate 永远一起写，不出现半新半旧的中间状态。</summary>
        private void ApplyTransform(double scale, double transX, double transY)
        {
            PixelScale.ScaleX = PixelScale.ScaleY = scale;
            PixelTranslate.X = transX;
            PixelTranslate.Y = transY;
        }

        /// <summary>
        /// 屏幕(Root)坐标 → 图像像素坐标：对 screen = scale*local + translate 求逆。
        /// 不要用 GetPosition(PixelCanvas)，它在 LayoutTransform 元素上不可靠（缩放锚点漂移的根因）。
        /// </summary>
        private Point ScreenToImage(Point p)
        {
            double s = CurrentScale;
            return new Point((p.X - PixelTranslate.X) / s, (p.Y - PixelTranslate.Y) / s);
        }

        /// <summary>按显示区当前尺寸重算适配比例；用户没手动动过就复位，动过则只更新比例、保留其视图。</summary>
        private void UpdateFit()
        {
            double sw = ImagePixelWidth, sh = ImagePixelHeight;
            double vw = Root.ActualWidth, vh = Root.ActualHeight;
            if (sw <= 0 || sh <= 0 || vw <= 0 || vh <= 0) return;
            _fitScale = Math.Min(vw / sw, vh / sh);
            if (_userTransformed)
                PixelScale.ScaleX = PixelScale.ScaleY = _fitScale * _zoomFactor;
            else
                FitToView();
        }

        /// <summary>复位：按当前显示区适配（最大化铺满、不裁切）并居中。</summary>
        private void FitToView()
        {
            double sw = ImagePixelWidth, sh = ImagePixelHeight;
            double vw = Root.ActualWidth, vh = Root.ActualHeight;
            if (sw <= 0 || sh <= 0 || vw <= 0 || vh <= 0) return;
            _fitScale = Math.Min(vw / sw, vh / sh);
            _zoomFactor = 1.0;
            double s = _fitScale;
            ApplyTransform(s, (vw - sw * s) / 2, (vh - sh * s) / 2);
        }

        // —— 中键滚轮：以光标为锚点缩放 ——
        private void OnViewPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ImagePixelWidth <= 0 || Root.ActualWidth <= 0) return;
            double zoom = _zoomFactor * (e.Delta > 0 ? ZoomStep : 1 / ZoomStep);
            zoom = Math.Max(0.5, Math.Min(10.0, zoom)); // 相对适配比例 0.5x ~ 10x
            if (Math.Abs(zoom - _zoomFactor) < 1e-9) { e.Handled = true; return; } // 已到边界
            _zoomFactor = zoom;
            double s = _fitScale * zoom;

            // 先取光标下的图像像素（用旧 scale/translate），再让该点缩放后仍停在光标处
            Point cursor = e.GetPosition(Root);
            Point anchor = ScreenToImage(cursor);
            ApplyTransform(s, cursor.X - anchor.X * s, cursor.Y - anchor.Y * s);
            _userTransformed = true;
            e.Handled = true;
        }

        // —— 中键按下：开始拖动平移 ——
        private void OnViewPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ImagePixelWidth <= 0 || e.ChangedButton != MouseButton.Middle) return;
            _panning = true;
            _panOrigin = e.GetPosition(Root);
            _panBaseX = PixelTranslate.X;
            _panBaseY = PixelTranslate.Y;
            Root.CaptureMouse();
            _userTransformed = true;
            e.Handled = true;
        }

        // —— 中键拖动：平移图像 ——
        private void OnViewPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_panning) return;
            Point cur = e.GetPosition(Root);
            PixelTranslate.X = _panBaseX + (cur.X - _panOrigin.X);
            PixelTranslate.Y = _panBaseY + (cur.Y - _panOrigin.Y);
            e.Handled = true;
        }

        // —— 中键松开：结束平移 ——
        private void OnViewPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_panning || e.ChangedButton != MouseButton.Middle) return;
            _panning = false;
            if (Root.IsMouseCaptured) Root.ReleaseMouseCapture();
            e.Handled = true;
        }

        /// <summary>复位按钮：回到适配 + 居中（最大化铺满）。</summary>
        private void OnFitViewClick(object sender, RoutedEventArgs e)
        {
            _userTransformed = false;
            _panning = false;
            if (Root.IsMouseCaptured) Root.ReleaseMouseCapture();
            FitToView();
            if (Root.ActualWidth <= 0 || Root.ActualHeight <= 0) // 布局未就绪时渲染后补一次
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(FitToView));
        }

        #endregion

        #region Source / ROI / Overlays 变更

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiImageView v)
            {
                v.Img.Source = v.SourceImage;
                v.DefectLayer.Source = null;
                if (v.SourceImage is System.Windows.Media.Imaging.BitmapSource bmp)
                {
                    v.ImagePixelWidth = bmp.PixelWidth;
                    v.ImagePixelHeight = bmp.PixelHeight;
                    // 像素画布尺寸 = 图像原生像素尺寸；显示缩放交给 LayoutTransform
                    v.PixelCanvas.Width = bmp.PixelWidth;
                    v.PixelCanvas.Height = bmp.PixelHeight;
                    // 关键：显式把 Image 钉成「1 像素 = 1 DIP」（配合 XAML 里 Stretch=Fill）。
                    // 否则 Stretch=None 按图片自带 DPI 渲染（72/144/200 DPI 的图会放大/缩小），
                    // 实际渲染尺寸和用 PixelWidth 算的 FitView 适配/居中对不上，
                    // 表现就是「复位后图像不居中、也没有最大化铺满」。
                    v.Img.Width = bmp.PixelWidth;
                    v.Img.Height = bmp.PixelHeight;
                    v.DefectLayer.Width = bmp.PixelWidth;
                    v.DefectLayer.Height = bmp.PixelHeight;
                    v.Placeholder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    v.ImagePixelWidth = 0;
                    v.ImagePixelHeight = 0;
                    v.PixelCanvas.Width = 0;
                    v.PixelCanvas.Height = 0;
                    v.Placeholder.Visibility = Visibility.Visible;
                }
                v._userTransformed = false;
                v.UpdateFit();
                v.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(v.UpdateFit));
            }
        }

        private static void OnRoiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiImageView v) v.RenderRoi();
        }

        private static void OnOverlaysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiImageView v) v.AttachOverlaysCollection();
        }

        private INotifyCollectionChanged? _overlaysCollection;

        private void AttachOverlaysCollection()
        {
            if (_overlaysCollection != null)
                _overlaysCollection.CollectionChanged -= OnOverlaysCollectionChanged;
            _overlaysCollection = Overlays as INotifyCollectionChanged;
            if (_overlaysCollection != null)
                _overlaysCollection.CollectionChanged += OnOverlaysCollectionChanged;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderOverlays));
        }

        private void OnOverlaysCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderOverlays));
        }

        #endregion

        #region ROI 渲染（图像像素坐标直接绘制）

        private void RenderRoi()
        {
            if (ImagePixelWidth <= 0) { RoiRect.Visibility = Visibility.Collapsed; HideHandles(); Hrot.Visibility = Visibility.Collapsed; return; }

            double x = RoiX, y = RoiY, w = Math.Max(RoiW, 4), h = Math.Max(RoiH, 4);
            bool isRot = MeasureMode == "RotatedRect";
            bool isPoint = MeasureMode == "Point";

            RoiRect.Width = w; RoiRect.Height = h;
            Canvas.SetLeft(RoiRect, x); Canvas.SetTop(RoiRect, y);
            RoiRect.RenderTransform = isRot ? new RotateTransform(RoiAngle, w / 2, h / 2) : null;
            RoiRect.Visibility = isPoint ? Visibility.Collapsed : Visibility.Visible;

            if (isRot)
            {
                HideHandles();
                double cx = x + w / 2, cy = y + h / 2;
                double rad = RoiAngle * Math.PI / 180.0;
                double hx = cx + (h / 2) * Math.Sin(rad);
                double hy = cy - (h / 2) * Math.Cos(rad);
                Canvas.SetLeft(Hrot, hx - 6); Canvas.SetTop(Hrot, hy - 6);
                Hrot.Visibility = Visibility.Visible;
            }
            else
            {
                Hrot.Visibility = Visibility.Collapsed;
                if (!isPoint)
                {
                    PlaceHandle(Htl, x, y);
                    PlaceHandle(Htr, x + w - 9, y);
                    PlaceHandle(Hbl, x, y + h - 9);
                    PlaceHandle(Hbr, x + w - 9, y + h - 9);
                }
                else
                {
                    HideHandles();
                }
            }

            RoiTipText.Text = $"X:{RoiX:F0}  Y:{RoiY:F0}  W:{RoiW:F0}  H:{RoiH:F0}" + (isRot ? $"  ∠:{RoiAngle:F0}°" : "");
        }

        private void PlaceHandle(Rectangle h, double x, double y)
        {
            Canvas.SetLeft(h, x); Canvas.SetTop(h, y); h.Visibility = Visibility.Visible;
        }
        private void HideHandles()
        {
            Htl.Visibility = Htr.Visibility = Hbl.Visibility = Hbr.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region 叠加框渲染（图像像素坐标直接绘制）

        private void RenderOverlays()
        {
            // 移除旧叠加（保留 ROI 及其手柄：固定子元素 0-4）
            var toRemove = new List<UIElement>();
            for (int i = Layer.Children.Count - 1; i >= 0; i--)
            {
                var c = Layer.Children[i];
                if (c != RoiRect && c != Htl && c != Htr && c != Hbl && c != Hbr)
                    toRemove.Add(c);
            }
            foreach (var c in toRemove) Layer.Children.Remove(c);

            if (Overlays == null || ImagePixelWidth <= 0) return;

            foreach (var item in Overlays)
            {
                if (item is not OverlayItem o) continue;
                double w = Math.Max(o.W, 2);
                double h = Math.Max(o.H, 2);
                double cx = o.X - w / 2;
                double cy = o.Y - h / 2;

                var rect = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(o.Color)!),
                    StrokeThickness = 2,
                    StrokeDashArray = o.Dashed ? new DoubleCollection { 4, 3 } : null,
                    RadiusX = 2,
                    RadiusY = 2,
                };
                rect.RenderTransform = new RotateTransform(o.AngleDeg, w / 2, h / 2);
                Canvas.SetLeft(rect, cx);
                Canvas.SetTop(rect, cy);
                Panel.SetZIndex(rect, 1);
                Layer.Children.Add(rect);

                if (!string.IsNullOrWhiteSpace(o.Label))
                {
                    var tb = new TextBlock
                    {
                        Text = o.Label,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(o.Color)!),
                        FontSize = 11,
                        Background = new SolidColorBrush(Color.FromArgb(180, 28, 28, 34)),
                        Padding = new Thickness(3, 1, 3, 1),
                    };
                    Canvas.SetLeft(tb, cx);
                    Canvas.SetTop(tb, cy - 16);
                    Panel.SetZIndex(tb, 2);
                    Layer.Children.Add(tb);
                }
            }
        }

        #endregion

        #region 鼠标交互（PixelCanvas 级）：左键画 ROI / 测量 / 旋转矩形

        private bool _roiDragging;
        private bool _rotating;
        private Point _roiStart;

        private bool _measureDragging;
        private Point _measureStart;
        private Point _measureCurrent;
        private MeasureItem? _previewMeasure;

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ImagePixelWidth <= 0) return;
            if (e.ChangedButton == MouseButton.Left && MeasureMode == "Point")
            {
                var p = e.GetPosition(PixelCanvas);
                AddMeasure(new MeasureItem
                {
                    Tool = "Point",
                    X1 = p.X, Y1 = p.Y, X2 = p.X, Y2 = p.Y,
                    Value = 0,
                    Label = $"点 ({p.X:F0},{p.Y:F0})",
                    Color = "#30D158"
                });
                e.Handled = true;
                return;
            }
            if (e.ChangedButton == MouseButton.Left && MeasureMode == "RotatedRect")
            {
                _roiDragging = true;
                _roiStart = e.GetPosition(PixelCanvas);
                PixelCanvas.CaptureMouse();
                RoiTip.Visibility = Visibility.Visible;
                UpdateRoiFromPoint(_roiStart, _roiStart);
                e.Handled = true;
                return;
            }
            if (e.ChangedButton == MouseButton.Left)
            {
                var p = e.GetPosition(PixelCanvas); // PixelCanvas 局部坐标 = 图像像素坐标
                if (MeasureMode == "Line" || MeasureMode == "Circle")
                {
                    _measureDragging = true;
                    _measureStart = p;
                    _measureCurrent = p;
                    PixelCanvas.CaptureMouse();
                    e.Handled = true;
                    return;
                }
                _roiDragging = true;
                _roiStart = p;
                PixelCanvas.CaptureMouse();
                RoiTip.Visibility = Visibility.Visible;
                UpdateRoiFromPoint(_roiStart, _roiStart);
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_measureDragging)
            {
                _measureCurrent = e.GetPosition(PixelCanvas);
                _previewMeasure = BuildMeasure(_measureStart, _measureCurrent);
                RenderMeasures();
            }
            else if (_roiDragging)
            {
                UpdateRoiFromPoint(_roiStart, e.GetPosition(PixelCanvas));
            }
            else if (_rotating)
            {
                var c = e.GetPosition(PixelCanvas);
                double cx = RoiX + RoiW / 2, cy = RoiY + RoiH / 2;
                double ang = Math.Atan2(c.X - cx, -(c.Y - cy)) * 180.0 / Math.PI;
                RoiAngle = ang;
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _measureDragging)
            {
                _measureDragging = false;
                PixelCanvas.ReleaseMouseCapture();
                var item = BuildMeasure(_measureStart, _measureCurrent);
                _previewMeasure = null;
                AddMeasure(item);
                RenderMeasures();
            }
            else if (e.ChangedButton == MouseButton.Left && _rotating)
            {
                _rotating = false;
                PixelCanvas.ReleaseMouseCapture();
            }
            else if (e.ChangedButton == MouseButton.Left && _roiDragging)
            {
                _roiDragging = false;
                RoiTip.Visibility = Visibility.Collapsed;
                PixelCanvas.ReleaseMouseCapture();
            }
        }

        #region 测量：构建 / 添加 / 渲染

        private MeasureItem BuildMeasure(Point a, Point b)
        {
            if (MeasureMode == "Circle")
            {
                double r = System.Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                return new MeasureItem
                {
                    Tool = "Circle",
                    X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
                    Value = r,
                    Label = $"半径 {r:F1}px",
                    Color = "#FFD60A"
                };
            }
            double d = System.Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            return new MeasureItem
            {
                Tool = "Line",
                X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
                Value = d,
                Label = $"距离 {d:F1}px",
                Color = "#FFD60A"
            };
        }

        private void AddMeasure(MeasureItem item)
        {
            if (Measures is ObservableCollection<MeasureItem> oc)
            {
                oc.Add(item);
            }
            else if (Measures is IList list)
            {
                list.Add(item);
            }
            else
            {
                // 未绑定外部集合时，内部自维护一份以便渲染
                var internalList = new ObservableCollection<MeasureItem>();
                internalList.Add(item);
                Measures = internalList;
            }
            MeasurementCompleted?.Invoke(this, item);
        }

        /// <summary>完成一次测量时触发，便于宿主（如 ViewModel）读取结果。</summary>
        public event EventHandler<MeasureItem>? MeasurementCompleted;

        private void RenderMeasures()
        {
            MeasureLayer.Children.Clear();
            if (Measures == null) return;
            foreach (var m in Measures.OfType<MeasureItem>())
                DrawMeasure(m);
            if (_previewMeasure != null)
                DrawMeasure(_previewMeasure);
        }

        private void DrawMeasure(MeasureItem m)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(m.Color)!);
            if (m.Tool == "Point")
            {
                var dot = new Ellipse { Width = 10, Height = 10, Stroke = brush, StrokeThickness = 2, Fill = Brushes.Transparent };
                Canvas.SetLeft(dot, m.X1 - 5); Canvas.SetTop(dot, m.Y1 - 5);
                MeasureLayer.Children.Add(dot);
                AddLabel(m.X1, m.Y1 - 18, m.Label, brush);
                return;
            }
            if (m.Tool == "Circle")
            {
                var ellipse = new Ellipse
                {
                    Width = Math.Max(2, m.Value * 2),
                    Height = Math.Max(2, m.Value * 2),
                    Stroke = brush,
                    StrokeThickness = 2,
                };
                Canvas.SetLeft(ellipse, m.X1 - m.Value);
                Canvas.SetTop(ellipse, m.Y1 - m.Value);
                MeasureLayer.Children.Add(ellipse);

                var rline = new Line
                {
                    X1 = m.X1, Y1 = m.Y1, X2 = m.X2, Y2 = m.Y2,
                    Stroke = brush, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 3 }
                };
                MeasureLayer.Children.Add(rline);
                AddDot(m.X1, m.Y1, brush);
                AddLabel(m.X1, m.Y1 - m.Value - 16, m.Label, brush);
            }
            else
            {
                var line = new Line
                {
                    X1 = m.X1, Y1 = m.Y1, X2 = m.X2, Y2 = m.Y2,
                    Stroke = brush, StrokeThickness = 2
                };
                MeasureLayer.Children.Add(line);
                AddDot(m.X1, m.Y1, brush);
                AddDot(m.X2, m.Y2, brush);
                AddLabel((m.X1 + m.X2) / 2, (m.Y1 + m.Y2) / 2, m.Label, brush);
            }
        }

        private void AddDot(double x, double y, Brush brush)
        {
            var dot = new Ellipse { Width = 6, Height = 6, Fill = brush };
            Canvas.SetLeft(dot, x - 3);
            Canvas.SetTop(dot, y - 3);
            MeasureLayer.Children.Add(dot);
        }

        private void AddLabel(double x, double y, string text, Brush brush)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(180, 28, 28, 34)),
                Padding = new Thickness(3, 1, 3, 1)
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, x - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, y);
            Panel.SetZIndex(tb, 3);
            MeasureLayer.Children.Add(tb);
        }

        #endregion

        #region 模式切换按钮

        private void OnModeClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string mode)
                MeasureMode = mode;
        }

        private void UpdateModeButtons()
        {
            if (BtnModeRoi == null) return;
            SetBtnActive(BtnModeRoi, MeasureMode == "Roi");
            SetBtnActive(BtnModeLine, MeasureMode == "Line");
            SetBtnActive(BtnModeCircle, MeasureMode == "Circle");
            SetBtnActive(BtnModePoint, MeasureMode == "Point");
            SetBtnActive(BtnModeRot, MeasureMode == "RotatedRect");
        }

        private void OnRotateHandleDown(object sender, MouseButtonEventArgs e)
        {
            if (ImagePixelWidth <= 0) return;
            _rotating = true;
            PixelCanvas.CaptureMouse();
            e.Handled = true;
        }

        private static void SetBtnActive(Button btn, bool active)
        {
            btn.Background = active
                ? (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0A84FF")!)
                : (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C1C22")!);
        }

        #endregion

        private void UpdateRoiFromPoint(Point a, Point b)
        {
            if (ImagePixelWidth <= 0 || ImagePixelHeight <= 0) return;

            double x = Math.Max(0, Math.Min(a.X, b.X));
            double y = Math.Max(0, Math.Min(a.Y, b.Y));
            double w = Math.Max(6, Math.Abs(b.X - a.X));
            double h = Math.Max(6, Math.Abs(b.Y - a.Y));

            // 限制不超出图像边界
            x = Math.Min(x, ImagePixelWidth - 1);
            y = Math.Min(y, ImagePixelHeight - 1);
            w = Math.Min(w, ImagePixelWidth - x);
            h = Math.Min(h, ImagePixelHeight - y);

            RoiX = x; RoiY = y; RoiW = w; RoiH = h;
        }

        #endregion
    }
}
// 温启志：18719361399  混淆: 温w启a志h：l1q8n7n1h9z3v6r1w3i9p9
