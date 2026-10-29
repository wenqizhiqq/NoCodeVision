// 平移 + 旋转标定的共享结果：像素当量、轴夹角、旋转中心。
// 实现：温启志

using System;

namespace NoCodeVision.Vision;

/// <summary>
/// 「九点平移标定」和「五点旋转标定」算完之后都写到这里，「对位换算(XYR)」从这里取。
/// 单位：像素当量 = 毫米/像素；角度 = 度；坐标 = 像素（旋转中心另有毫米坐标）。
/// </summary>
public static class StageCalibration
{
    /// <summary>X 方向每像素多少毫米（0 = 还没标定）。</summary>
    public static double MmPerPxX { get; private set; }

    /// <summary>Y 方向每像素多少毫米。</summary>
    public static double MmPerPxY { get; private set; }

    /// <summary>机台 X 轴相对图像 X 轴转了多少度（逆时针为正）。</summary>
    public static double AxisAngleDeg { get; private set; }

    /// <summary>旋转中心（像素）。</summary>
    public static double RotCenterPx { get; private set; }
    public static double RotCenterPy { get; private set; }

    /// <summary>旋转中心到 mark 的半径（像素）。</summary>
    public static double RotRadiusPx { get; private set; }

    /// <summary>旋转中心的机台坐标（毫米）。</summary>
    public static double RotCenterWorldX { get; private set; }
    public static double RotCenterWorldY { get; private set; }

    /// <summary>转 1 度，mark 在机台上走多少毫米（半径 × 当量 × π/180）。</summary>
    public static double MmPerDeg => RotRadiusPx * (MmPerPxX + MmPerPxY) * 0.5 * Math.PI / 180.0;

    public static bool HasXY => MmPerPxX > 0 && MmPerPxY > 0;

    public static bool HasRot => RotRadiusPx > 0;

    public static void SetXY(double mmPerPxX, double mmPerPxY, double axisDeg)
    {
        MmPerPxX = mmPerPxX;
        MmPerPxY = mmPerPxY;
        AxisAngleDeg = axisDeg;
    }

    public static void SetRot(double cx, double cy, double radiusPx, double worldX, double worldY)
    {
        RotCenterPx = cx;
        RotCenterPy = cy;
        RotRadiusPx = radiusPx;
        RotCenterWorldX = worldX;
        RotCenterWorldY = worldY;
    }

    public static void Reset()
    {
        MmPerPxX = 0; MmPerPxY = 0; AxisAngleDeg = 0;
        RotCenterPx = 0; RotCenterPy = 0; RotRadiusPx = 0;
        RotCenterWorldX = 0; RotCenterWorldY = 0;
    }
}
