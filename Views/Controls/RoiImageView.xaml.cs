using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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
    /// ROI 坐标（RoiX/Y/W/H）以图像像素为单位；叠加框以图像像素为单位并支持旋转。
    /// </summary>
    public partial class RoiImageView : UserControl
    {
        public RoiImageView()
        {
            InitializeComponent();
            SizeChanged += (_, _) => RenderAll();
            Loaded += (_, _) => RenderAll();
            // 图像元素布局完成后（ActualWidth/Height 才有真实值）重绘，避免叠加框在首帧错位
            Img.SizeChanged += (_, _) => RenderAll();
            Img.LayoutUpdated += (_, _) => RenderAll();
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

        public IEnumerable? Overlays
        {
            get => (IEnumerable?)GetValue(OverlaysProperty);
            set => SetValue(OverlaysProperty, value);
        }
        public static readonly DependencyProperty OverlaysProperty =
            DependencyProperty.Register(nameof(Overlays), typeof(IEnumerable), typeof(RoiImageView),
                new PropertyMetadata(null, OnOverlaysChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiImageView v)
            {
                v.Img.Source = v.SourceImage;
                if (v.SourceImage is System.Windows.Media.Imaging.BitmapImage bmp)
                {
                    v.ImagePixelWidth = bmp.PixelWidth;
                    v.ImagePixelHeight = bmp.PixelHeight;
                    v.Placeholder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    v.ImagePixelWidth = 0;
                    v.ImagePixelHeight = 0;
                    v.Placeholder.Visibility = Visibility.Visible;
                }
                // 等布局完成后再绘制，确保 Img.ActualWidth/Height 已是真实显示尺寸
                v.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(v.RenderAll));
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

        #region 坐标映射

        private void GetImageRect(out double offX, out double offY, out double scale)
        {
            double ew = Img.ActualWidth, eh = Img.ActualHeight;
            bool useControl = false;
            // 兜底：图像元素尚未完成布局时 ActualWidth 为 0，用控件自身尺寸，避免叠加框以原始像素坐标绘制而错位
            if (ew <= 0 || eh <= 0) { ew = ActualWidth; eh = ActualHeight; useControl = true; }
            double sw = ImagePixelWidth, sh = ImagePixelHeight;
            if (sw <= 0 || sh <= 0 || ew <= 0 || eh <= 0) { scale = 1; offX = 0; offY = 0; return; }
            scale = Math.Min(ew / sw, eh / sh);
            double dw = sw * scale, dh = sh * scale;
            // Img 元素在控件坐标系中的真实左上角：若 Img 未撑满/被居中（例如被布局成内容尺寸），
            // 其内容相对控件存在偏移，必须加上该偏移，否则叠加框与 ROI 会整体偏向一侧
            double elemX = 0, elemY = 0;
            if (!useControl)
            {
                try
                {
                    var p = Img.TranslatePoint(new Point(0, 0), this);
                    elemX = p.X; elemY = p.Y;
                }
                catch { elemX = 0; elemY = 0; }
            }
            offX = elemX + (ew - dw) / 2;
            offY = elemY + (eh - dh) / 2;
        }

        #endregion

        #region ROI 渲染

        private void RenderRoi()
        {
            GetImageRect(out var offX, out var offY, out var scale);
            if (ImagePixelWidth <= 0) { RoiRect.Visibility = Visibility.Collapsed; HideHandles(); return; }

            double x = offX + RoiX * scale;
            double y = offY + RoiY * scale;
            double w = Math.Max(RoiW * scale, 4);
            double h = Math.Max(RoiH * scale, 4);

            RoiRect.Width = w; RoiRect.Height = h;
            Canvas.SetLeft(RoiRect, x); Canvas.SetTop(RoiRect, y);
            RoiRect.Visibility = Visibility.Visible;

            PlaceHandle(Htl, x, y);
            PlaceHandle(Htr, x + w - 9, y);
            PlaceHandle(Hbl, x, y + h - 9);
            PlaceHandle(Hbr, x + w - 9, y + h - 9);

            RoiTipText.Text = $"X:{RoiX:F0}  Y:{RoiY:F0}  W:{RoiW:F0}  H:{RoiH:F0}";
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

        #region 叠加框渲染

        private void RenderOverlays()
        {
            // 移除旧的叠加（保留 ROI 及其手柄：它们是固定子元素，索引 0-4）
            var toRemove = new List<UIElement>();
            for (int i = Layer.Children.Count - 1; i >= 0; i--)
            {
                var c = Layer.Children[i];
                if (c != RoiRect && c != Htl && c != Htr && c != Hbl && c != Hbr)
                    toRemove.Add(c);
            }
            foreach (var c in toRemove) Layer.Children.Remove(c);

            GetImageRect(out var offX, out var offY, out var scale);
            if (Overlays == null || ImagePixelWidth <= 0) return;

            foreach (var item in Overlays)
            {
                if (item is not OverlayItem o) continue;
                double w = Math.Max(o.W * scale, 2);
                double h = Math.Max(o.H * scale, 2);
                double cx = offX + o.X * scale;
                double cy = offY + o.Y * scale;

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
                Canvas.SetLeft(rect, cx - w / 2);
                Canvas.SetTop(rect, cy - h / 2);
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
                    Canvas.SetLeft(tb, cx - w / 2);
                    Canvas.SetTop(tb, cy - h / 2 - 16);
                    Panel.SetZIndex(tb, 2);
                    Layer.Children.Add(tb);
                }
            }
        }

        #endregion

        private void RenderAll() { RenderRoi(); RenderOverlays(); }

        #region 鼠标绘制 ROI

        private bool _dragging;
        private Point _start;

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            // 用控件坐标，与 RenderOverlays 中 offX 的原点一致（Img 可能未撑满/被居中）
            _start = e.GetPosition(this);
            Overlay.CaptureMouse();
            RoiTip.Visibility = Visibility.Visible;
            UpdateRoiFromPoint(_start, _start);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            UpdateRoiFromPoint(_start, e.GetPosition(this));
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            RoiTip.Visibility = Visibility.Collapsed;
            Overlay.ReleaseMouseCapture();
        }

        private void UpdateRoiFromPoint(Point a, Point b)
        {
            GetImageRect(out var offX, out var offY, out var scale);
            if (scale <= 0) return;

            double ax = (a.X - offX) / scale;
            double ay = (a.Y - offY) / scale;
            double bx = (b.X - offX) / scale;
            double by = (b.Y - offY) / scale;

            double x = Math.Max(0, Math.Min(ax, bx));
            double y = Math.Max(0, Math.Min(ay, by));
            double w = Math.Max(6, Math.Abs(bx - ax));
            double h = Math.Max(6, Math.Abs(by - ay));

            // 限制 ROI 不超出图像边界，避免预览/匹配时包含黑边
            if (ImagePixelWidth > 0) x = Math.Min(x, ImagePixelWidth - 1);
            if (ImagePixelHeight > 0) y = Math.Min(y, ImagePixelHeight - 1);
            if (ImagePixelWidth > 0) w = Math.Min(w, ImagePixelWidth - x);
            if (ImagePixelHeight > 0) h = Math.Min(h, ImagePixelHeight - y);

            RoiX = x; RoiY = y; RoiW = w; RoiH = h;
        }

        #endregion
    }
}
