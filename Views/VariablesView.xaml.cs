using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class VariablesView : UserControl
{
    public VariablesView()
    {
        InitializeComponent();
        DataContext = new ViewModels.VariablesViewModel();
    }
}
