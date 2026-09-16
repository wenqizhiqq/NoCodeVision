using System;
using System.Windows;
using System.Windows.Controls;
using NoCodeVision.Helpers;
using NoCodeVision.Views;

namespace NoCodeVision;

public partial class MainWindow : Window
{
    private readonly UserControl[] _views;
    private readonly Button[] _navButtons;
    private static readonly string[] PageNames = { "项目", "相机", "运控", "通讯", "变量", "流程", "工程师", "操作员", "说明书" };

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
        };
        _navButtons = new[] { Nav0, Nav1, Nav2, Nav3, Nav4, Nav5, Nav6, Nav7, Nav8 };
        UiState.Saved += OnUiStateSaved;
        // 恢复上次选中的导航页（记录在 bin\Data\ui_state.json）
        Navigate(Math.Clamp(UiState.GetInt("MainWindow.SelectedNav", 0), 0, _views.Length - 1));
        UpdateSaveStatus();
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
        PageNameText.Text = "当前：" + PageNames[idx];
        UiState.SetInt("MainWindow.SelectedNav", idx);
    }

    private void OnUiStateSaved()
    {
        // 程序退出/Dispatcher 关闭过程中挂起的操作会被取消（TaskCanceledException），直接忽略
        var d = Dispatcher;
        if (d == null || d.HasShutdownStarted || d.HasShutdownFinished) return;
        try
        {
            if (d.CheckAccess()) UpdateSaveStatus();
            else d.Invoke(UpdateSaveStatus);
        }
        catch { /* 关闭过程中的取消/中止，忽略 */ }
    }

    private void UpdateSaveStatus()
    {
        if (UiState.LastSaveTime == DateTime.MinValue)
            SaveStatusText.Text = "配置自动保存中（首次保存稍后触发）";
        else
            SaveStatusText.Text = "已自动保存 · " + UiState.LastSaveTime.ToString("HH:mm:ss");
    }
}
// 温启志：18719361399  混淆: 温m启d志j：l1u8o7y1l9e3u6j1w3l9r9
