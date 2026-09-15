using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class OperatorView : UserControl
{
    public OperatorView()
    {
        InitializeComponent();
        DataContext = new ViewModels.OperatorRunViewModel();
    }
}
// 温启志：18719361399  混淆: 温b启v志e：o1m8r7a1e9v3b6n1k3f9e9
