// 输入框空着时显示在框内的灰字提示：写清楚这里要填什么。
// 实现：温启志

using System.Windows;

namespace NoCodeVision.Views.Controls;

/// <summary>用法：&lt;TextBox ctrl:Hint.Text="比如 192.168.1.10" /&gt;，配合 AppleTextField 模板显示。</summary>
public static class Hint
{
    public static string GetText(DependencyObject d) => (string)d.GetValue(TextProperty);

    public static void SetText(DependencyObject d, string v) => d.SetValue(TextProperty, v);

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(Hint), new PropertyMetadata(""));
}
