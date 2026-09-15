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
// 温启志：18719361399  混淆: 温z启d志g：i1x8y7u1r9k3z6g1y3x9o9
