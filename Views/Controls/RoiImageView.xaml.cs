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
    /// 采用与 GrayMatch.Wpf 一致的做法：图像以原生像素尺寸渲染在 PixelCanvas 中，
    /// 整体用 LayoutTransform（ScaleTransform）缩放适配显示区；ROI 与叠加框均在「图像像素」坐标系下绘制，
    /// 因此随 LayoutTransform 一起缩放，无需手动计算 letterbox 偏移，彻底避免叠加框整体偏移的问题。
    /// </summary>
    public partial class RoiImageView : UserControl
    {
        public RoiImageView()
        {
            InitializeComponent();
            SizeChanged += (_, _) => UpdateScale();
            Loaded += (_, _) => UpdateScale();
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

        #endregion

        #region 缩放 / 平移（LayoutTransform 统一处理）

        private double _fitScale = 1.0;   // 适配控件所需的缩放
        private double _zoomFactor = 1.0; // 用户在适配基础上的额外滚轮缩放

        private void UpdateScale()
        {
            double sw = ImagePixelWidth, sh = ImagePixelHeight;
            double availW = Root.ActualWidth, availH = Root.ActualHeight;
            if (sw <= 0 || sh <= 0 || availW <= 0 || availH <= 0) return;
            _fitScale = Math.Min(availW / sw, availH / sh);
            double s = _fitScale * _zoomFactor;
            PixelScale.ScaleX = s;
            PixelScale.ScaleY = s;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ImagePixelWidth <= 0) return;
            const double factor = 1.15;
            if (e.Delta > 0) _zoomFactor *= factor;
            else if (e.Delta < 0) _zoomFactor /= factor;
            // 限制在 0.5x ~ 10x 之间
            _zoomFactor = Math.Max(0.5, Math.Min(10.0, _zoomFactor));
            UpdateScale();
            e.Handled = true;
        }

        #endregion

        #region Source / ROI / Overlays 变更

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiImageView v)
            {
                v.Img.Source = v.SourceImage;
                if (v.SourceImage is System.Windows.Media.Imaging.BitmapImage bmp)
                {
                    v.ImagePixelWidth = bmp.PixelWidth;
                    v.ImagePixelHeight = bmp.PixelHeight;
                    // 像素画布尺寸 = 图像原生像素尺寸；显示缩放交给 LayoutTransform
                    v.PixelCanvas.Width = bmp.PixelWidth;
                    v.PixelCanvas.Height = bmp.PixelHeight;
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
                v.UpdateScale();
                v.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(v.UpdateScale));
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
            if (ImagePixelWidth <= 0) { RoiRect.Visibility = Visibility.Collapsed; HideHandles(); return; }

            double x = RoiX, y = RoiY, w = Math.Max(RoiW, 4), h = Math.Max(RoiH, 4);

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

        #region 鼠标交互：左键画 ROI，右键/中键平移，滚轮缩放

        private bool _roiDragging;
        private Point _roiStart;

        private bool _panDragging;
        private Point _panStart;
        private double _panStartX, _panStartY;

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ImagePixelWidth <= 0) return;
            if (e.ChangedButton == MouseButton.Left)
            {
                _roiDragging = true;
                _roiStart = e.GetPosition(PixelCanvas); // PixelCanvas 局部坐标 = 图像像素坐标
                PixelCanvas.CaptureMouse();
                RoiTip.Visibility = Visibility.Visible;
                UpdateRoiFromPoint(_roiStart, _roiStart);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle)
            {
                _panDragging = true;
                _panStart = e.GetPosition(Root); // 用控件坐标算平移，直观
                _panStartX = PixelPan.X;
                _panStartY = PixelPan.Y;
                PixelCanvas.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_roiDragging)
            {
                UpdateRoiFromPoint(_roiStart, e.GetPosition(PixelCanvas));
            }
            else if (_panDragging)
            {
                var cur = e.GetPosition(Root);
                double s = PixelScale.ScaleX;
                if (s > 0)
                {
                    PixelPan.X = _panStartX + (cur.X - _panStart.X) / s;
                    PixelPan.Y = _panStartY + (cur.Y - _panStart.Y) / s;
                }
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _roiDragging)
            {
                _roiDragging = false;
                RoiTip.Visibility = Visibility.Collapsed;
                PixelCanvas.ReleaseMouseCapture();
            }
            else if (_panDragging && (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle))
            {
                _panDragging = false;
                PixelCanvas.ReleaseMouseCapture();
            }
        }

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
