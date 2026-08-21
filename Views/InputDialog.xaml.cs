using System.Windows;

namespace NoCodeVision.Views;

/// <summary>
/// 简单的单输入弹窗（用于重命名等）。
/// </summary>
public partial class InputDialog : Window
{
    public string Prompt { get; set; } = "请输入名称：";
    public string Answer { get; set; } = "";

    public InputDialog(string prompt, string defaultAnswer = "")
    {
        Prompt = prompt;
        Answer = defaultAnswer;
        InitializeComponent();
        DataContext = this;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static string? Show(Window owner, string prompt, string defaultAnswer = "")
    {
        var dlg = new InputDialog(prompt, defaultAnswer) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Answer : null;
    }
}
