using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>一个可疑缺陷：外接框 + 面积 + 最亮（差异最大）强度。</summary>
public sealed record ZsDefect(double X, double Y, double W, double H, double Area, double Peak);

/// <summary>零样本查瑕疵的参数。</summary>
public sealed class ZeroShotParam
{
    /// <summary>0=不配准，1=只补平移，2=平移+旋转。</summary>
    public int Register { get; set; } = 1;
    /// <summary>灵敏度 0~100，越大挑得越细。</summary>
    public double Sensitivity { get; set; } = 60;
    /// <summary>小于这个面积的差异当噪点扔掉。</summary>
    public double MinArea { get; set; } = 30;
    /// <summary>平滑核（奇数），用来压噪声。</summary>
    public int Smooth { get; set; } = 3;
    /// <summary>四周忽略的边带比例（0.012 = 1.2%）。</summary>
    public double EdgeBand { get; set; } = 0.012;
}

/// <summary>
/// 零样本查瑕疵：只给一张标准（OK）图，不训练。
/// 流程：配准（相位相关补平移 / ECC 再补旋转）→ 亮度归一 → 差分 → 阈值 → 连通域。
/// 纯 OpenCV，不依赖 WPF，方便单独跑测试。
/// </summary>
public static class ZeroShot
{
    public static (List<ZsDefect> defects, Mat diff, double shiftX, double shiftY, double angleDeg, string note)
        Run(Mat current, Mat golden, ZeroShotParam p)
    {
        var defects = new List<ZsDefect>();
        var diffOut = new Mat();
        if (current == null || current.Empty() || golden == null || golden.Empty())
            return (defects, diffOut, 0, 0, 0, "没有图");

        using var curGray = ToGray(current);
        using var goldGray0 = ToGray(golden);

        Mat gold = goldGray0;
        bool ownGold = false;
        if (goldGray0.Size() != curGray.Size())
        {
            gold = new Mat();
            Cv2.Resize(goldGray0, gold, curGray.Size(), 0, 0, InterpolationFlags.Area);
            ownGold = true;
        }

        double dx = 0, dy = 0, ang = 0;
        string note = "";
        try
        {
            using var aligned = new Mat();
            if (p.Register == 0)
            {
                gold.CopyTo(aligned);
                note = "不配准";
            }
            else
            {
                // ① 相位相关补平移
                using var f1 = new Mat();
                using var f2 = new Mat();
                curGray.ConvertTo(f1, MatType.CV_32F);
                gold.ConvertTo(f2, MatType.CV_32F);
                var shift = Cv2.PhaseCorrelate(f1, f2, new Mat(), out double response);
                dx = -shift.X; dy = -shift.Y;      // 取反才是“把标准图搬到当前图”的平移量

                if (p.Register == 2)
                {
                    // ② ECC 把旋转也补上（用平移结果当初值）
                    using var warp = new Mat(2, 3, MatType.CV_32F, Scalar.All(0));
                    warp.Set(0, 0, 1.0f); warp.Set(1, 1, 1.0f);
                    warp.Set(0, 2, (float)dx); warp.Set(1, 2, (float)dy);
                    bool eccOk = false;
                    try
                    {
                        var cc = new TermCriteria(CriteriaTypes.Count | CriteriaTypes.Eps, 120, 1e-5);
                        Cv2.FindTransformECC(curGray, gold, warp, MotionTypes.Euclidean, cc, new Mat(), 5);
                        Cv2.WarpAffine(gold, aligned, warp, gold.Size(),
                            InterpolationFlags.Linear | InterpolationFlags.WarpInverseMap, BorderTypes.Replicate);
                        ang = Math.Atan2(warp.Get<float>(1, 0), warp.Get<float>(0, 0)) * 180 / Math.PI;
                        eccOk = true;
                    }
                    catch { }
                    if (!eccOk)
                    {
                        Cv2.WarpAffine(gold, aligned, Trans(dx, dy), gold.Size(),
                            InterpolationFlags.Linear, BorderTypes.Replicate);
                        note = $"平移({dx:F1},{dy:F1})，旋转没拟合上";
                    }
                    else note = $"平移({dx:F1},{dy:F1}) 旋转{ang:F2}°";
                }
                else
                {
                    Cv2.WarpAffine(gold, aligned, Trans(dx, dy), gold.Size(),
                        InterpolationFlags.Linear, BorderTypes.Replicate);
                    note = $"平移({dx:F1},{dy:F1}) 相关度{response:F2}";
                }
            }

            // ③ 亮度归一：让标准图的均值和方差跟当前图一致（压住整体明暗漂移造成的满屏误报）
            Cv2.MeanStdDev(aligned, out var agm, out var ags);
            Cv2.MeanStdDev(curGray, out var ccm, out var ccs);
            double gain = ags.Val0 > 1e-6 ? Math.Clamp(ccs.Val0 / ags.Val0, 0.5, 2.0) : 1.0;
            double offset = ccm.Val0 - agm.Val0 * gain;
            aligned.ConvertTo(aligned, MatType.CV_8UC1, gain, offset);

            // ④ 差分 + 边缘带屏蔽 + 平滑
            using var raw = new Mat();
            Cv2.Absdiff(curGray, aligned, raw);
            int band = Math.Max(6, (int)Math.Round(Math.Min(raw.Width, raw.Height) * Math.Max(0, p.EdgeBand)));
            using (var keep = new Mat(raw.Size(), MatType.CV_8UC1, Scalar.White))
            {
                Cv2.Rectangle(keep, new Rect(0, 0, keep.Width, keep.Height), Scalar.Black, band);
                Cv2.BitwiseAnd(raw, keep, raw);
            }

            using var smooth = new Mat();
            int k = p.Smooth < 1 ? 1 : (p.Smooth % 2 == 0 ? p.Smooth + 1 : p.Smooth);
            if (k > 1) Cv2.GaussianBlur(raw, smooth, new Size(k, k), 0);
            else raw.CopyTo(smooth);
            smooth.CopyTo(diffOut);

            // ⑤ 阈值：均值 + k·标准差，k 由灵敏度换算（100 → 2σ，0 → 6σ）
            Cv2.MeanStdDev(smooth, out var mean, out var std);
            double s = Math.Clamp(p.Sensitivity, 0, 100);
            double sigma = 6.0 - 4.0 * (s / 100.0);
            double th = Math.Max(6.0, mean.Val0 + sigma * std.Val0);
            using var bin = new Mat();
            Cv2.Threshold(smooth, bin, th, 255, ThresholdTypes.Binary);

            // ⑥ 去噪：开 + 闭
            using var k3 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(bin, bin, MorphTypes.Open, k3);
            Cv2.MorphologyEx(bin, bin, MorphTypes.Close, k3);

            // ⑦ 连通域，按面积筛
            Cv2.FindContours(bin, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            double minArea = Math.Max(1, p.MinArea);
            foreach (var c in contours)
            {
                double area = Cv2.ContourArea(c);
                if (area < minArea) continue;
                var r = Cv2.BoundingRect(c);
                var m = Cv2.Moments(c);
                double px = m.M00 > 0 ? m.M10 / m.M00 : r.X + r.Width / 2.0;
                double py = m.M00 > 0 ? m.M01 / m.M00 : r.Y + r.Height / 2.0;
                using var roi = new Mat(smooth, r);
                Cv2.MinMaxLoc(roi, out _, out double peak);
                defects.Add(new ZsDefect(r.X, r.Y, r.Width, r.Height, area, peak));
            }
            defects.Sort((a, b) => b.Area.CompareTo(a.Area));
            note += $"，阈值 {th:F1}";
        }
        finally
        {
            if (ownGold) gold.Dispose();
        }

        return (defects, diffOut, dx, dy, ang, note);
    }

    private static Mat Trans(double dx, double dy)
        => new(2, 3, MatType.CV_64F, new double[] { 1, 0, dx, 0, 1, dy });

    private static Mat ToGray(Mat src)
    {
        var g = new Mat();
        if (src.Channels() == 1) src.CopyTo(g);
        else if (src.Channels() == 4) Cv2.CvtColor(src, g, ColorConversionCodes.BGRA2GRAY);
        else Cv2.CvtColor(src, g, ColorConversionCodes.BGR2GRAY);
        return g;
    }

    /// <summary>把查到的缺陷画到图上（红框 + 面积标签），返回新图。</summary>
    public static Mat Draw(Mat bgr, IReadOnlyList<ZsDefect> defects, int top = 10)
    {
        var outMat = bgr.Clone();
        var red = new Scalar(48, 59, 255);
        for (int i = 0; i < defects.Count && i < top; i++)
        {
            var d = defects[i];
            var p1 = new Point((int)Math.Round(d.X), (int)Math.Round(d.Y));
            var p2 = new Point((int)Math.Round(d.X + d.W), (int)Math.Round(d.Y + d.H));
            Cv2.Rectangle(outMat, p1, p2, red, 2);
            Cv2.PutText(outMat, "A" + (int)Math.Round(d.Area), new Point(p1.X, Math.Max(12, p1.Y - 4)),
                HersheyFonts.HersheyPlain, 0.9, red, 2, LineTypes.AntiAlias);
        }
        return outMat;
    }
}