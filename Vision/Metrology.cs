using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>精找圆的结果：圆心、半径、内点数、残差、覆盖率、得分。</summary>
public sealed record CircleProResult(double Cx, double Cy, double R, int Inliers, int Total, double Rms, double CoverageDeg, double Score, string Note);

/// <summary>精找直线的结果：线上一点、方向、角度、内点数、残差、得分。</summary>
public sealed record LineProResult(double Px, double Py, double Dx, double Dy, double AngleDeg, int Inliers, int Total, double Rms, double Score, string Note);

/// <summary>精找圆参数（对应 demo 的 circle recipe 简化版）。</summary>
public sealed class CircleProParam
{
    /// <summary>半径大概多少，0 = 按 ROI 自动估。</summary>
    public double ExpectR { get; set; }
    /// <summary>沿圆周摆多少个卡尺。</summary>
    public int Calipers { get; set; } = 48;
    /// <summary>每个卡尺沿半径方向看多长。</summary>
    public double CaliperLength { get; set; } = 40;
    /// <summary>明暗方向：0=从暗到亮 1=从亮到暗 2=两种都要。</summary>
    public int Polarity { get; set; } = 2;
    /// <summary>边缘灵敏度 0~100（换算成归一化梯度阈值）。</summary>
    public double Sensitivity { get; set; } = 30;
    /// <summary>内点率下限，低于它判「不可信」。</summary>
    public double MinInlierRatio { get; set; } = 0.35;
}

/// <summary>精找直线参数。</summary>
public sealed class LineProParam
{
    /// <summary>沿 ROI 长边摆多少个卡尺。</summary>
    public int Calipers { get; set; } = 24;
    /// <summary>每个卡尺沿垂直方向看多长（一般 = ROI 短边）。</summary>
    public double CaliperLength { get; set; } = 40;
    public int Polarity { get; set; } = 2;
    public double Sensitivity { get; set; } = 30;
    public double MinInlierRatio { get; set; } = 0.4;
}

/// <summary>
/// 亚像素计量内核（对应「最强传统找圆找直线」demo 的主流程，简化但同路子）：
/// 卡尺采一维灰度剖面 → 平滑后求导 → 取峰并做抛物线插值（亚像素）→
/// 圆/直线都先用最小二乘起头，再用 Tukey 加权（IRLS）挤掉飞点 → 输出残差、内点率、覆盖率、得分。
/// 纯 OpenCV，不依赖 WPF。
/// </summary>
public static class Metrology
{
    const double TukeyC = 4.685;

