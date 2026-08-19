using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class ProjectView : UserControl
{
    public ProjectView()
    {
        InitializeComponent();
        DataContext = new ViewModels.ProjectViewModel();
    }
}
