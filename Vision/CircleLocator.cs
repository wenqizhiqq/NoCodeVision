// 移植自 LvStudioNi/_recon 逆向成果（lvMeasure/lvBlob/lvHistogram/lvTransform/lvBaseApi），实现：温启志
using System.Linq;
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>
/// 圆目标定位器：从灰度图里找出"最圆"的连通区域，返回其质心与半径统计。
/// 流程里没有手绘 ROI 时，圆查找/圆弧查找用它来自动布卡尺环。
/// </summary>
/// <remarks>
/// 三级筛选（按示例图实证标定）：
/// <list type="number">
///   <item><b>面积下限</b> <c>W*H/1250</c>：滤掉小噪点。</item>
///   <item><b>尺寸上限</b>：轮廓包围盒任一边超过图幅 80% 的判为"暗角/夹具/图框"直接淘汰。
///     实证（<c>images/圆查找_1.png</c>）：四周暗角与画面连成一片，
///     <c>RetrievalModes.External</c> 只能取到"整幅图外框"（bbox=全图、质心=画面中心、
///     meanR=2592≈半宽），拿它布环会让半数卡尺扫出图外。
///     同图真正的目标是三块 ~300×400 的冲压件（meanR≈176），只有取到二级轮廓才看得见。</item>
///   <item><b>圆度</b>：轮廓点到质心半径的变异系数最小者胜出。</item>
/// </list>
/// </remarks>
public static class CircleLocator
{
    /// <summary>面积下限系数：轮廓面积需 ≥ W*H/1250。</summary>
    public const double MinAreaRatio = 1.0 / 1250.0;

    /// <summary>包围盒占图幅比例上限，超过则判为暗角/图框。</summary>
    public const double MaxSpanRatio = 0.8;

    /// <summary>定位结果。<see cref="RadiusLo"/>/<see cref="RadiusHi"/> 是轮廓半径的 1%/99% 分位。</summary>
    public readonly record struct Hit(double Cx, double Cy, double Radius, double Circularity,
                                      double RadiusLo = 0, double RadiusHi = 0)
    {
        public bool Valid => Circularity < 0.25 && Radius > 4;
    }

    /// <summary>
    /// Otsu 反二值化后取所有够大的轮廓，排除暗角/图框，按圆度挑最优。
    /// dark=true 表示目标比背景暗（默认，适配白底黑件）。
    /// </summary>
    public static Hit Find(Mat gray, bool dark = true)
    {
        if (gray.Empty()) return new Hit(gray.Width / 2.0, gray.Height / 2.0,
            Math.Min(gray.Width, gray.Height) * 0.2, 1);

        using var bin = new Mat();
        Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
        using var target = dark ? new Mat() : bin.Clone();
        if (dark)
        {
            Cv2.BitwiseNot(bin, target);
        }

        // 用 List 而不是 External：目标常是被亮区/暗角夹在中间的"洞中洞"，
        // External 只会返回最外层边界（= 图框），把真正的部件全丢掉。
        Cv2.FindContours(target, out var contours, out _,
            RetrievalModes.List, ContourApproximationModes.ApproxNone);

        double minArea = gray.Width * gray.Height * MinAreaRatio;   // 滤掉小噪点
        Hit best = new(gray.Width / 2.0, gray.Height / 2.0,
                       Math.Min(gray.Width, gray.Height) * 0.2, 1);

        foreach (var c in contours)
        {
            if (Math.Abs(Cv2.ContourArea(c)) < minArea) continue;

            // 尺寸上限：暗角/夹具/图框的包围盒铺满整幅图，不是零件
            var bb = Cv2.BoundingRect(c);
            if (bb.Width > gray.Width * MaxSpanRatio || bb.Height > gray.Height * MaxSpanRatio) continue;

            var m = Cv2.Moments(c);
            if (m.M00 <= 0) continue;

            var cen = new Point2d(m.M10 / m.M00, m.M01 / m.M00);
            var rs = c.Select(p => Math.Sqrt((p.X - cen.X) * (p.X - cen.X)
                                           + (p.Y - cen.Y) * (p.Y - cen.Y))).ToArray();
            if (rs.Length < 16) continue;

            double mean = rs.Average();
            if (mean < 4) continue;
            double sd = Math.Sqrt(rs.Average(x => (x - mean) * (x - mean)));
            double cv = sd / mean;                     // 变异系数：0 = 完美圆

            if (cv < best.Circularity)
            {
                var sorted = rs.OrderBy(x => x).ToArray();   // 求分位要先排序
                double lo = sorted[(int)Math.Max(0, (sorted.Length - 1) * 0.01)];
                double hi = sorted[(int)Math.Min(sorted.Length - 1, (sorted.Length - 1) * 0.99)];
                best = new Hit(cen.X, cen.Y, mean, cv, lo, hi);
            }
        }

        return best;
    }

    /// <summary>
    /// 把定位结果转成卡尺环。
    ///
    /// <para><b>环宽必须覆盖轮廓半径的真实跨度，而不是"均值 ± 固定半宽"。</b>
    /// 实证（<c>Samples/images/4.png</c>：白色圆角板 + 椭圆孔）：
    /// Otsu→轮廓给出的质心 (1360.12, 963.81) 完全正确，
    /// 但边界半径随角度在 <b>372~538</b> 之间波动（非圆目标），
    /// 旧的 <c>meanR±30 = [408.8, 468.8]</c> 只覆盖 <b>11/32</b> 个卡尺方向，
    /// 于是 32 条卡尺只有 13 条能落到真正的边上 —— 有效点骤降是<b>环放置</b>的问题，
    /// 不是卡尺/找边算法的问题（已用 Python 独立复算逐位比对证实：
    /// 梯度 25~93、边界半径逐角度列表完全对得上）。</para>
    ///
    /// <para>改用 <c>[p01, p99]</c> 跨度并兜底 <paramref name="halfWidth"/>：
    /// 同图环变成 [374.4, 535.4]，覆盖 <b>32/32</b>，
    /// 自检实测 <c>有效点=32/32 (100%)</c>、拟合圆心与参考仅差 0.2 px、半径差 2.1 px。</para>
    /// </summary>
    public static DvRing ToRing(Hit h, double halfWidth = 30)
    {
        double up = Math.Max(halfWidth, h.RadiusHi - h.Radius);
        double dn = Math.Max(halfWidth, h.Radius - h.RadiusLo);
        return DvRing.FromCenterR(new DvPoint2D(h.Cx, h.Cy),
                                  h.Radius + up,
                                  Math.Max(1, h.Radius - dn));
    }
}
