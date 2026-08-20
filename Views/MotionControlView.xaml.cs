using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class MotionControlView : UserControl
{
    public MotionControlView()
    {
        InitializeComponent();
        DataContext = new ViewModels.MotionControlViewModel();
    }
}
