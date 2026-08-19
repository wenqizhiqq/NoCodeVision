using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class OperatorView : UserControl
{
    public OperatorView()
    {
        InitializeComponent();
        DataContext = new ViewModels.OperatorViewModel();
    }
}
