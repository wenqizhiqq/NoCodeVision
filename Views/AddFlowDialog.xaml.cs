using System.Collections.Generic;
using System.Windows;

namespace NoCodeVision.Views;

/// <summary>新建流程时选模板：空白 or 从示例改。</summary>
public partial class AddFlowDialog : Window
{
    public sealed class Option
    {
        public string Key { get; init; } = "empty";
        public string Title { get; init; } = "";
        public string Desc { get; init; } = "";
    }

    public string SelectedKey { get; private set; } = "empty";

    /// <summary>输入框里的流程名称（空的话调用方用默认名）。</summary>
    public string FlowName => NameBox.Text?.Trim() ?? "";

    /// <summary>
    /// items = 这个按钮对应的示例清单（不同按钮不一样）；传 null 表示只改名、不选模板。
    /// </summary>
    public AddFlowDialog(string title, string defaultName, List<Option>? items)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        NameBox.Text = defaultName;

        if (items == null || items.Count == 0)
        {
            // 只改名的场景：把模板清单收起来，名称框顶上去（没清单就别占那么大）
            TemplateList.Visibility = System.Windows.Visibility.Collapsed;
            HintText.Text = "改个名就行，回车或点「确定」生效。";
            NameLabel.Text = "新名称";
            Height = 260;
        }
        else
        {
            TemplateList.ItemsSource = items;
            TemplateList.SelectedIndex = 0;
            // 清单给足高度（一屏能看全所有示例），但别超出屏幕
            double maxH = SystemParameters.WorkArea.Height - 70;
            if (Height > maxH) Height = maxH;
        }

        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateList.SelectedItem is Option o) SelectedKey = o.Key;
        if (string.IsNullOrWhiteSpace(NameBox.Text)) NameBox.Text = "未命名流程";
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}