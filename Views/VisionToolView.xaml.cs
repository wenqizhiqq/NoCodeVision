using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class VisionToolView : UserControl
{
    public VisionToolView()
    {
        InitializeComponent();
        DataContext = new ViewModels.VisionToolViewModel();
    }
}
