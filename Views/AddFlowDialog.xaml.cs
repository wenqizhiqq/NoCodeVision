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

    public AddFlowDialog(string title = "新建流程")
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;

        var items = new List<Option>
        {
            new()
            {
                Key = "empty",
                Title = "空白流程",
                Desc = "不放任何步骤，自己从右侧「工具箱」一步步加。",
            },
            new()
            {
                Key = "bottle",
                Title = "示例：瓶底线圈角度",
                Desc = "拍张照片 → 找圆定中心 → 去掉红圈 → 数块找黑色线圈 → 求角度 → 写 Modbus → 判角度 → 显示结果（8 步，图片在「瓶底线圈角度」工程文件夹里）",
            },
            new()
            {
                Key = "zeroshot",
                Title = "示例：零样本查瑕疵",
                Desc = "拍张照片 → 零样本查瑕疵（和标准图比差异，不用训练）→ 判个数 → 显示结果（4 步）",
            },
        };
        TemplateList.ItemsSource = items;
        TemplateList.SelectedIndex = 0;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateList.SelectedItem is Option o) SelectedKey = o.Key;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}