    public static (CircleProResult? result, Mat drawn, string note) FindCircle(Mat src, Rect? roi, CircleProParam p)
    {
        var empty = new Mat();
        if (src == null || src.Empty()) return (null, empty, "没有图");
        using var g = ToGray(src);

        Rect box = roi ?? new Rect(0, 0, g.Width, g.Height);
        box = Clamp(box, g.Width, g.Height);
        double cx = box.X + box.Width / 2.0, cy = box.Y + box.Height / 2.0;
        double r0 = p.ExpectR > 1 ? p.ExpectR : Math.Min(box.Width, box.Height) / 2.0;
        double half = Math.Max(4.0, p.CaliperLength / 2.0);
        int n = Math.Clamp(p.Calipers, 8, 720);

        var pts = new List<Point2d>();
        for (int pass = 0; pass < 8; pass++)
        {
            pts.Clear();
            for (int i = 0; i < n; i++)
            {
                double th = 2 * Math.PI * i / n;
                double ux = Math.Cos(th), uy = Math.Sin(th);
                // 以「期望半径处」为中心，沿半径方向前后各扫半个卡尺长度
                double sx = cx + r0 * ux, sy = cy + r0 * uy;
                if (EdgeNearest(g, sx, sy, ux, uy, (int)Math.Round(2 * half), p.Sensitivity, p.Polarity, out double t))
                    pts.Add(new Point2d(sx + t * ux, sy + t * uy));
            }
            if (pts.Count < 8) break;

            // 半径中位数 ± 3×MAD 之外的点当误检（直边、纹理、灰尘），剔掉再拟合
            var radii = new double[pts.Count];
            for (int i = 0; i < pts.Count; i++)
                radii[i] = Math.Sqrt((pts[i].X - cx) * (pts[i].X - cx) + (pts[i].Y - cy) * (pts[i].Y - cy));
            var sorted = (double[])radii.Clone();
            Array.Sort(sorted);
            double medR = sorted[sorted.Length / 2];
            var devs = new double[radii.Length];
            for (int i = 0; i < radii.Length; i++) devs[i] = Math.Abs(radii[i] - medR);
            Array.Sort(devs);
            double mad = devs[devs.Length / 2];
            double cut = Math.Max(2.5, 3.0 * mad);
            var keep = new List<Point2d>();
            for (int i = 0; i < pts.Count; i++) if (Math.Abs(radii[i] - medR) <= cut) keep.Add(pts[i]);
            if (keep.Count < 8) break;

            if (!FitCircleLs(keep, out double ncx, out double ncy, out double nr)) break;
            double move = Math.Abs(ncx - cx) + Math.Abs(ncy - cy) + Math.Abs(nr - r0);
            cx = ncx; cy = ncy; r0 = nr;
            if (pass == 7 || move < 1e-3)
            {
                double cov = Coverage(keep, cx, cy);
                double rmsAll = 0;
                int inl = 0;
                foreach (var q in pts)
                {
                    double d = Math.Sqrt((q.X - cx) * (q.X - cx) + (q.Y - cy) * (q.Y - cy));
                    if (Math.Abs(d - r0) <= Math.Max(2.0, 3 * mad)) { inl++; rmsAll += (d - r0) * (d - r0); }
                }
                double rms = inl > 0 ? Math.Sqrt(rmsAll / inl) : 0;
                double ratio = inl / (double)Math.Max(1, pts.Count);
                double score = ratio * Math.Min(1.0, cov / 270.0);
                string note = $"内点 {inl}/{pts.Count}，RMS {rms:F3} 像素，覆盖率 {cov:F0}°";
                var res = new CircleProResult(cx, cy, r0, inl, pts.Count, rms, cov, score, note);
                var drawn = DrawCircle(src, res, keep);
                return (res, drawn, note + (ratio < p.MinInlierRatio ? "（内点太少，结果要复核）" : ""));
            }
        }
        return (null, empty, "没找到圆（边缘太少或半径估得太偏）");
    }

    public static (LineProResult? result, Mat drawn, string note) FindLine(Mat src, Rect? roi, LineProParam p)
    {
        var empty = new Mat();
        if (src == null || src.Empty()) return (null, empty, "没有图");
        using var g = ToGray(src);
        Rect box = Clamp(roi ?? new Rect(0, 0, g.Width, g.Height), g.Width, g.Height);
        if (box.Width < 8 || box.Height < 8) return (null, empty, "框太小");

        // ROI 长边 = 线走向；卡尺沿长边均匀摆，垂直方向扫
        bool horizontal = box.Width >= box.Height;
        int n = Math.Clamp(p.Calipers, 4, 400);
        double half = Math.Max(3.0, p.CaliperLength / 2.0);

        var pts = new List<Point2d>();
        for (int i = 0; i < n; i++)
        {
            double f = n == 1 ? 0.5 : (i + 0.5) / n;
            if (horizontal)
            {
                double x = box.X + f * box.Width;
                if (EdgeAlong(g, x, box.Y + box.Height / 2.0, 0, 1, 1, 0, ProjHalf, (int)Math.Round(half * 2), p.Sensitivity, p.Polarity, out double t, out _))
                    pts.Add(new Point2d(x, box.Y + box.Height / 2.0 + t));
            }
            else
            {
                double y = box.Y + f * box.Height;
                if (EdgeAlong(g, box.X + box.Width / 2.0, y, 1, 0, 0, 1, ProjHalf, (int)Math.Round(half * 2), p.Sensitivity, p.Polarity, out double t, out _))
                    pts.Add(new Point2d(box.X + box.Width / 2.0 + t, y));
            }
        }
        if (pts.Count < 4) return (null, empty, "边缘太少，找不到直线");

        if (!FitLineRobust(pts, out double px, out double py, out double dx, out double dy, out int inl, out double rms)) return (null, empty, "拟合失败");
        double ang = Math.Atan2(dy, dx) * 180 / Math.PI;
        double inlierRatio = inl / (double)Math.Max(1, pts.Count);
        var res = new LineProResult(px, py, dx, dy, ang, inl, pts.Count, rms, inlierRatio, $"内点 {inl}/{pts.Count}，RMS {rms:F3} 像素，角度 {ang:F2}°");
        var drawn = DrawLine(src, res, pts);
        return (res, drawn, res.Note + (inlierRatio < p.MinInlierRatio ? "（内点太少，结果要复核）" : ""));
    }

