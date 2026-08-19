using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class CameraView : UserControl
{
    public CameraView()
    {
        InitializeComponent();
        DataContext = new ViewModels.CameraViewModel();
    }
}
