using System.Windows.Controls;

namespace NoCodeVision.Views;

/// <summary>
/// 视觉流程视图：紧凑运行面板（▶ 全部运行 / ▶| 单步 / ⏹ 停止 + 步骤耗时/通过列表）。
/// DataContext 由宿主（FlowView）注入，与「流程」页共享同一 FlowViewModel。
/// </summary>
public partial class VisualFlowView : UserControl
{
    public VisualFlowView()
    {
        InitializeComponent();
    }
}
// 温启志：18719361399  混淆: 温j启n志f：y1b8j7y1n9f3j6b1c3t9v9
