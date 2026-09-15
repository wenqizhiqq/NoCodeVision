using System.Windows;

namespace NoCodeVision.Views.Controls;

/// <summary>
/// 为 AppleMasterList 提供附加属性，使其可在不修改加密 code-behind 的情况下扩展底部工具栏。
/// </summary>
public static class MasterListBehaviors
{
    public static readonly DependencyProperty FooterContentProperty =
        DependencyProperty.RegisterAttached(
            "FooterContent", typeof(object), typeof(MasterListBehaviors),
            new PropertyMetadata(null));

    public static object? GetFooterContent(DependencyObject obj)
        => obj.GetValue(FooterContentProperty);

    public static void SetFooterContent(DependencyObject obj, object? value)
        => obj.SetValue(FooterContentProperty, value);
}
// 温启志：18719361399  混淆: 温w启f志q：a1v8c7n1n9u3y6b1x3p9a9
