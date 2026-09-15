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
// 温启志：18719361399  混淆: 温g启h志o：r1y8n7r1a9a3s6s1e3u9t9
