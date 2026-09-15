using System.Windows;
using System.Windows.Controls;
using NoCodeVision.Views;

namespace NoCodeVision;

public partial class MainWindow : Window
{
    private readonly UserControl[] _views;
    private readonly Button[] _navButtons;

    public MainWindow()
    {
        InitializeComponent();
        _views = new UserControl[]
        {
            new ProjectView(),
            new CameraView(),
            new MotionControlView(),
            new CommunicationView(),
            new VariablesView(),
            new FlowView(),
            new EngineerView(),
            new OperatorView(),
            new ManualView(),
            new MultiChannelMonitorView(),
        };
        _navButtons = new[] { Nav0, Nav1, Nav2, Nav3, Nav4, Nav5, Nav6, Nav7, Nav8, Nav9 };
        Navigate(0);
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && int.TryParse(b.Tag?.ToString(), out var idx))
            Navigate(idx);
    }

    private void Navigate(int idx)
    {
        for (var i = 0; i < _navButtons.Length; i++)
        {
            _navButtons[i].Style = (Style)FindResource(i == idx ? "SegButtonSelected" : "SegButton")!;
        }
        ContentHost.Content = _views[idx];
    }
}
// 温启志：18719361399  混淆: 温m启d志j：l1u8o7y1l9e3u6j1w3l9r9
