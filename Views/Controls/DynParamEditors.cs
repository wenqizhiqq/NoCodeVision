// 参数面板的动态绑定：把算子目录里的属性名在运行时变成真正的 Binding。
// 实现：温启志

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace NoCodeVision.Views.Controls;

/// <summary>
/// 用法：&lt;TextBox local:DynBind.Target="Text" local:DynBind.Path="SelectedStep.RoiX" /&gt;
/// 控件本身不带 Text 绑定，由这两个附加属性在运行时挂上去，路径相对于所在 UserControl 的 DataContext。
/// </summary>
public static class DynBind
{
    public static string GetTarget(DependencyObject d) => (string)d.GetValue(TargetProperty);
    public static void SetTarget(DependencyObject d, string v) => d.SetValue(TargetProperty, v);

    public static string GetPath(DependencyObject d) => (string)d.GetValue(PathProperty);
    public static void SetPath(DependencyObject d, string v) => d.SetValue(PathProperty, v);

    /// <summary>参数名挂在哪个对象上（默认当前选中步骤），绑定路径 = Root + "." + Path。</summary>
    public static string GetRoot(DependencyObject d) => (string)d.GetValue(RootProperty);
    public static void SetRoot(DependencyObject d, string v) => d.SetValue(RootProperty, v);

    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.RegisterAttached("Target", typeof(string), typeof(DynBind), new PropertyMetadata("", Apply));

    public static readonly DependencyProperty PathProperty =
        DependencyProperty.RegisterAttached("Path", typeof(string), typeof(DynBind), new PropertyMetadata("", Apply));

    public static readonly DependencyProperty RootProperty =
        DependencyProperty.RegisterAttached("Root", typeof(string), typeof(DynBind), new PropertyMetadata("SelectedStep", Apply));

    static DependencyProperty? Resolve(string target) => target switch
    {
        "Text" => TextBox.TextProperty,
        "Checked" => ToggleButton.IsCheckedProperty,
        "Selected" => Selector.SelectedItemProperty,
        _ => null
    };

    static void Apply(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;
        if (fe.IsLoaded) Do(fe);
        else fe.Loaded += (s, _) => Do((FrameworkElement)s);
    }

    static void Do(FrameworkElement fe)
    {
        var dp = Resolve(GetTarget(fe));
        var path = GetPath(fe);
        if (dp == null || string.IsNullOrEmpty(path)) return;
        if (BindingOperations.GetBinding(fe, dp) != null) return;   // 已经挂过，别重复

        var root = GetRoot(fe);

        fe.SetBinding(dp, new Binding("DataContext." + (string.IsNullOrEmpty(root) ? path : root + "." + path))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UserControl), 1),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = dp == TextBox.TextProperty ? UpdateSourceTrigger.PropertyChanged : UpdateSourceTrigger.Default
        });
    }
}

/// <summary>参数类型 → 该不该显示这个编辑控件。</summary>
public sealed class KindToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kind = value?.ToString();
        var want = parameter as string;
        if (kind == null || string.IsNullOrEmpty(want)) return Visibility.Collapsed;
        foreach (var w in want.Split(','))
            if (string.Equals(kind, w.Trim(), StringComparison.Ordinal)) return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>带单位的数字显示（只影响显示，不改存储）。</summary>
public sealed class UnitSuffixConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var unit = values.Length > 1 ? values[1] as string : "";
        return string.IsNullOrEmpty(unit) ? values[0]?.ToString() ?? "" : $"{values[0]} {unit}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
