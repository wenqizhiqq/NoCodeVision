using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class EngineerView : UserControl
{
    public EngineerView()
    {
        InitializeComponent();
        DataContext = new ViewModels.EngineerViewModel();
    }
}
