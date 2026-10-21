// 移植自 LvStudioNi/_recon 逆向成果（lvMeasure/lvBlob/lvHistogram/lvTransform/lvBaseApi），实现：温启志
using System.Linq;
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>
/// 对应 lvBlob.dll 导出的 DipperVision::CBlobContour。
///
/// 原接口（逆自导出表）：
///   InitProcess(int w, int h)                  —— 预分配
///   Threshold(const IplImage*, double, bool)   —— 二值化；bool = 自动阈值(Otsu)
///   GetThreshValOtsu(const IplImage*, const IplImage*) -> double
///   FindContour(IplImage*, int, int, bool)     —— 找轮廓；返回 IplImage*
///   GenerateRegion(点集/CvRect/_dvCircle/_dvEllipse/_dvRect64d/_dvRing/_dvRotRect, bool)
///   BlobFilter(double, double, int, int)      —— 特征过滤（例：面积/周长上下限）
///   Sort(int)                                  —— 排序
///   GetResultNum() / GetBlobResult(int) -> _dvBlobResult
///   DrawContours(IplImage*, CvScalar, int)
///   ResetRegion(bool)
/// 手册对应：6.4.6 BLOB 分析 / 6.4.7 连通域分析。
/// </summary>

/// <summary>Blob 区域属性（手册"统计对象 / 掩膜对象"）。</summary>
public enum DvRegionRole { Statistic = 0, Mask = 1 }

/// <summary>Blob 极性（手册"亮区域/暗区域"）。</summary>
public enum DvBlobPolarity { Bright = 0, Dark = 1 }

/// <summary>阈值判断方式（手册 6.4.7"二值化/自动阈值/双阈值"）。</summary>
public enum DvThresholdMode { Fixed = 0, Otsu = 1, Double = 2 }

/// <summary>统计模式（手册"排除区域内孔洞 / 包含区域内孔洞"）。</summary>
public enum DvStatMode { ExcludeHoles = 0, IncludeHoles = 1 }

/// <summary>Blob 过滤与统计参数。</summary>
public sealed class BlobParam
{
    public DvRegionRole Role { get; set; } = DvRegionRole.Statistic;
    public DvBlobPolarity Polarity { get; set; } = DvBlobPolarity.Bright;
    public DvThresholdMode ThresholdMode { get; set; } = DvThresholdMode.Otsu;
    public double ThresholdLo { get; set; } = 128;
    public double ThresholdHi { get; set; } = 200;
    public DvStatMode StatMode { get; set; } = DvStatMode.ExcludeHoles;

    // 过滤范围
    public (double lo, double hi) Area { get; set; } = (0, double.MaxValue);
    public (double lo, double hi) Perimeter { get; set; } = (0, double.MaxValue);
    public (double lo, double hi) XRange { get; set; } = (0, double.MaxValue);
    public (double lo, double hi) YRange { get; set; } = (0, double.MaxValue);

    /// <summary>过滤动作：false=移除范围外目标，true=保留（反选）。</summary>
    public bool InvertFilter { get; set; }

    public DvBlobSort Sort { get; set; } = DvBlobSort.AreaDesc;

    /// <summary>ROI（含掩膜）列表。role 决定统计还是屏蔽。</summary>
    public List<(object shape, DvRegionRole role)> Regions { get; } = new();
}

/// <summary>CBlobContour 的托管复刻。</summary>
public static class Blob
{
    /// <summary>GetThreshValOtsu：对 ROI 内像素算 Otsu 最佳阈值。</summary>
    public static double ThreshValOtsu(Mat gray, Mat? mask = null)
    {
        using var m = new Mat();
        if (mask != null) Cv2.BitwiseAnd(gray, gray, m, mask);
        else gray.CopyTo(m);
        double t = Cv2.Threshold(m, m, 0, 255,
            ThresholdTypes.Otsu | ThresholdTypes.Binary);
        return t;
    }

