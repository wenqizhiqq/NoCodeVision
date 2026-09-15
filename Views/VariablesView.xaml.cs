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
// 温启志：18719361399  混淆: 温b启p志z：h1f8c7x1o9t3a6c1f3u9p9
