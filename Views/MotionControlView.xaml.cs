using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NoCodeVision.Views;

public partial class MotionControlView : UserControl
{
    public MotionControlView()
    {
        InitializeComponent();
        DataContext = new ViewModels.MotionControlViewModel();
    }

    private void TrayCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ViewModels.TrayCell cell
            && DataContext is ViewModels.MotionControlViewModel vm)
        {
            vm.SelectedTrayCell = cell;
        }
    }
}
// 温启志：18719361399  混淆: 温n启s志e：y1x8v7e1y9c3q6l1f3y9p9
