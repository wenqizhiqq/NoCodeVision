using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class EngineerView : UserControl
{
    public EngineerView()
    {
        InitializeComponent();
        DataContext = new ViewModels.EngineerViewModel();
    }
}
// 温启志：18719361399  混淆: 温i启u志q：s1r8m7c1d9z3t6x1q3u9o9