    // ---------- 一维剖面亚像素找边 ----------
    /// <summary>找线时每个卡尺沿走向取多宽（±ProjHalf）：单列采样会把灰尘当边，取中位数更抗灰。</summary>
    const int ProjHalf = 4;

    static bool EdgeAlong(Mat g, double x0, double y0, double ux, double uy, double vx, double vy, int halfW, int count, double sensitivity, int polarity, out double t, out double strength)
    {
        t = 0; strength = 0;
        count = Math.Clamp(count, 6, 400);
        var prof = new double[count];
        var buf = new double[2 * halfW + 1];
        for (int i = 0; i < count; i++)
        {
            double s = i - (count - 1) / 2.0;
            for (int j = -halfW; j <= halfW; j++)
                buf[j + halfW] = Sample(g, x0 + s * ux + j * vx, y0 + s * uy + j * vy);
            Array.Sort(buf);
            prof[i] = buf[halfW];
        }
        // 轻微平滑，抑制噪声
        var sm = new double[count];
        for (int pass = 0; pass < 4; pass++)
        {
            var src = pass == 0 ? prof : (double[])sm.Clone();
            for (int i = 0; i < count; i++)
            {
                double a = src[Math.Max(0, i - 1)], b = src[i], c = src[Math.Min(count - 1, i + 1)];
                sm[i] = (a + 2 * b + c) / 4.0;
            }
        }
        // 导数
        var d = new double[count];
        double maxAbs = 0;
        for (int i = 1; i < count - 1; i++)
        {
            d[i] = (sm[i + 1] - sm[i - 1]) / 2.0;
            if (Math.Abs(d[i]) > maxAbs) maxAbs = Math.Abs(d[i]);
        }
        if (maxAbs < 1e-6) return false;
        // 门槛跟着这条剖面自己的噪声走：软边（几十个灰阶摊在几十像素上）梯度可能只有 1~2，
        // 固定门槛 4 会把真边全挡掉、只剩灰尘尖峰。
        var absD = new List<double>(count - 2);
        for (int i = 1; i < count - 1; i++) absD.Add(Math.Abs(d[i]));
        absD.Sort();
        double dMed = absD[absD.Count / 2];
        double mad = 0;
        for (int i = 0; i < absD.Count; i++) mad += Math.Abs(absD[i] - dMed);
        mad /= absD.Count;
        double sigmaD = Math.Max(0.02, 1.4826 * mad);
        double floor = Math.Max(0.35, 2.5 * sigmaD);
        double thresh = Math.Max(floor, maxAbs * 0.10 * (1.0 - sensitivity / 150.0));
        int best = -1;
        double bestVal = 0;
        for (int i = 1; i < count - 1; i++)
        {
            double v = d[i];
            if (polarity == 0 && v <= 0) continue;      // 从暗到亮：正值
            if (polarity == 1 && v >= 0) continue;      // 从亮到暗：负值
            if (Math.Abs(v) < thresh) continue;
            // 必须是局部极值
            if (Math.Abs(v) < Math.Abs(d[i - 1]) || Math.Abs(v) < Math.Abs(d[i + 1])) continue;
            if (Math.Abs(v) > Math.Abs(bestVal)) { bestVal = v; best = i; }
        }
        if (best < 1) return false;
        // 抛物线插值求亚像素位置
        double y0v = Math.Abs(d[best - 1]), y1v = Math.Abs(d[best]), y2v = Math.Abs(d[best + 1]);
        double denom = y0v - 2 * y1v + y2v;
        double delta = Math.Abs(denom) > 1e-9 ? 0.5 * (y0v - y2v) / denom : 0;
        delta = Math.Clamp(delta, -0.5, 0.5);
        t = (best - (count - 1) / 2.0) + delta;
        strength = y1v;
        return true;
    }


