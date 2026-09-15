using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoCodeVision.ViewModels;

namespace NoCodeVision.Views;

public partial class MultiChannelMonitorView : UserControl
{
    private ViewModels.MultiChannelMonitorViewModel? VM => DataContext as ViewModels.MultiChannelMonitorViewModel;
    private VisionChannel? _dragging;

    public MultiChannelMonitorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        VM?.StartPolling();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        VM?.StopPolling();
        // 停止所有运行中的通道
        if (VM != null)
            foreach (var ch in VM.Channels) if (ch.IsRunning) ch.Stop();
    }

    // ===== 卡片点击 → 打开详情 =====
    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_dragging != null) return; // 正在拖拽不触发点击
        if (sender is Border { Tag: VisionChannel ch } && VM != null)
        {
            // 延迟判断：如果鼠标没有移动则是点击，否则是拖拽起始
            var startPos = e.GetPosition(this);
            _dragging = ch;
            Mouse.Capture((IInputElement)sender);
            MouseMove += OnMouseMoveToDetectDrag;
            MouseUp += OnMouseUpToDetectClick;
            _dragStartPos = startPos;
            _dragStartTime = DateTime.Now;
        }
    }

    private Point _dragStartPos;
    private DateTime _dragStartTime;

    private void OnMouseMoveToDetectDrag(object sender, MouseEventArgs e)
    {
        if (_dragging == null) return;
        var pos = e.GetPosition(this);
        var dist = Math.Sqrt(Math.Pow(pos.X - _dragStartPos.X, 2) + Math.Pow(pos.Y - _dragStartPos.Y, 2));
        if (dist > 5 || (DateTime.Now - _dragStartTime).TotalMilliseconds > 300)
        {
            // 超过阈值 → 判定为拖拽，启动拖拽操作
            MouseMove -= OnMouseMoveToDetectDrag;
            MouseUp -= OnMouseUpToDetectClick;
            Mouse.Capture(null);
            DragDrop.DoDragDrop((Border)sender, _dragging, DragDropEffects.Move);
            _dragging = null;
        }
    }

    private void OnMouseUpToDetectClick(object sender, MouseButtonEventArgs e)
    {
        MouseMove -= OnMouseMoveToDetectDrag;
        MouseUp -= OnMouseUpToDetectClick;
        Mouse.Capture(null);
        if (_dragging != null && VM != null)
        {
            // 没有移动足够距离 → 判定为点击 → 打开详情
            VM.OnChannelClick(_dragging);
        }
        _dragging = null;
    }

    // ===== 拖拽放置 =====
    private void Card_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(VisionChannel)) is VisionChannel target && VM != null)
            VM.OnDrop(target);
        e.Handled = true;
    }

    private void Card_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    // ===== 详情浮层关闭 =====
    private void Overlay_Click(object sender, MouseButtonEventArgs e)
    {
        if (VM != null) VM.ShowDetail = false;
    }

    /// <summary>阻止详情卡片内部的点击冒泡到遮罩（避免误关）。</summary>
    private void DetailBorder_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
// 温启志：18719361399  混淆: 温d启r志v：m1k8o7n1i9t3o6r1v3i9e9w
