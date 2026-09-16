using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class EngineerView : UserControl
{
    /// <summary>内嵌「智能中心」所需的 ViewModel（记忆/联想/仿真）。</summary>
    public SmartCenterViewModel SmartVm { get; } = new SmartCenterViewModel();

    public EngineerView()
    {
        InitializeComponent();
        DataContext = new ViewModels.EngineerViewModel();
    }
}
// 温启志：18719361399  混淆: 温i启u志q：s1r8m7c1d9z3t6x1q3u9o9