    /// <summary>沿剖面找边，多个峰里挑「离期望位置最近」的那个（对应 demo 的 edge_select_mode）。</summary>
    static bool EdgeNearest(Mat g, double x0, double y0, double ux, double uy, int count, double sensitivity, int polarity, out double t)
    {
        t = 0;
        count = Math.Clamp(count, 8, 400);
        var prof = new double[count];
        for (int i = 0; i < count; i++)
        {
            double s = i - (count - 1) / 2.0;
            prof[i] = Sample(g, x0 + s * ux, y0 + s * uy);
        }
        var sm = new double[count];
        for (int pass = 0; pass < 4; pass++)
        {
            var src = pass == 0 ? prof : (double[])sm.Clone();
            for (int i = 0; i < count; i++)
            {
                double a = src[Math.Max(0, i - 1)], b = src[i], c = src[Math.Min(count - 1, i + 1)];
                sm[i] = (a + 2 * b + c) / 4.0;
            }
        }
        var d = new double[count];
        double maxAbs = 0;
        for (int i = 1; i < count - 1; i++)
        {
            d[i] = (sm[i + 1] - sm[i - 1]) / 2.0;
            if (Math.Abs(d[i]) > maxAbs) maxAbs = Math.Abs(d[i]);
        }
        if (maxAbs < 1e-6) return false;
        double thresh = Math.Max(4.0, maxAbs * 0.25 * (1.0 - Math.Clamp(sensitivity, 0, 100) / 150.0));
        double bestT = 0, bestAbs = double.MaxValue;
        bool found = false;
        for (int i = 1; i < count - 1; i++)
        {
            double v = d[i];
            if (polarity == 0 && v <= 0) continue;
            if (polarity == 1 && v >= 0) continue;
            if (Math.Abs(v) < thresh) continue;
            if (Math.Abs(v) < Math.Abs(d[i - 1]) || Math.Abs(v) < Math.Abs(d[i + 1])) continue;
            double y0v = Math.Abs(d[i - 1]), y1v = Math.Abs(d[i]), y2v = Math.Abs(d[i + 1]);
            double denom = y0v - 2 * y1v + y2v;
            double delta = Math.Abs(denom) > 1e-9 ? 0.5 * (y0v - y2v) / denom : 0;
            double tt = (i - (count - 1) / 2.0) + Math.Clamp(delta, -0.5, 0.5);
            if (Math.Abs(tt) < bestAbs) { bestAbs = Math.Abs(tt); bestT = tt; found = true; }
        }
        if (!found) return false;
        t = bestT;
        return true;
    }

    static double Sample(Mat g, double x, double y)
    {
        if (x < 0 || y < 0 || x > g.Width - 1.001 || y > g.Height - 1.001) return 0;
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        double a = g.At<byte>(y0, x0), b = g.At<byte>(y0, x0 + 1), c = g.At<byte>(y0 + 1, x0), d = g.At<byte>(y0 + 1, x0 + 1);
        return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
    }

