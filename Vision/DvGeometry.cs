// 移植自 LvStudioNi/_recon 逆向成果（lvMeasure/lvBlob/lvHistogram/lvTransform/lvBaseApi），实现：温启志
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>
/// 对应 lvBaseApi.dll 导出的 DipperVision 几何层。
/// 原程序用 _dvArc / _dvCircle / _dvRect64d / _dvRotRect / _dvRing / _dvLine /
/// _dvVector2D / _dvAffineTrans 这组值类型承载 ROI 与变换，再由
/// dvAffineTransform / dvGetPoint2LineDistance 等 16 个自由函数做运算。
/// 这里按同名同义复刻（C# 记录类型 + 静态方法）。
/// </summary>

/// <summary>二维点（对应 CvPoint2D32f / CvPoint2D64f）。</summary>
public readonly record struct DvPoint2D(double X, double Y)
{
    public static readonly DvPoint2D Empty = new(0, 0);
    public double DistTo(DvPoint2D o) => Math.Sqrt((X - o.X) * (X - o.X) + (Y - o.Y) * (Y - o.Y));
    public Point2d ToCv() => new(X, Y);
    public override string ToString() => $"({X:0.###}, {Y:0.###})";
}

/// <summary>二维向量。</summary>
public readonly record struct DvVector2D(double X, double Y)
{
    public double Length => Math.Sqrt(X * X + Y * Y);
    public DvVector2D Normalized()
    {
        double l = Length;
        return l < 1e-12 ? new DvVector2D(0, 0) : new DvVector2D(X / l, Y / l);
    }
    public double Dot(DvVector2D o) => X * o.X + Y * o.Y;
}

/// <summary>有向直线：过 P0、方向 Dir（原程序 _dvLine）。</summary>
public readonly record struct DvLine(DvPoint2D P0, DvVector2D Dir)
{
    public static DvLine FromTwoPoints(DvPoint2D a, DvPoint2D b)
        => new(a, new DvVector2D(b.X - a.X, b.Y - a.Y).Normalized());
}

/// <summary>圆（_dvCircle）。</summary>
public readonly record struct DvCircle(DvPoint2D Center, double Radius);

/// <summary>圆弧 / 圆环段（_dvArc / _dvRing）。</summary>
public readonly record struct DvRing(DvCircle Outer, DvCircle Inner)
{
    public DvPoint2D Center => Outer.Center;
    public double OuterRadius => Outer.Radius;
    public double InnerRadius => Inner.Radius;
    /// <summary>圆环宽度 = 卡尺搜索长度（手册：检测圆环宽度）。</summary>
    public double Width => Outer.Radius - Inner.Radius;

    public static DvRing FromCenterR(DvPoint2D c, double rOuter, double rInner)
        => new(new DvCircle(c, rOuter), new DvCircle(c, rInner));

    /// <summary>第 i 个卡尺测量线的起点（外端）与方向（指向圆心）。</summary>
    public (DvPoint2D start, DvVector2D dir) Caliper(int i, int count, bool fromOutside)
    {
        double a = count <= 1 ? -Math.PI / 2 : -Math.PI / 2 + 2 * Math.PI * i / count;
        var u = new DvVector2D(Math.Cos(a), Math.Sin(a));
        double r = fromOutside ? OuterRadius : InnerRadius;
        var p = new DvPoint2D(Center.X + u.X * r, Center.Y + u.Y * r);
        var toCenter = new DvVector2D(-u.X, -u.Y);
        return (p, fromOutside ? toCenter : u);
    }
}

/// <summary>
/// 圆弧 ROI（原程序 <c>_dvArc</c>，0x28 字节 = 5 个 double）。
/// 字段顺序与反汇编一致：<c>+0x00</c> cx、<c>+0x08</c> cy、<c>+0x10</c> r、
/// <c>+0x18</c> startAngle（度）、<c>+0x20</c> arcSpan（度）。
/// <c>CMeasureCircle::process</c>（<c>sub_7050</c>）直接用 <c>[arc+0x18]</c> / <c>[arc+0x20]</c>。
/// </summary>
public readonly record struct DvArc(DvPoint2D Center, double Radius, double StartAngleDeg, double SpanDeg)
{
    public static DvArc FullCircle(DvPoint2D c, double r) => new(c, r, 0, 360);
}

