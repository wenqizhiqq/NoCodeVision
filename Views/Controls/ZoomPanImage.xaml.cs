using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoCodeVision.Views.Controls;

/// <summary>
/// 只读看图控件：滚轮以光标为中心缩放，中键/右键按住拖动平移，双击复位到「铺满」。
/// 用在「显示结果」这类只看的图上（要画框选区域的用 RoiImageView）。
/// </summary>
public partial class ZoomPanImage : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ImageSource), typeof(ZoomPanImage),
        new PropertyMetadata(null, OnSourceChanged));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private bool _panning;
    private Point _panFrom;
    private double _panX, _panY;
    private const double MinZoom = 0.05, MaxZoom = 20, ZoomStep = 1.2;

    public ZoomPanImage()
    {
        InitializeComponent();
        Loaded += (_, _) => Fit();
        SizeChanged += (_, _) => { if (!_panning) Fit(); };
        AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler(OnWheel), true);
        AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(OnDown), true);
        AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(OnMove), true);
        AddHandler(PreviewMouseUpEvent, new MouseButtonEventHandler(OnUp), true);
        MouseDoubleClick += (_, _) => Fit();
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var v = (ZoomPanImage)d;
        v.Pic.Source = e.NewValue as ImageSource;
        v.UpdatePixelSize();
        v.Fit();
        // 布局还没量出来时，等一帧再适配
        v.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(v.Fit));
    }

    private void UpdatePixelSize()
    {
        double w = 0, h = 0;
        if (Source is BitmapSource bs) { w = bs.PixelWidth; h = bs.PixelHeight; }
        else if (Source != null) { w = Source.Width; h = Source.Height; }
        if (w <= 0 || h <= 0) { w = Math.Max(1, Root.ActualWidth); h = Math.Max(1, Root.ActualHeight); }
        Pic.Width = w;
        Pic.Height = h;
        Host.Width = w;
        Host.Height = h;
    }

    /// <summary>铺满显示区并居中。</summary>
    private void Fit()
    {
        UpdatePixelSize();
        double vw = Root.ActualWidth, vh = Root.ActualHeight;
        double iw = Host.Width, ih = Host.Height;
        if (vw <= 0 || vh <= 0 || iw <= 0 || ih <= 0) return;
        double fit = Math.Min(vw / iw, vh / ih);
        Scale.ScaleX = Scale.ScaleY = fit;
        Move.X = (vw - iw * fit) / 2;
        Move.Y = (vh - ih * fit) / 2;
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (Source == null) return;
        var p = e.GetPosition(Root);
        double oldScale = Scale.ScaleX <= 0 ? 1 : Scale.ScaleX;
        double factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        double newScale = Math.Clamp(oldScale * factor, MinZoom, MaxZoom);
        if (Math.Abs(newScale - oldScale) < 1e-9) { e.Handled = true; return; }
        // 光标下的那一点保持不动
        Move.X = p.X - (p.X - Move.X) * (newScale / oldScale);
        Move.Y = p.Y - (p.Y - Move.Y) * (newScale / oldScale);
        Scale.ScaleX = Scale.ScaleY = newScale;
        e.Handled = true;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (Source == null) return;
        if (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right) return;
        _panning = true;
        _panFrom = e.GetPosition(Root);
        _panX = Move.X;
        _panY = Move.Y;
        Root.CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_panning) return;
        var cur = e.GetPosition(Root);
        Move.X = _panX + (cur.X - _panFrom.X);
        Move.Y = _panY + (cur.Y - _panFrom.Y);
        e.Handled = true;
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning) return;
        _panning = false;
        if (Root.IsMouseCaptured) Root.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        e.Handled = true;
    }
}
