using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class CommunicationView : UserControl
{
    public CommunicationView()
    {
        InitializeComponent();
        DataContext = new ViewModels.CommunicationViewModel();
    }
}
