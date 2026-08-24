using System.Windows;

namespace NoCodeVision.Views.Controls;

/// <summary>
/// 交互式测量结果：在图像像素坐标系下绘制线段或圆，并记录测量值。
/// </summary>
public class MeasureItem
{
    /// <summary>工具类型：Line=线段/直线距离，Circle=圆（取半径）。</summary>
    public string Tool { get; set; } = "Line";

    // 线段：起点(X1,Y1) → 终点(X2,Y2)；圆：圆心(X1,Y1)，边缘点(X2,Y2)
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }

    /// <summary>测量结果：线段为长度(px)，圆为半径(px)。</summary>
    public double Value { get; set; }

    /// <summary>展示文本，例如「距离 123.4px」「半径 56.7px」。</summary>
    public string Label { get; set; } = "";

    /// <summary>线条/文字颜色（十六进制，如 #FFD60A）。</summary>
    public string Color { get; set; } = "#FFD60A";
}
