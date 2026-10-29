using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>一条疑似划痕：外接框 + 长度 + 线宽（都按原图像素返回）。</summary>
public sealed record ScratchLine(double X, double Y, double W, double H, double Length, double Width, double Area);

/// <summary>找划痕的参数。</summary>
public sealed class ScratchParam
{
    /// <summary>灵敏度 0~100，越大阈值越低、越容易找出细线。</summary>
    public double Sensitivity { get; set; } = 80;

    /// <summary>比这短的不要（原图像素）。</summary>
    public double MinLength { get; set; } = 300;

    /// <summary>比这粗的不算划痕（原图像素，管壁/倒角那种宽边会被挡掉）。</summary>
    public double MaxWidth { get; set; } = 44;

    /// <summary>外接框里线条占的比例上限：划痕是细线，占比低；实心块（字、条码）占比高。</summary>
    public double MaxFill { get; set; } = 0.18;

    /// <summary>框起来只看这一块（null = 整张图）。</summary>
    public Rect? Roi { get; set; }
}

/// <summary>
/// 免训练的「找划痕」：不需要标准图、不需要训练。
/// 做法：黑帽（比周围暗的细线会被提亮）→ 阈值 → 连通域 →
/// 按「长、细、不实心、不围成一圈」四个条件留下真正的线条。
/// 半分辨率上计算（快 4 倍），参数按原图像素给，内部分别换算。
/// </summary>
public static class ScratchFinder
{
    private const double WorkScale = 0.5;

    public static (List<ScratchLine> lines, Mat response, string note) Find(Mat src, ScratchParam p)
    {
        var lines = new List<ScratchLine>();
        var resp = new Mat();
        if (src == null || src.Empty()) return (lines, resp, "没有图");

        using var gray = ToGray(src);
        int sw = Math.Max(1, (int)Math.Round(gray.Width * WorkScale));
        int sh = Math.Max(1, (int)Math.Round(gray.Height * WorkScale));
        using var small = new Mat();
        Cv2.Resize(gray, small, new Size(sw, sh), 0, 0, InterpolationFlags.Area);

        // 只看框里的那块
        Rect roi = new(0, 0, sw, sh);
        if (p.Roi is { } r && r.Width > 2 && r.Height > 2)
        {
            int x = (int)Math.Clamp(Math.Round(r.X * WorkScale), 0, Math.Max(0, sw - 4));
            int y = (int)Math.Clamp(Math.Round(r.Y * WorkScale), 0, Math.Max(0, sh - 4));
            int w = (int)Math.Clamp(Math.Round(r.Width * WorkScale), 4, sw - x);
            int h = (int)Math.Clamp(Math.Round(r.Height * WorkScale), 4, sh - y);
            roi = new Rect(x, y, w, h);
        }
        using var work = new Mat(small, roi);

        using var g = new Mat();
        Cv2.GaussianBlur(work, g, new Size(3, 3), 0, 0, BorderTypes.Reflect);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(31, 31));
        using var bh = new Mat();
        Cv2.MorphologyEx(g, bh, MorphTypes.BlackHat, kernel);
        bh.CopyTo(resp);

        double th = Math.Max(20.0, 140.0 - Math.Clamp(p.Sensitivity, 0, 100));
        using var bin = new Mat();
        Cv2.Threshold(bh, bin, th, 255, ThresholdTypes.Binary);
        using var k5 = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        Cv2.MorphologyEx(bin, bin, MorphTypes.Close, k5);

        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        int n = Cv2.ConnectedComponentsWithStats(bin, labels, stats, centroids, PixelConnectivity.Connectivity8);

        double minLen = Math.Max(20.0, p.MinLength * WorkScale);
        double maxW = Math.Max(2.0, p.MaxWidth * WorkScale);
        double inv = 1.0 / WorkScale;

        for (int i = 1; i < n; i++)
        {
            int x = stats.At<int>(i, 0), y = stats.At<int>(i, 1);
            int w = stats.At<int>(i, 2), h = stats.At<int>(i, 3);
            int area = stats.At<int>(i, 4);
            if (area < 120 || w < 2 || h < 2) continue;

            double len = Math.Sqrt((double)w * w + (double)h * h);
            if (len < minLen) continue;
            double fill = area / (double)(w * h);
            if (fill > p.MaxFill) continue;

            using var roiLabels = new Mat(labels, new Rect(x, y, w, h));
            using var mask = new Mat();
            Cv2.InRange(roiLabels, Scalar.All(i), Scalar.All(i), mask);

            // 围成一圈的（面板凹槽、标签边框、零件外轮廓）不算划痕：它们里面包着东西
            Cv2.FindContours(mask, out var cs, out var hier, RetrievalModes.CComp, ContourApproximationModes.ApproxSimple);
            bool hole = false;
            for (int c = 0; c < cs.Length; c++)
            {
                if (hier[c].Parent >= 0 && Cv2.ContourArea(cs[c]) > 200) { hole = true; break; }
            }
            if (hole) continue;

            using var dt = new Mat();
            Cv2.DistanceTransform(mask, dt, DistanceTypes.L2, DistanceTransformMasks.Mask3);
            Cv2.MinMaxLoc(dt, out _, out double maxDt);
            double width = 2 * maxDt;
            if (width < 1.5 || width > maxW) continue;

            lines.Add(new ScratchLine(
                (roi.X + x) * inv, (roi.Y + y) * inv, w * inv, h * inv,
                len * inv, width * inv, area * inv * inv));
        }

        lines.Sort((a, b) => b.Length.CompareTo(a.Length));
        string note = $"阈值 {th:F0}，找出 {lines.Count} 条";
        return (lines, resp, note);
    }

    /// <summary>把找到的划痕画到图上（红框 + 长度）。</summary>
    public static Mat Draw(Mat src, List<ScratchLine> lines)
    {
        var bgr = new Mat();
        if (src.Channels() == 1) Cv2.CvtColor(src, bgr, ColorConversionCodes.GRAY2BGR);
        else src.CopyTo(bgr);

        int thickness = Math.Max(2, (int)Math.Round(Math.Min(bgr.Width, bgr.Height) / 500.0));
        foreach (var l in lines)
        {
            var r = new Rect((int)Math.Round(l.X), (int)Math.Round(l.Y),
                             Math.Max(2, (int)Math.Round(l.W)), Math.Max(2, (int)Math.Round(l.H)));
            Cv2.Rectangle(bgr, r, new Scalar(0, 0, 255), thickness);
        }
        if (lines.Count > 0)
        {
            var best = lines[0];
            string txt = $"{lines.Count}";
            Cv2.PutText(bgr, txt, new Point(Math.Max(0, best.X), Math.Max(20, best.Y - 8)),
                        HersheyFonts.HersheyDuplex, 1.2, new Scalar(0, 0, 255), Math.Max(2, thickness - 1));
        }
        return bgr;
    }

    private static Mat ToGray(Mat src)
    {
        var g = new Mat();
        if (src.Channels() == 1) src.CopyTo(g);
        else if (src.Channels() == 4) Cv2.CvtColor(src, g, ColorConversionCodes.BGRA2GRAY);
        else Cv2.CvtColor(src, g, ColorConversionCodes.BGR2GRAY);
        return g;
    }
}