    /// <summary>
    /// Threshold：按参数做二值化，返回二值图（已按极性取到目标为白）。
    /// </summary>
    public static Mat Threshold(Mat gray, BlobParam p, out double usedThreshold, Mat? roiMask = null)
    {
        var bin = new Mat();
        usedThreshold = p.ThresholdLo;

        switch (p.ThresholdMode)
        {
            case DvThresholdMode.Otsu:
                var src = roiMask != null ? new Mat() : gray;
                if (roiMask != null) Cv2.BitwiseAnd(gray, gray, src, roiMask);
                usedThreshold = Cv2.Threshold(src, bin, 0, 255,
                    ThresholdTypes.Otsu | ThresholdTypes.Binary);
                break;

            case DvThresholdMode.Double:
                // 双阈值：灰度 > 低阈值 或 < 高阈值 为目标
                var hi = new Mat(); var lo = new Mat();
                Cv2.Threshold(gray, hi, p.ThresholdHi, 255, ThresholdTypes.BinaryInv);
                Cv2.Threshold(gray, lo, p.ThresholdLo, 255, ThresholdTypes.Binary);
                Cv2.BitwiseOr(hi, lo, bin);
                hi.Dispose(); lo.Dispose();
                break;

            default:
                Cv2.Threshold(gray, bin, p.ThresholdLo, 255, ThresholdTypes.Binary);
                break;
        }

        if (p.Polarity == DvBlobPolarity.Dark) Cv2.BitwiseNot(bin, bin);

        if (roiMask != null) Cv2.BitwiseAnd(bin, bin, bin, roiMask);
        return bin;
    }

    /// <summary>
    /// FindContour + BlobFilter + Sort + GetBlobResult 的完整一条链。
    /// </summary>
    public static List<DvBlobResult> Analyze(Mat gray, BlobParam p)
    {
        // 1. 生成 ROI 掩膜
        Mat? roiMask = null;
        foreach (var (shape, role) in p.Regions)
        {
            if (role != DvRegionRole.Statistic) continue;
            var m = RasterizeShape(gray.Size(), shape);
            if (m == null) continue;
            if (roiMask == null) roiMask = m; else { Cv2.BitwiseOr(roiMask, m, roiMask); m.Dispose(); }
        }
        // 掩膜区域单独扣除
        Mat? holeMask = null;
        foreach (var (shape, role) in p.Regions)
        {
            if (role != DvRegionRole.Mask) continue;
            var m = RasterizeShape(gray.Size(), shape);
            if (m == null) continue;
            if (holeMask == null) holeMask = m; else { Cv2.BitwiseOr(holeMask, m, holeMask); m.Dispose(); }
        }

        // 2. 二值化
        using var bin = Threshold(gray, p, out double thr, roiMask);
        if (holeMask != null)
        {
            using var notHole = new Mat();
            Cv2.BitwiseNot(holeMask, notHole);
            Cv2.BitwiseAnd(bin, notHole, bin);
        }
        roiMask?.Dispose();
        holeMask?.Dispose();

        // 3. 找轮廓
        Cv2.FindContours(bin, out var contours, out var hierarchy,
            RetrievalModes.List, ContourApproximationModes.ApproxNone);

        // 4. 逐轮廓算特征（排除/包含孔洞由 hierarchy 决定）
        var results = new List<DvBlobResult>(contours.Length);
        for (int i = 0; i < contours.Length; i++)
        {
            double area = p.StatMode == DvStatMode.IncludeHoles
                ? Cv2.ContourArea(contours[i], true) + HoleArea(contours, hierarchy, i, sign: true)
                : Cv2.ContourArea(contours[i]);
            if (area <= 0) area = Math.Abs(area);

            double peri = Cv2.ArcLength(contours[i], true);
            var mom = Cv2.Moments(contours[i]);
            var cent = mom.M00 > 0
                ? new DvPoint2D(mom.M10 / mom.M00, mom.M01 / mom.M00)
                : DvPoint2D.Empty;
            var box = Cv2.MinAreaRect(contours[i]);
            var minRect = new DvRotRect(new DvPoint2D(box.Center.X, box.Center.Y),
                                        box.Size.Width, box.Size.Height,
                                        box.Angle * Math.PI / 180.0);
            Cv2.MinEnclosingCircle(contours[i], out var encC, out float radius);
            var minCircle = new DvCircle(new DvPoint2D(encC.X, encC.Y), radius);

            // 主轴方向（用二阶矩）
            double mu20 = mom.Mu20, mu02 = mom.Mu02, mu11 = mom.Mu11;
            double theta = 0.5 * Math.Atan2(2 * mu11, mu20 - mu02) + Math.PI / 2;
            var axis = new DvVector2D(Math.Cos(theta), Math.Sin(theta));

            results.Add(new DvBlobResult
            {
                Area = area,
                Perimeter = peri,
                Centroid = cent,
                MinRect = minRect.Bounds(),
                MinCircle = minCircle,
                Axis = axis,
                PixelCount = contours[i].Length,
            });
        }

        // 5. 过滤
        var kept = results.Where(r => InRange(r.Area, p.Area)
                                  && InRange(r.Perimeter, p.Perimeter)
                                  && InRange(r.Centroid.X, p.XRange)
                                  && InRange(r.Centroid.Y, p.YRange));
        if (p.InvertFilter) kept = results.Where(r => !(InRange(r.Area, p.Area)
                                  && InRange(r.Perimeter, p.Perimeter)
                                  && InRange(r.Centroid.X, p.XRange)
                                  && InRange(r.Centroid.Y, p.YRange)));
        var list = kept.ToList();

        // 6. 排序
        list = p.Sort switch
        {
            DvBlobSort.AreaAsc => list.OrderBy(r => r.Area).ToList(),
            DvBlobSort.AreaDesc => list.OrderByDescending(r => r.Area).ToList(),
            DvBlobSort.PerimeterAsc => list.OrderBy(r => r.Perimeter).ToList(),
            DvBlobSort.PerimeterDesc => list.OrderByDescending(r => r.Perimeter).ToList(),
            DvBlobSort.XAsc => list.OrderBy(r => r.Centroid.X).ToList(),
            DvBlobSort.XDesc => list.OrderByDescending(r => r.Centroid.X).ToList(),
            DvBlobSort.YAsc => list.OrderBy(r => r.Centroid.Y).ToList(),
            DvBlobSort.YDesc => list.OrderByDescending(r => r.Centroid.Y).ToList(),
            _ => list,
        };

        return list;
    }

