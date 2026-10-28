// 空状态提示用的值转换器：把 SelectedFlow 转为「无流程 / 流程无步骤」提示的可见性。
// 作者：温启志 编写 ▢ 联系 wx:187-1936-1399
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NoCodeVision.ViewModels;

namespace NoCodeVision;

/// <summary>路径显示：bin 目录下的显示成相对路径；手填相对路径也能还原成绝对（两向都管）。</summary>
public sealed class BinPathConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Helpers.AppPaths.ToStoredPath(value as string ?? "");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Helpers.AppPaths.ResolvePath(value as string ?? "");
}

/// <summary>宽度 × 比例 → 高度：把图片区钉成固定显示比例（默认 0.7）。</summary>
public sealed class WidthToHeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double w = value is double d ? d : 0;
        double ratio = double.TryParse(parameter as string, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0.7;
        return w <= 0 ? 0d : w * ratio;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>按图片自身宽高比算显示高度：图片宽度占满，高度跟着图片比例走。
/// 比例带上下限（0.45~1.45），免得线扫那种超长图把页面撑爆。</summary>
public sealed class AspectHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        double w = Num(values, 0);
        double iw = Num(values, 1);
        double ih = Num(values, 2);
        if (w <= 0) return 0d;
        if (iw <= 0 || ih <= 0) return w * 0.7;
        return w * Math.Clamp(ih / iw, 0.45, 1.45);
    }

    // BitmapSource.PixelWidth/PixelHeight 是 int，老写法只认 double，竖图一律退化成 0.7 倍宽度
    static double Num(object[] values, int i)
    {
        if (values.Length <= i) return 0;
        return values[i] switch { double d => d, int n => n, float f => f, _ => 0 };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

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
