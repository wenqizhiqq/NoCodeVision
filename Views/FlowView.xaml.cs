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