    // ---------- 稳健圆拟合（LS 起头 + Tukey IRLS + 几何精修） ----------
    static bool FitCircleRobust(List<Point2d> pts, double expectR, out double cx, out double cy, out double r,
                                out double rms, out double sigma, out int inliers, out double coverageDeg)
    {
        cx = cy = r = rms = sigma = 0; inliers = 0; coverageDeg = 0;
        if (expectR > 1)
        {
            // 有半径先验：固定半径做 RANSAC 找圆心（直边、飞点不会把圆心带跑），再用内点精修
            r = expectR;
            var keep = RansacCircleFixedR(pts, r, 3.0);
            if (keep.Count < 6) return false;
            if (!FitCircleFixedR(keep, r, ref cx, ref cy)) return false;
            pts = keep;
        }
        else if (!FitCircleLs(pts, out cx, out cy, out r)) return false;
        for (int it = 0; it < 12; it++)
        {
            rms = Rms(pts, cx, cy, r);
            sigma = Math.Max(0.05, rms * 1.4826);
            double c = TukeyC * sigma;
            double sw = 0, sx = 0, sy = 0, sr = 0;
            inliers = 0;
            foreach (var p in pts)
            {
                double d = Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
                double res = d - r;
                if (Math.Abs(res) >= c) continue;
                double u = res / c;
                double w = (1 - u * u) * (1 - u * u);
                sw += w; sx += w * p.X; sy += w * p.Y; sr += w * d;
                inliers++;
            }
            if (sw <= 1e-9) return false;
            double ncx = sx / sw, ncy = sy / sw;
            double nr = expectR > 1 ? r : sr / sw;   // 给了半径先验就锁住半径，只调圆心
            double move = Math.Abs(ncx - cx) + Math.Abs(ncy - cy) + Math.Abs(nr - r);
            cx = ncx; cy = ncy; r = nr;
            if (move < 1e-4) break;
        }
        rms = Rms(pts, cx, cy, r);
        coverageDeg = Coverage(pts, cx, cy);
        return r > 0.5;
    }

