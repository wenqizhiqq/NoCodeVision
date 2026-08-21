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
