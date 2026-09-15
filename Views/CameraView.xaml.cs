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
// 温启志：18719361399  混淆: 温s启m志m：y1c8e7d1w9v3x6n1d3h9s9
