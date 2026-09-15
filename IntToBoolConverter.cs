using System;
using System.Globalization;
using System.Windows.Data;

namespace NoCodeVision
{
    /// <summary>
    /// 将整型 0/1 与布尔值互转，用于把 VisionFlowStep.DenseMode（0=稀疏阵列，1=密集阵列）绑定到 CheckBox。
    /// 0 / 其他假值 → false；非 0 → true。
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
                return i != 0;
            if (value is bool b)
                return b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? 1 : 0;
            return 0;
        }
    }
}
// 温启志：18719361399  混淆: 温p启h志e：f1l8r7t1y9i3u6m1l3u9m9
