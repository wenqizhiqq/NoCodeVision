// 空状态提示用的值转换器：把 SelectedFlow 转为「无流程 / 流程无步骤」提示的可见性。
// 作者：温启志 编写 ▢ 联系 wx:187-1936-1399
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NoCodeVision.ViewModels;

namespace NoCodeVision;

/// <summary>对象为 null → Visible；否则 Collapsed。用于「未选中流程」提示。</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>选中流程但步骤为 0 → Visible；否则 Collapsed。用于「流程无步骤」提示。</summary>
public sealed class FlowEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is VisionFlow vf && vf.Steps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 步骤 StatusText → "✓"（通过/成功/合格等）或 "✗"（失败/错误/超差等）或 ""（未开始/无结果）。
/// 用于视觉流程运行列表的通过标记列。
/// </summary>
public sealed class StatusToMarkConverter : IValueConverter
{
    private static readonly string[] FailKeys = { "失败", "错误", "超差", "超时", "中断", "异常", "不通过", "NG" };
    private static readonly string[] PassKeys = { "成功", "合格", "通过", "正常", "完成", "OK", "Pass" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = (value as string)?.Trim() ?? string.Empty;
        if (s.Length == 0 || s == "未开始") return string.Empty;
        foreach (var k in FailKeys) if (s.Contains(k)) return "✗";
        foreach (var k in PassKeys) if (s.Contains(k, StringComparison.OrdinalIgnoreCase)) return "✓";
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>步骤 StatusText → ✓ 绿 / ✗ 红 / 其余透明。与 StatusToMarkConverter 配套。</summary>
public sealed class StatusToMarkBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush PassBrush = Frozen("#34C759");
    private static readonly SolidColorBrush FailBrush = Frozen("#FF3B30");
    private static readonly SolidColorBrush NoneBrush = Brushes.Transparent;

    private static SolidColorBrush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var mark = new StatusToMarkConverter().Convert(value, targetType, parameter, culture) as string ?? string.Empty;
        return mark == "✓" ? PassBrush : mark == "✗" ? FailBrush : NoneBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
// 温启志：18719361399  混淆: 温f启r志a：d1n8e7w1v9t3y6o1c3x9r9
