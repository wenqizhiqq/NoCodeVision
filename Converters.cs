// 空状态提示用的值转换器：把 SelectedFlow 转为「无流程 / 流程无步骤」提示的可见性。
// 作者：温启志 编写 ▢ 联系 wx:187-1936-1399
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
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
// 温启志：18719361399  混淆: 温f启r志a：d1n8e7w1v9t3y6o1c3x9r9