/// <summary>正矩形（_dvRect64d：x/y/w/h，与流程文件 geometry 字段同名同义）。</summary>
public readonly record struct DvRect64d(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public DvPoint2D Center => new(X + Width / 2, Y + Height / 2);
    public double Area => Math.Max(0, Width) * Math.Max(0, Height);
    public bool Contains(DvPoint2D p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;
}

/// <summary>旋转矩形（_dvRotRect：中心 + 长宽 + 角度）。</summary>
public readonly record struct DvRotRect(DvPoint2D Center, double Width, double Height, double AngleRad)
{
    /// <summary>原程序 <c>_dvRotRect.angle</c> 的单位是<b>度</b>（反编译 <c>sub_85B0</c>：
    /// <c>rad = angle*π/180</c>）。</summary>
    public static DvRotRect FromDeg(DvPoint2D center, double width, double height, double angleDeg)
        => new(center, width, height, angleDeg * Math.PI / 180.0);

    /// <summary>宽度方向单位向量 û = (cos, sin)。</summary>
    public DvVector2D UnitX() => new(Math.Cos(AngleRad), Math.Sin(AngleRad));

    /// <summary>高度方向单位向量 n̂ = (−sin, cos)。</summary>
    public DvVector2D UnitY() => new(-Math.Sin(AngleRad), Math.Cos(AngleRad));

    public Point2d[] Corners()
    {
        double c = Math.Cos(AngleRad), s = Math.Sin(AngleRad);
        var pts = new[]
        {
            (-Width / 2, -Height / 2), (Width / 2, -Height / 2),
            (Width / 2, Height / 2), (-Width / 2, Height / 2)
        };
        var r = new Point2d[4];
        for (int i = 0; i < 4; i++)
            r[i] = new Point2d(Center.X + pts[i].Item1 * c - pts[i].Item2 * s,
                               Center.Y + pts[i].Item1 * s + pts[i].Item2 * c);
        return r;
    }
    public bool Contains(DvPoint2D p)
    {
        double c = Math.Cos(-AngleRad), s = Math.Sin(-AngleRad);
        double dx = p.X - Center.X, dy = p.Y - Center.Y;
        double lx = dx * c - dy * s, ly = dx * s + dy * c;
        return Math.Abs(lx) <= Width / 2 && Math.Abs(ly) <= Height / 2;
    }
}

/// <summary>椭圆（_dvEllipse）。</summary>
public readonly record struct DvEllipse(DvPoint2D Center, double A, double B, double AngleRad);

/// <summary>仿射变换（_dvAffineTrans：平移 + 旋转 + 缩放）。</summary>
public readonly record struct DvAffineTrans(double Tx, double Ty, double RotRad, double Scale)
{
    public static readonly DvAffineTrans Identity = new(0, 0, 0, 1);

    /// <summary>2x3 矩阵，行主序，可直接喂给 OpenCvSharp.WarpAffine。</summary>
    public Mat ToMat()
    {
        double c = Math.Cos(RotRad) * Scale, s = Math.Sin(RotRad) * Scale;
        // OpenCV 的 setTo 需要 float[2,3]
        var m = new Mat(2, 3, MatType.CV_64FC1);
        m.Set(0, 0, c); m.Set(0, 1, -s); m.Set(0, 2, Tx);
        m.Set(1, 0, s); m.Set(1, 1, c);  m.Set(1, 2, Ty);
        return m;
    }

    public DvPoint2D Apply(DvPoint2D p)
    {
        double c = Math.Cos(RotRad) * Scale, s = Math.Sin(RotRad) * Scale;
        return new DvPoint2D(p.X * c - p.Y * s + Tx, p.X * s + p.Y * c + Ty);
    }

    public DvAffineTrans Compose(DvAffineTrans after)
    {
        // this ∘ after：先做 after 再做 this
        double c1 = Math.Cos(RotRad), s1 = Math.Sin(RotRad);
        double c2 = Math.Cos(after.RotRad), s2 = Math.Sin(after.RotRad);
        double rot = RotRad + after.RotRad;
        double scale = Scale * after.Scale;
        double tx = Tx + after.Tx * c1 * Scale - after.Ty * s1 * Scale;
        double ty = Ty + after.Tx * s1 * Scale + after.Ty * c1 * Scale;
        return new DvAffineTrans(tx, ty, rot, scale);
    }

    public DvAffineTrans Invert()
    {
        double c = Math.Cos(RotRad) * Scale, s = Math.Sin(RotRad) * Scale;
        double det = c * c + s * s;
        if (Math.Abs(det) < 1e-12) return Identity;
        double a = c / det, b = s / det;
        return new DvAffineTrans(-a * Tx - b * Ty, b * Tx - a * Ty, -RotRad, 1.0 / Scale);
    }
}

/// <summary>匹配结果（_dvMatchResult，手册 6.4.1 结果参数页逐字段对应）。</summary>
public sealed record DvMatchResult
{
    public double Score { get; init; }
    public DvPoint2D Center { get; init; }
    public double ThetaRad { get; init; }
    public double Scale { get; init; } = 1.0;
    public double ThetaDeg => ThetaRad * 180.0 / Math.PI;
}

/// <summary>Blob 结果（_dvBlobResult，手册 6.4.6 处理结果页逐字段对应）。</summary>
public sealed record DvBlobResult
{
    public double Area { get; init; }
    public double Perimeter { get; init; }
    public DvPoint2D Centroid { get; init; }
    public DvRect64d MinRect { get; init; }
    public DvCircle MinCircle { get; init; }
    public DvVector2D Axis { get; init; }
    public int PixelCount { get; init; }
}

/// <summary>边缘点（CMeasurePoint::getEdgePt 的输出）。</summary>
public sealed record DvEdgePoint
{
    public DvPoint2D Position { get; init; }
    public double Gradient { get; init; }
    public double Gray { get; init; }
}

/// <summary>边缘极性（对应 setPolarity 的三种模式，手册"由明到暗/由暗到明/任意极性"）。</summary>
public enum DvPolarity
{
    /// <summary>由暗到明：暗 → 亮 的上升沿。</summary>
    DarkToBright = 0,
    /// <summary>由明到暗：亮 → 暗 的下降沿。</summary>
    BrightToDark = 1,
    /// <summary>任意极性：两种都检。</summary>
    Any = 2,
}

/// <summary>拟合方式（手册"标准代数最小二乘/正交距离最小二乘/带权重最小二乘"）。</summary>
public enum DvFitMethod
{
    /// <summary>标准代数最小二乘。</summary>
    AlgebraicLeastSquares = 0,
    /// <summary>基于正交距离最小二乘（几何拟合，更准）。</summary>
    OrthogonalLeastSquares = 1,
    /// <summary>带权重的最小二乘（按梯度幅值加权）。</summary>
    WeightedLeastSquares = 2,
}

/// <summary>匹配极性（_eDvMatchPolar：忽略极性开关）。</summary>
public enum DvMatchPolar { Same = 0, Ignore = 1 }

/// <summary>排序方式（手册 6.4.6 过滤参数页"按面积/轮廓/坐标的升序或降序"）。</summary>
public enum DvBlobSort
{
    None = 0,
    AreaAsc = 1, AreaDesc = 2,
    PerimeterAsc = 3, PerimeterDesc = 4,
    XAsc = 5, XDesc = 6,
    YAsc = 7, YDesc = 8,
}

/// <summary>lvBaseApi.dll 导出的 16 个自由函数，按原签名复刻。</summary>
public static class DvGeometry
{
    /// <summary>点绕中心旋转 angleRad（dvRotate2DPoint）。</summary>
    public static DvPoint2D Rotate2DPoint(DvPoint2D p, DvPoint2D center, double angleRad)
    {
        double c = Math.Cos(angleRad), s = Math.Sin(angleRad);
        double dx = p.X - center.X, dy = p.Y - center.Y;
        return new DvPoint2D(center.X + dx * c - dy * s, center.Y + dx * s + dy * c);
    }

    /// <summary>向量旋转（dvRotate2DVector）。</summary>
    public static DvVector2D Rotate2DVector(DvVector2D v, double angleRad)
    {
        double c = Math.Cos(angleRad), s = Math.Sin(angleRad);
        return new DvVector2D(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    /// <summary>点到直线距离（dvGetPoint2LineDistance）。</summary>
    public static double Point2LineDistance(DvPoint2D p, DvLine line)
    {
        var n = new DvVector2D(-line.Dir.Y, line.Dir.X);   // 法向
        double len = n.Length;
        if (len < 1e-12) return p.DistTo(line.P0);
        n = n.Normalized();
        return Math.Abs((p.X - line.P0.X) * n.X + (p.Y - line.P0.Y) * n.Y);
    }

    /// <summary>两直线间距离（dvGetLine2LineDistance）：平行取垂距，相交取 0。</summary>
    public static double Line2LineDistance(DvLine a, DvLine b)
    {
        double cross = a.Dir.X * b.Dir.Y - a.Dir.Y * b.Dir.X;
        if (Math.Abs(cross) < 1e-12) return Point2LineDistance(b.P0, a);
        return 0.0;
    }

    /// <summary>两直线交点（dvGet2LineIPoint）。</summary>
    public static bool Get2LineIPoint(DvLine a, DvLine b, out DvPoint2D ip)
    {
        double d = a.Dir.X * b.Dir.Y - a.Dir.Y * b.Dir.X;
        if (Math.Abs(d) < 1e-12) { ip = DvPoint2D.Empty; return false; }
        double dx = b.P0.X - a.P0.X, dy = b.P0.Y - a.P0.Y;
        double t = (dx * b.Dir.Y - dy * b.Dir.X) / d;
        ip = new DvPoint2D(a.P0.X + a.Dir.X * t, a.P0.Y + a.Dir.Y * t);
        return true;
    }

    /// <summary>对点集做仿射（dvAffineTransform 的 Point2D 重载）。</summary>
    public static DvPoint2D AffineTransform(DvPoint2D p, DvAffineTrans t) => t.Apply(p);

    public static DvCircle AffineTransform(DvCircle c, DvAffineTrans t)
        => new(t.Apply(c.Center), c.Radius * t.Scale);

    public static DvRing AffineTransform(DvRing r, DvAffineTrans t)
        => new(AffineTransform(r.Outer, t), AffineTransform(r.Inner, t));

    public static DvRotRect AffineTransform(DvRotRect r, DvAffineTrans t)
        => new(t.Apply(r.Center), r.Width * t.Scale, r.Height * t.Scale, r.AngleRad + t.RotRad);

    public static DvRect64d AffineTransform(DvRect64d r, DvAffineTrans t)
        => DvRotRectExt.Bounds(AffineTransform(new DvRotRect(r.Center, r.Width, r.Height, 0), t));

    /// <summary>仿射逆变换（dvInvAffineTrans）。</summary>
    public static DvPoint2D InvAffineTrans(DvPoint2D p, DvAffineTrans t) => t.Invert().Apply(p);

    public static DvCircle InvAffineTrans(DvCircle c, DvAffineTrans t)
    {
        var inv = t.Invert();
        return new DvCircle(inv.Apply(c.Center), c.Radius / t.Scale);
    }

    public static DvRing InvAffineTrans(DvRing r, DvAffineTrans t)
        => new(InvAffineTrans(r.Outer, t), InvAffineTrans(r.Inner, t));

    public static DvRotRect InvAffineTrans(DvRotRect r, DvAffineTrans t)
    {
        var inv = t.Invert();
        return new DvRotRect(inv.Apply(r.Center), r.Width / t.Scale, r.Height / t.Scale, r.AngleRad - t.RotRad);
    }

    /// <summary>最小二乘拟合圆（代数法，对应"标准代数最小二乘"）。</summary>
    public static DvCircle FitCircleAlgebraic(IReadOnlyList<DvPoint2D> pts)
    {
        int n = pts.Count;
        if (n < 3) return new DvCircle(DvPoint2D.Empty, 0);

        double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0, sz = 0, szx = 0, szy = 0;
        double mx = 0, my = 0;
        foreach (var p in pts) { mx += p.X; my += p.Y; }
        mx /= n; my /= n;

        foreach (var p in pts)
        {
            double x = p.X - mx, y = p.Y - my, z = x * x + y * y;
            sx += x; sy += y; sxx += x * x; syy += y * y; sxy += x * y;
            sz += z; szx += z * x; szy += z * y;
        }
        // 解 [sxx sxy; sxy syy][a;b] = [szx; szy] / 2
        double det = sxx * syy - sxy * sxy;
        if (Math.Abs(det) < 1e-12) return new DvCircle(new DvPoint2D(mx, my), 0);
        double a = (szx * syy - szy * sxy) / (2 * det);
        double b = (szy * sxx - szx * sxy) / (2 * det);
        double cx = a + mx, cy = b + my;
        double r2 = sz / n + a * a + b * b;
        return new DvCircle(new DvPoint2D(cx, cy), Math.Sqrt(Math.Max(0, r2)));
    }

    /// <summary>
    /// 标准代数最小二乘拟合圆 —— 逐位对齐 <c>lvMeasure!sub_4AF0</c>（3009 B）。
    ///
    /// <para>反汇编里的正规方程（Kasa 拟合，<b>不先做中心化</b>，与 <see cref="FitCircleAlgebraic"/>
    /// 的中心化版本在精确算术下等价、浮点上略有差异）：</para>
    /// <code>
    /// A = [[Σx², Σxy, Σx],
    ///      [Σxy, Σy², Σy],
    ///      [Σx,  Σy,  n ]]
    /// b = −[Σx(x²+y²), Σy(x²+y²), Σ(x²+y²)]
    /// cv::solve(A, b, x, DECOMP_SVD)         // 反汇编里 r9d = 1
    /// 圆心 = (−x₀/2, −x₁/2)                   // 常量 −0.5 @ 0xDC60
    /// 半径 = 0.5·√(x₀² + x₁² − 4·x₂)          // 常量 4.0 @ 0xDC38、0.5 @ 0xDA60
    /// </code>
    /// </summary>
    public static DvCircle FitCircleKasa(IReadOnlyList<DvPoint2D> pts)
    {
        int n = pts.Count;
        if (n < 3) return new DvCircle(DvPoint2D.Empty, 0);

        double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
        double sxz = 0, syz = 0, sz = 0;
        foreach (var p in pts)
        {
            double x = p.X, y = p.Y;
            double z = x * x + y * y;
            sx += x; sy += y;
            sxx += x * x; syy += y * y; sxy += x * y;
            sxz += x * z; syz += y * z; sz += z;
        }

        // A x = b（b 取负号，与反汇编一致）
        var a = new[,]
        {
            { sxx, sxy, sx },
            { sxy, syy, sy },
            { sx,  sy,  (double)n },
        };
        var b = new[] { -sxz, -syz, -sz };
        if (!Solve3(a, b, out var s)) return new DvCircle(DvPoint2D.Empty, 0);

        double cx = -0.5 * s[0];
        double cy = -0.5 * s[1];
        double r2 = s[0] * s[0] + s[1] * s[1] - 4.0 * s[2];
        return new DvCircle(new DvPoint2D(cx, cy), 0.5 * Math.Sqrt(Math.Max(0, r2)));
    }

    /// <summary>3×3 线性方程组求解（带部分主元的高斯消元；对良态 3×3 与 <c>cv::solve(…, DECOMP_SVD)</c> 同解）。</summary>
    private static bool Solve3(double[,] a, double[] b, out double[] x)
    {
        x = new double[3];
        var m = new double[3, 4];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++) m[i, j] = a[i, j];
            m[i, 3] = b[i];
        }
        for (int c = 0; c < 3; c++)
        {
            int piv = c;
            for (int r = c + 1; r < 3; r++)
                if (Math.Abs(m[r, c]) > Math.Abs(m[piv, c])) piv = r;
            if (Math.Abs(m[piv, c]) < 1e-12) return false;
            if (piv != c)
                for (int j = c; j < 4; j++) (m[c, j], m[piv, j]) = (m[piv, j], m[c, j]);
            for (int r = c + 1; r < 3; r++)
            {
                double f = m[r, c] / m[c, c];
                for (int j = c; j < 4; j++) m[r, j] -= f * m[c, j];
            }
        }
        for (int i = 2; i >= 0; i--)
        {
            double s = m[i, 3];
            for (int j = i + 1; j < 3; j++) s -= m[i, j] * x[j];
            x[i] = s / m[i, i];
        }
        return true;
    }

    /// <summary>正交距离最小二乘拟合圆（几何拟合，Levenberg-Marquardt 迭代）。</summary>
    public static DvCircle FitCircleOrthogonal(IReadOnlyList<DvPoint2D> pts, int iters = 60)
    {
        var c = FitCircleAlgebraic(pts);
        if (pts.Count < 3) return c;
        double cx = c.Center.X, cy = c.Center.Y, r = Math.Max(c.Radius, 1.0);
        for (int it = 0; it < iters; it++)
        {
            double s11 = 0, s12 = 0, s22 = 0, b1 = 0, b2 = 0;
            foreach (var p in pts)
            {
                double dx = p.X - cx, dy = p.Y - cy;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 1e-9) continue;
                double ux = dx / d, uy = dy / d;
                double res = d - r;
                s11 += ux * ux; s12 += ux * uy; s22 += uy * uy;
                b1 += res * ux; b2 += res * uy;
            }
            double det = s11 * s22 - s12 * s12;
            if (Math.Abs(det) < 1e-12) break;
            double dcx = (b1 * s22 - b2 * s12) / det;
            double dcy = (b2 * s11 - b1 * s12) / det;
            double dr = 0;
            foreach (var p in pts)
            {
                double dx = p.X - cx - dcx, dy = p.Y - cy - dcy;
                dr += Math.Sqrt(dx * dx + dy * dy) - r;
            }
            dr /= pts.Count;
            cx += dcx; cy += dcy; r += dr;
            if (Math.Abs(dcx) < 1e-9 && Math.Abs(dcy) < 1e-9 && Math.Abs(dr) < 1e-9) break;
        }
        return new DvCircle(new DvPoint2D(cx, cy), r);
    }

    /// <summary>带权重最小二乘拟合圆（按梯度幅值加权，权重大的点主导拟合）。</summary>
    public static DvCircle FitCircleWeighted(IReadOnlyList<DvEdgePoint> pts, int iters = 60)
    {
        if (pts.Count < 3) return new DvCircle(DvPoint2D.Empty, 0);
        var c = FitCircleOrthogonal(pts.Select(p => p.Position).ToList(), iters);
        double cx = c.Center.X, cy = c.Center.Y, r = Math.Max(c.Radius, 1.0);
        double wsum = pts.Sum(p => Math.Max(1e-6, p.Gradient));
        for (int it = 0; it < iters; it++)
        {
            double s11 = 0, s12 = 0, s22 = 0, b1 = 0, b2 = 0, w = 0, wr = 0;
            foreach (var p in pts)
            {
                double dx = p.Position.X - cx, dy = p.Position.Y - cy;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 1e-9) continue;
                double ux = dx / d, uy = dy / d, res = d - r;
                double ww = Math.Max(1e-6, p.Gradient) / wsum * pts.Count;
                s11 += ww * ux * ux; s12 += ww * ux * uy; s22 += ww * uy * uy;
                b1 += ww * res * ux; b2 += ww * res * uy;
            }
            double det = s11 * s22 - s12 * s12;
            if (Math.Abs(det) < 1e-12) break;
            cx += (b1 * s22 - b2 * s12) / det;
            cy += (b2 * s11 - b1 * s12) / det;
            foreach (var p in pts)
            {
                double dx = p.Position.X - cx, dy = p.Position.Y - cy;
                w += Math.Max(1e-6, p.Gradient);
                wr += Math.Max(1e-6, p.Gradient) * (Math.Sqrt(dx * dx + dy * dy) - r);
            }
            r += wr / w;
        }
        return new DvCircle(new DvPoint2D(cx, cy), r);
    }

    /// <summary>最小二乘拟合直线（带权重模式按梯度加权）。</summary>
    public static DvLine FitLineAlgebraic(IReadOnlyList<DvPoint2D> pts)
    {
        int n = pts.Count;
        if (n < 2) return new DvLine(DvPoint2D.Empty, new DvVector2D(1, 0));
        double mx = 0, my = 0;
        foreach (var p in pts) { mx += p.X; my += p.Y; }
        mx /= n; my /= n;
        double sxx = 0, sxy = 0, syy = 0;
        foreach (var p in pts)
        {
            double x = p.X - mx, y = p.Y - my;
            sxx += x * x; sxy += x * y; syy += y * y;
        }
        // 最小特征向量方向
        double tr = sxx + syy, det = sxx * syy - sxy * sxy;
        double l1 = tr / 2 + Math.Sqrt(Math.Max(0, tr * tr / 4 - det));
        double vx = sxy, vy = l1 - sxx;
        if (Math.Abs(vx) < 1e-12 && Math.Abs(vy) < 1e-12) vy = 1;
        return DvLine.FromTwoPoints(new DvPoint2D(mx, my),
                                    new DvPoint2D(mx + vx, my + vy));
    }

    public static DvLine FitLineWeighted(IReadOnlyList<DvEdgePoint> pts)
    {
        if (pts.Count < 2) return FitLineAlgebraic(pts.Select(p => p.Position).ToList());
        double w = 0;
        foreach (var p in pts) w += Math.Max(1e-6, p.Gradient);
        double mx = 0, my = 0;
        foreach (var p in pts) { var g = Math.Max(1e-6, p.Gradient) / w; mx += p.Position.X * g; my += p.Position.Y * g; }
        double sxx = 0, sxy = 0, syy = 0;
        foreach (var p in pts)
        {
            double g = Math.Max(1e-6, p.Gradient) / w;
            double x = p.Position.X - mx, y = p.Position.Y - my;
            sxx += g * x * x; sxy += g * x * y; syy += g * y * y;
        }
        double tr = sxx + syy, det = sxx * syy - sxy * sxy;
        double l1 = tr / 2 + Math.Sqrt(Math.Max(0, tr * tr / 4 - det));
        double vx = sxy, vy = l1 - sxx;
        if (Math.Abs(vx) < 1e-12 && Math.Abs(vy) < 1e-12) vy = 1;
        return DvLine.FromTwoPoints(new DvPoint2D(mx, my), new DvPoint2D(mx + vx, my + vy));
    }
}

/// <summary>DvRotRect 扩展：取轴对齐包围盒。</summary>
public static class DvRotRectExt
{
    public static DvRect64d Bounds(this DvRotRect r)
    {
        var c = r.Corners();
        double minX = c.Min(p => p.X), maxX = c.Max(p => p.X);
        double minY = c.Min(p => p.Y), maxY = c.Max(p => p.Y);
        return new DvRect64d(minX, minY, maxX - minX, maxY - minY);
    }
}
