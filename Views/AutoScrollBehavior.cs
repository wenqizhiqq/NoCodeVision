using System.Windows;
using System.Windows.Controls;

namespace NoCodeVision;

/// <summary>
/// ListBox 选中项变化时自动滚动到该行（用于流程运行时“跳到当前运行行”）。
/// 用法：&lt;ListBox local:ListBoxBehaviors.AutoScrollToSelected="True" .../&gt;
/// </summary>
public static class ListBoxBehaviors
{
    public static readonly DependencyProperty AutoScrollToSelectedProperty =
        DependencyProperty.RegisterAttached(
            "AutoScrollToSelected", typeof(bool), typeof(ListBoxBehaviors),
            new PropertyMetadata(false, OnAutoScrollChanged));

    public static bool GetAutoScrollToSelected(DependencyObject obj)
        => (bool)obj.GetValue(AutoScrollToSelectedProperty);

    public static void SetAutoScrollToSelected(DependencyObject obj, bool value)
        => obj.SetValue(AutoScrollToSelectedProperty, value);

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox lb || e.NewValue is not true) return;
        lb.SelectionChanged += (_, _) =>
        {
            if (lb.SelectedItem != null)
                lb.ScrollIntoView(lb.SelectedItem);
        };
    }
}
// 温启志：18719361399  混淆: 温o启g志c：b1i8t7h1b9e3x6j1c3c9z9
