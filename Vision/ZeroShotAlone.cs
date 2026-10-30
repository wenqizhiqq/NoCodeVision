// 零样本（无参考图）：只看这一张图，把「和周围不一样」的地方挑出来。
// 做法：多尺度中值背景求偏离（Lab 色差 + 灰度差）→ 稳健 z 分数 → 阈值 → 连通域。
// 实现：温启志

using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>零样本查出来的可疑区域。</summary>
public sealed record AloneDefect(double X, double Y, double W, double H, double Area, double Peak);

public static class ZeroShotAlone
{
    /// <summary>每个尺度上用多大的中值核找背景（大核相当于把比它小的东西都抹掉）。</summary>
    static readonly int[] Scales = { 9, 25, 61 };

    /// <summary>
    /// 只看这一张图找异常：越不像周围越亮。
    /// sensitivity 0~100（越大挑得越细），minArea 是最小缺陷面积（像素）。
    /// score8 是 0~255 的分数图（给热力图用），threshold 是判定用的 z 门槛。
    /// </summary>
    public static (List<AloneDefect> defects, Mat score8, double threshold, string note)
        Run(Mat bgr, double sensitivity = 60, double minArea = 300)
    {
        var defects = new List<AloneDefect>();
        var score8 = new Mat();
        if (bgr == null || bgr.Empty()) return (defects, score8, 0, "没有图");

        using var lab = new Mat();
        using var gray = new Mat();
        Cv2.CvtColor(bgr, lab, ColorConversionCodes.BGR2Lab);
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        Mat? zMax = null;
        int minSide = Math.Min(bgr.Width, bgr.Height);
        foreach (var s in Scales)
        {
            int k = s;
            if (k > minSide / 3) k = minSide / 3;
            if (k % 2 == 0) k++;
            if (k < 5) continue;

            using var bgCol = new Mat();
            using var bgLab = new Mat();
            using var bgGray = new Mat();
            Cv2.MedianBlur(bgr, bgCol, k);
            Cv2.CvtColor(bgCol, bgLab, ColorConversionCodes.BGR2Lab);
            Cv2.MedianBlur(gray, bgGray, k);

            // Lab 三个通道的色差（ΔE）
            using var dLab = new Mat();
            Cv2.Absdiff(lab, bgLab, dLab);
            using var dF = new Mat();
            dLab.ConvertTo(dF, MatType.CV_32FC3);
            using var sq = new Mat();
            Cv2.Multiply(dF, dF, sq);
            var ch = sq.Split();
            using var sum = new Mat();
            Cv2.Add(ch[0], ch[1], sum);
            Cv2.Add(sum, ch[2], sum);
            using var dE = new Mat();
            Cv2.Sqrt(sum, dE);
            foreach (var c in ch) c.Dispose();

            // 灰度上的明暗差（细裂纹主要靠它）
            using var dG = new Mat();
            Cv2.Absdiff(gray, bgGray, dG);
            using var dGf = new Mat();
            dG.ConvertTo(dGf, MatType.CV_32FC1, 1.5);

            using var dev = new Mat();
            Cv2.Max(dE, dGf, dev);

            // 稳健 z：中位数 + MAD，用直方图统计（整幅图精确算，不抽样）
            (double med, double mad) = MedianMad(dev);
            double scale = 1.4826 * mad + 1.0;

            using var z = new Mat();
            dev.ConvertTo(z, MatType.CV_32FC1, 1.0 / scale, -med / scale);

            if (zMax == null) zMax = z.Clone();
            else Cv2.Max(zMax, z, zMax);
        }

        if (zMax == null) return (defects, score8, 0, "图太小，算不出来");

        double sens = Math.Clamp(sensitivity, 0, 100);
        double thr = 12.0 - 6.0 * sens / 100.0;      // 100 → 6σ，0 → 12σ

        using var mask = new Mat();
        Cv2.Threshold(zMax, mask, thr, 255, ThresholdTypes.Binary);
        mask.ConvertTo(mask, MatType.CV_8UC1);
        using var k3 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var k15 = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(15, 15));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, k3);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, k15);
        // 补洞：从左上角灌水，灌不到的地方就是被轮廓包住的洞（虫子/压痕中间常是空心的）
        using (var flood = mask.Clone())
        {
            Cv2.FloodFill(flood, new Point(0, 0), new Scalar(255));
            using var holes = new Mat();
            Cv2.BitwiseNot(flood, holes);
            Cv2.BitwiseOr(mask, holes, mask);
        }

        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        double areaFloor = Math.Max(150, minArea);
        foreach (var c in contours)
        {
            double area = Math.Abs(Cv2.ContourArea(c));
            if (area < areaFloor) continue;
            var r = Cv2.BoundingRect(c);
            using var roi = new Mat(zMax, r);
            Cv2.MinMaxLoc(roi, out _, out double peak);
            defects.Add(new AloneDefect(r.X, r.Y, r.Width, r.Height, area, peak));
        }
        defects.Sort((a, b) => b.Area.CompareTo(a.Area));

        // 热力图用：z 压到 0~255（40σ 封顶），门槛也按同样比例换算
        using var z8 = new Mat();
        zMax.ConvertTo(z8, MatType.CV_8UC1, 255.0 / 40.0);
        z8.CopyTo(score8);

        string note = $"{defects.Count} 处 · 门槛 {thr:F1}σ";
        return (defects, score8, thr, note);
    }

    /// <summary>整幅图的中位数和 MAD（把 0~511 的偏离值装进直方图数）。</summary>
    static (double median, double mad) MedianMad(Mat dev)
    {
        const int Bins = 512;
        var hist = new int[Bins];
        int total = 0;
        for (int y = 0; y < dev.Rows; y++)
        {
            for (int x = 0; x < dev.Cols; x++)
            {
                float v = dev.At<float>(y, x);
                int b = v <= 0 ? 0 : (v >= Bins - 1 ? Bins - 1 : (int)v);
                hist[b]++;
                total++;
            }
        }
        if (total == 0) return (0, 1);
        int half = total / 2, acc = 0, medBin = 0;
        for (int i = 0; i < Bins; i++)
        {
            acc += hist[i];
            if (acc >= half) { medBin = i; break; }
        }
        var hist2 = new int[Bins];
        for (int y = 0; y < dev.Rows; y++)
            for (int x = 0; x < dev.Cols; x++)
            {
                int d = (int)Math.Abs(dev.At<float>(y, x) - medBin);
                hist2[d >= Bins ? Bins - 1 : d]++;
            }
        acc = 0;
        int madBin = 0;
        for (int i = 0; i < Bins; i++)
        {
            acc += hist2[i];
            if (acc >= half) { madBin = i; break; }
        }
        return (medBin, Math.Max(1, madBin));
    }

    /// <summary>把查出来的区域画到图上：青色轮廓 + 中心十字。</summary>
    public static Mat Draw(Mat bgr, IReadOnlyList<AloneDefect> defects)
    {
        var outMat = bgr.Clone();
        var cyan = new Scalar(255, 255, 0);
        var red = new Scalar(48, 59, 255);
        foreach (var d in defects.Take(20))
        {
            var p1 = new Point((int)Math.Round(d.X), (int)Math.Round(d.Y));
            var p2 = new Point((int)Math.Round(d.X + d.W), (int)Math.Round(d.Y + d.H));
            Cv2.Rectangle(outMat, p1, p2, cyan, 1, LineTypes.AntiAlias);
            int cx = p1.X + (p2.X - p1.X) / 2, cy = p1.Y + (p2.Y - p1.Y) / 2;
            Cv2.Line(outMat, new Point(cx - 6, cy), new Point(cx + 6, cy), red, 2);
            Cv2.Line(outMat, new Point(cx, cy - 6), new Point(cx, cy + 6), red, 2);
        }
        return outMat;
    }
}