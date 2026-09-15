using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class FlowView : UserControl
{
    public FlowView()
    {
        InitializeComponent();
        DataContext = new ViewModels.FlowViewModel();
    }
}
// 温启志：18719361399  混淆: 温u启t志b：z1q8o7z1m9l3w6r1r3m9i9