    private static double HoleArea(Point[][] contours, HierarchyIndex[] hierarchy, int idx, bool sign)
    {
        double sum = 0;
        var h = hierarchy[idx];
        int child = h.Child;
        while (child >= 0 && child < contours.Length)
        {
            h = hierarchy[child];
            sum += Math.Abs(Cv2.ContourArea(contours[child]));
            child = h.Next;
        }
        return sign ? sum : -sum;
    }

    private static OpenCvSharp.Point[] ToIntPts(Point2d[] src)
        => src.Select(q => new OpenCvSharp.Point((int)Math.Round(q.X), (int)Math.Round(q.Y))).ToArray();

    private static bool InRange(double v, (double lo, double hi) r)
        => v >= r.lo && v <= r.hi;

    /// <summary>把 _dv* 形状栅格化为掩膜。</summary>
    public static Mat? RasterizeShape(Size size, object shape)
    {
        var m = new Mat(size, MatType.CV_8UC1, Scalar.All(0));
        switch (shape)
        {
            case DvRect64d r:
                Cv2.Rectangle(m, new OpenCvSharp.Point((int)r.X, (int)r.Y),
                    new OpenCvSharp.Point((int)(r.X + r.Width), (int)(r.Y + r.Height)),
                    Scalar.All(255), -1);
                break;
            case DvCircle c:
                Cv2.Circle(m, new OpenCvSharp.Point((int)c.Center.X, (int)c.Center.Y),
                    (int)c.Radius, Scalar.All(255), -1);
                break;
            case DvRing ring:
                Cv2.Circle(m, new OpenCvSharp.Point((int)ring.Center.X, (int)ring.Center.Y),
                    (int)ring.OuterRadius, Scalar.All(255), -1);
                Cv2.Circle(m, new OpenCvSharp.Point((int)ring.Center.X, (int)ring.Center.Y),
                    (int)ring.InnerRadius, Scalar.All(0), -1);
                break;
            case DvRotRect rr:
                Cv2.FillPoly(m, new[] { ToIntPts(rr.Corners()) }, Scalar.All(255));
                break;
            case DvEllipse e:
                Cv2.Ellipse(m, new OpenCvSharp.Point((int)e.Center.X, (int)e.Center.Y),
                    new Size((int)e.A, (int)e.B), e.AngleRad * 180 / Math.PI, 0, 360,
                    Scalar.All(255), -1);
                break;
            case Point2d[] poly:
                Cv2.FillPoly(m, new[] { poly.Select(q => new OpenCvSharp.Point((int)q.X, (int)q.Y)).ToArray() }, Scalar.All(255));
                break;
            default:
                m.Dispose();
                return null;
        }
        return m;
    }
}
