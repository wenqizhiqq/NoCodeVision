using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NoCodeVision.Views.Controls
{
    public partial class RoiCanvas : UserControl
    {
        public RoiCanvas()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
        }

        #region DependencyProperties

        public double RoiX
        {
            get => (double)GetValue(RoiXProperty);
            set => SetValue(RoiXProperty, value);
        }
        public static readonly DependencyProperty RoiXProperty =
            DependencyProperty.Register(nameof(RoiX), typeof(double), typeof(RoiCanvas),
                new PropertyMetadata(10.0, OnRoiChanged));

        public double RoiY
        {
            get => (double)GetValue(RoiYProperty);
            set => SetValue(RoiYProperty, value);
        }
        public static readonly DependencyProperty RoiYProperty =
            DependencyProperty.Register(nameof(RoiY), typeof(double), typeof(RoiCanvas),
                new PropertyMetadata(10.0, OnRoiChanged));

        public double RoiW
        {
            get => (double)GetValue(RoiWProperty);
            set => SetValue(RoiWProperty, value);
        }
        public static readonly DependencyProperty RoiWProperty =
            DependencyProperty.Register(nameof(RoiW), typeof(double), typeof(RoiCanvas),
                new PropertyMetadata(120.0, OnRoiChanged));

        public double RoiH
        {
            get => (double)GetValue(RoiHProperty);
            set => SetValue(RoiHProperty, value);
        }
        public static readonly DependencyProperty RoiHProperty =
            DependencyProperty.Register(nameof(RoiH), typeof(double), typeof(RoiCanvas),
                new PropertyMetadata(80.0, OnRoiChanged));

        public string ImageSource
        {
            get => (string)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(string), typeof(RoiCanvas),
                new PropertyMetadata(null, OnImageSourceChanged));

        private static void OnRoiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiCanvas canvas) canvas.RenderRoi();
        }

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoiCanvas canvas) canvas.UpdateImage();
        }

        #endregion

        private Point _start;
        private bool _isDragging;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderRoi();
        }

        private void UpdateImage()
        {
            if (!string.IsNullOrWhiteSpace(ImageSource))
            {
                try
                {
                    PreviewImage.Source = new ImageSourceConverter().ConvertFromString(ImageSource) as ImageSource;
                    PreviewImage.Visibility = Visibility.Visible;
                    PlaceholderText.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    PreviewImage.Visibility = Visibility.Collapsed;
                    PlaceholderText.Visibility = Visibility.Visible;
                }
            }
            else
            {
                PreviewImage.Visibility = Visibility.Collapsed;
                PlaceholderText.Visibility = Visibility.Visible;
            }
        }

        private void RenderRoi()
        {
            if (RoiLayer == null) return;

            var w = Math.Max(RoiLayer.ActualWidth, 1);
            var h = Math.Max(RoiLayer.ActualHeight, 1);

            // 将 view-model 的 ROI 坐标（像素）映射到当前画布尺寸
            double sx = w / 640.0;
            double sy = h / 480.0;

            double x = RoiX * sx;
            double y = RoiY * sy;
            double rw = Math.Max(RoiW * sx, 4);
            double rh = Math.Max(RoiH * sy, 4);

            RoiRect.Width = rw;
            RoiRect.Height = rh;
            Canvas.SetLeft(RoiRect, x);
            Canvas.SetTop(RoiRect, y);
            RoiRect.Visibility = Visibility.Visible;

            PositionHandle(HandleTL, x, y);
            PositionHandle(HandleTR, x + rw - 8, y);
            PositionHandle(HandleBL, x, y + rh - 8);
            PositionHandle(HandleBR, x + rw - 8, y + rh - 8);

            CoordText.Text = $"X:{RoiX:F0} Y:{RoiY:F0}\nW:{RoiW:F0} H:{RoiH:F0}";
        }

        private static void PositionHandle(Rectangle handle, double x, double y)
        {
            Canvas.SetLeft(handle, x);
            Canvas.SetTop(handle, y);
            handle.Visibility = Visibility.Visible;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _start = e.GetPosition(RoiLayer);
            CoordTip.Visibility = Visibility.Visible;
            RoiLayer.CaptureMouse();
            UpdateRoiFromMouse(_start, _start);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            var pos = e.GetPosition(RoiLayer);
            UpdateRoiFromMouse(_start, pos);
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            CoordTip.Visibility = Visibility.Collapsed;
            RoiLayer.ReleaseMouseCapture();
        }

        private void UpdateRoiFromMouse(Point a, Point b)
        {
            var w = Math.Max(RoiLayer.ActualWidth, 1);
            var h = Math.Max(RoiLayer.ActualHeight, 1);

            double sx = 640.0 / w;
            double sy = 480.0 / h;

            double x = Math.Min(a.X, b.X) * sx;
            double y = Math.Min(a.Y, b.Y) * sy;
            double rw = Math.Abs(b.X - a.X) * sx;
            double rh = Math.Abs(b.Y - a.Y) * sy;

            // 避免绑定循环：直接设置 DP，会触发 RenderRoi
            RoiX = Math.Max(0, x);
            RoiY = Math.Max(0, y);
            RoiW = Math.Max(8, rw);
            RoiH = Math.Max(8, rh);

            CoordTip.Visibility = Visibility.Visible;
        }
    }
}
