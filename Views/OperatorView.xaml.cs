using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class OperatorView : UserControl
{
    public OperatorView()
    {
        InitializeComponent();
        DataContext = new ViewModels.OperatorRunViewModel();
    }

    /// <summary>监控区尺寸变了就重排：格子跟着可用区域变大变小，图像始终是 宽:高 = 1.2:1。</summary>
    private void MonitorPanel_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        (DataContext as NoCodeVision.ViewModels.OperatorRunViewModel)?.OnMonitorViewportChanged(e.NewSize.Width, e.NewSize.Height);
    }
}