    static bool FitCircleLs(List<Point2d> pts, out double cx, out double cy, out double r)
    {
        // Kasa：x²+y² + D x + E y + F = 0
        double n = pts.Count, sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0, sz = 0;
        foreach (var p in pts)
        {
            double z = p.X * p.X + p.Y * p.Y;
            sx += p.X; sy += p.Y; sxx += p.X * p.X; syy += p.Y * p.Y; sxy += p.X * p.Y;
            sxz += p.X * z; syz += p.Y * z; sz += z;
        }
        // 解 3x3 正规方程 [sxx sxy sx; sxy syy sy; sx sy n] * [D E F] = -[sxz syz sz]
        double a11 = sxx, a12 = sxy, a13 = sx, a22 = syy, a23 = sy, a33 = n;
        double b1 = -sxz, b2 = -syz, b3 = -sz;
        double det = a11 * (a22 * a33 - a23 * a23) - a12 * (a12 * a33 - a23 * a13) + a13 * (a12 * a23 - a22 * a13);
        if (Math.Abs(det) < 1e-9) { cx = cy = r = 0; return false; }
        double d1 = b1 * (a22 * a33 - a23 * a23) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a23 - a22 * b3);
        double d2 = a11 * (b2 * a33 - a23 * b3) - b1 * (a12 * a33 - a23 * a13) + a13 * (a12 * b3 - b2 * a13);
        double d3 = a11 * (a22 * b3 - b2 * a23) - a12 * (a12 * b3 - b2 * a13) + b1 * (a12 * a23 - a22 * a13);
        double D = d1 / det, E = d2 / det, F = d3 / det;
        cx = -D / 2; cy = -E / 2;
        double rr = cx * cx + cy * cy - F;
        if (rr <= 0) { r = 0; return false; }
        r = Math.Sqrt(rr);
        return true;
    }


    /// <summary>半径已知的 RANSAC：随机取两点反推圆心，数一数有多少点落在这个圆上，挑最多的那个。</summary>
    static List<Point2d> RansacCircleFixedR(List<Point2d> pts, double R, double tol)
    {
        var best = new List<Point2d>();
        if (pts.Count < 6) return best;
        var rnd = new Random(12345);
        int iters = Math.Min(400, 20 * pts.Count);
        for (int it = 0; it < iters; it++)
        {
            int i = rnd.Next(pts.Count), j = rnd.Next(pts.Count);
            if (i == j) continue;
            var a = pts[i]; var b = pts[j];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-6 || d > 2 * R) continue;
            double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
            double h2 = R * R - d * d / 4;
            if (h2 <= 0) continue;
            double h = Math.Sqrt(h2);
            double px = -dy / d, py = dx / d;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                double cx = mx + sgn * h * px, cy = my + sgn * h * py;
                var inl = new List<Point2d>();
                foreach (var p in pts)
                {
                    double dd = Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
                    if (Math.Abs(dd - R) <= tol) inl.Add(p);
                }
                if (inl.Count > best.Count) best = inl;
            }
        }
        return best;
    }

    /// <summary>半径已知时只解圆心：最小化 Σ(|p-c|-R)²，用高斯-牛顿迭代几步。</summary>
    static bool FitCircleFixedR(List<Point2d> pts, double R, ref double cx, ref double cy)
    {
        if (pts.Count < 3) return false;
        double mx = 0, my = 0;
        foreach (var p in pts) { mx += p.X; my += p.Y; }
        cx = mx / pts.Count; cy = my / pts.Count;
        for (int it = 0; it < 20; it++)
        {
            double a11 = 0, a12 = 0, a22 = 0, b1 = 0, b2 = 0;
            foreach (var p in pts)
            {
                double dx = p.X - cx, dy = p.Y - cy;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 1e-6) continue;
                double jx = -dx / d, jy = -dy / d;      // ∂e/∂c
                double e = d - R;
                a11 += jx * jx; a12 += jx * jy; a22 += jy * jy;
                b1 -= jx * e; b2 -= jy * e;
            }
            double det = a11 * a22 - a12 * a12;
            if (Math.Abs(det) < 1e-12) return false;
            double dxc = (b1 * a22 - b2 * a12) / det;
            double dyc = (a11 * b2 - a12 * b1) / det;
            cx += dxc; cy += dyc;
            if (Math.Abs(dxc) + Math.Abs(dyc) < 1e-5) break;
        }
        return true;
    }

    // ---------- 稳健直线拟合（RANSAC 找主直线 + PCA + Tukey IRLS） ----------
    static bool FitLineRobust(List<Point2d> pts, out double px, out double py, out double dx, out double dy, out int inliers, out double rms)
    {
        px = py = 0; dx = 1; dy = 0; inliers = 0; rms = 0;
        if (pts.Count < 4) return false;

        var alive = new List<Point2d>(pts);

        for (int it = 0; it < 10; it++)
        {
            if (alive.Count < 4) return false;
            if (!FitLinePca(alive, out px, out py, out dx, out dy)) return false;
            var res = new List<double>(alive.Count);
            foreach (var p in alive) res.Add((p.X - px) * dy - (p.Y - py) * dx);
            var abs = new List<double>(res.Count);
            foreach (var v in res) abs.Add(Math.Abs(v));
            abs.Sort();
            double med = abs[abs.Count / 2];
            double mad = 0;
            foreach (var v in abs) mad += Math.Abs(v - med);
            mad /= abs.Count;
            double sigma = Math.Max(0.15, 1.4826 * mad);
            double c = TukeyC * sigma;
            var next = new List<Point2d>();
            for (int i = 0; i < alive.Count; i++)
                if (Math.Abs(res[i]) < c) next.Add(alive[i]);
            inliers = next.Count;
            if (next.Count == alive.Count) { alive = next; break; }
            if (next.Count < 4) break;
            alive = next;
        }
        if (!FitLinePca(alive, out px, out py, out dx, out dy)) return false;
        double s2 = 0; foreach (var p in alive) { double d = (p.X - px) * dy - (p.Y - py) * dx; s2 += d * d; }
        rms = alive.Count > 0 ? Math.Sqrt(s2 / alive.Count) : 0;
        inliers = alive.Count;
        return true;
    }

    static bool FitLinePca(List<Point2d> pts, out double px, out double py, out double dx, out double dy)
    {
        px = py = 0; dx = 1; dy = 0;
        if (pts.Count < 2) return false;
        double mx = 0, my = 0;
        foreach (var p in pts) { mx += p.X; my += p.Y; }
        mx /= pts.Count; my /= pts.Count;
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var p in pts) { double ax = p.X - mx, ay = p.Y - my; sxx += ax * ax; syy += ay * ay; sxy += ax * ay; }
        double theta = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        px = mx; py = my; dx = Math.Cos(theta); dy = Math.Sin(theta);
        return true;
    }

    static double Rms(List<Point2d> pts, double cx, double cy, double r)
    {
        if (pts.Count == 0) return 0;
        double s = 0;
        foreach (var p in pts)
        {
            double d = Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)) - r;
            s += d * d;
        }
        return Math.Sqrt(s / pts.Count);
    }

    static double Coverage(List<Point2d> pts, double cx, double cy)
    {
        if (pts.Count < 2) return 0;
        var angs = new List<double>(pts.Count);
        foreach (var p in pts) angs.Add(Math.Atan2(p.Y - cy, p.X - cx));
        angs.Sort();
        double maxGap = 0;
        for (int i = 0; i < angs.Count; i++)
        {
            double a = angs[i], b = i + 1 < angs.Count ? angs[i + 1] : angs[0] + 2 * Math.PI;
            maxGap = Math.Max(maxGap, b - a);
        }
        return Math.Max(0, 360 - maxGap * 180 / Math.PI);
    }

    static Rect Clamp(Rect r, int w, int h)
    {
        int x = Math.Clamp(r.X, 0, Math.Max(0, w - 4));
        int y = Math.Clamp(r.Y, 0, Math.Max(0, h - 4));
        int ww = Math.Clamp(r.Width, 4, w - x);
        int hh = Math.Clamp(r.Height, 4, h - y);
        return new Rect(x, y, ww, hh);
    }

    static Mat ToGray(Mat src)
    {
        var g = new Mat();
        if (src.Channels() == 1) src.CopyTo(g);
        else if (src.Channels() == 4) Cv2.CvtColor(src, g, ColorConversionCodes.BGRA2GRAY);
        else Cv2.CvtColor(src, g, ColorConversionCodes.BGR2GRAY);
        return g;
    }

    static Mat DrawCircle(Mat src, CircleProResult r, List<Point2d> pts, bool withPoints = false)
    {
        var bgr = new Mat();
        if (src.Channels() == 1) Cv2.CvtColor(src, bgr, ColorConversionCodes.GRAY2BGR); else src.CopyTo(bgr);
        var green = new Scalar(60, 230, 60);
        Cv2.Circle(bgr, new Point((int)Math.Round(r.Cx), (int)Math.Round(r.Cy)), Math.Max(1, (int)Math.Round(r.R)), green, 2);
        Cv2.DrawMarker(bgr, new Point((int)Math.Round(r.Cx), (int)Math.Round(r.Cy)), new Scalar(0, 0, 255), MarkerTypes.Cross, 24, 2);
        foreach (var p in pts) Cv2.Circle(bgr, new Point((int)Math.Round(p.X), (int)Math.Round(p.Y)), 2, new Scalar(255, 160, 0), -1);
        return bgr;
    }

    static Mat DrawLine(Mat src, LineProResult r, List<Point2d> pts)
    {
        var bgr = new Mat();
        if (src.Channels() == 1) Cv2.CvtColor(src, bgr, ColorConversionCodes.GRAY2BGR); else src.CopyTo(bgr);
        var green = new Scalar(60, 230, 60);
        double L = Math.Max(bgr.Width, bgr.Height);
        var p1 = new Point((int)Math.Round(r.Px - r.Dx * L), (int)Math.Round(r.Py - r.Dy * L));
        var p2 = new Point((int)Math.Round(r.Px + r.Dx * L), (int)Math.Round(r.Py + r.Dy * L));
        Cv2.Line(bgr, p1, p2, green, 2);
        foreach (var p in pts) Cv2.Circle(bgr, new Point((int)Math.Round(p.X), (int)Math.Round(p.Y)), 2, new Scalar(255, 160, 0), -1);
        return bgr;
    }
}
