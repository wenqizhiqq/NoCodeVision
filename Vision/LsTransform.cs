// 移植自 LvStudioNi/_recon 逆向成果（lvMeasure/lvBlob/lvHistogram/lvTransform/lvBaseApi），实现：温启志
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>
/// LSTransform 像素坐标↔世界坐标标定的 C# 复刻。
/// 蓝本：lvTransform.dll.decompiled.c（归档于 _recon/decompiled/），导出类 LSVision::LSTransform，
/// 12 个导出函数仅围绕一个 3×3 CV_64F 单应矩阵工作：
///   cvFindHomography(图像点→世界点, method=0 普通最小二乘, maxIters=2000) 标定，
///   cvPerspectiveTransform 做坐标映射。
/// 行为要点（与原 DLL 对齐）：
///   - 每类点最多 128 个（0x80），超出截断；
///   - calcTransform：点数 ≤3 返回 -1，两组点数不等返回 -2，成功返回 cvFindHomography 结果(1)；
///   - setTransformData 仅接受 n==9 的行优先 3×3 double，否则静默忽略；
///   - getTransformData 返回 9 个 double（行优先）；
///   - getWorldCoordinate(double,double,...) 会把输入先截断为 float 再运算（原 DLL 精度行为）；
///   - 矩阵初始态为单位阵。
/// </summary>
public sealed class LsTransform
{
    private const int MaxPoints = 128;   // 原 DLL 内部缓冲 0x80

    private readonly List<Point2f> _imagePoints = new();
    private readonly List<Point2f> _worldPoints = new();

    /// <summary>3×3 单应矩阵，行优先；初始为单位阵。</summary>
    private double[,] _h = Identity();

    private static double[,] Identity()
    {
        var h = new double[3, 3];
        for (int i = 0; i < 3; i++) h[i, i] = 1.0;
        return h;
    }

    /// <summary>流程算子共用的标定实例。</summary>
    public static LsTransform Shared { get; } = new();

    public int ImagePointCount => _imagePoints.Count;
    public int WorldPointCount => _worldPoints.Count;

    /// <summary>添加像素坐标标定点（原 addImagePoints，超 128 截断）。</summary>
    public void AddImagePoints(IEnumerable<Point2f> pts)
    {
        foreach (var p in pts)
        {
            if (_imagePoints.Count >= MaxPoints) break;
            _imagePoints.Add(p);
        }
    }

    /// <summary>添加世界坐标标定点（原 addWorldPoints，超 128 截断）。</summary>
    public void AddWorldPoints(IEnumerable<Point2f> pts)
    {
        foreach (var p in pts)
        {
            if (_worldPoints.Count >= MaxPoints) break;
            _worldPoints.Add(p);
        }
    }

    public void ClearPoints()
    {
        _imagePoints.Clear();
        _worldPoints.Clear();
    }

    /// <summary>
    /// 由已添加的点对计算单应矩阵（原 calcTransform）。
    /// 返回：-1 点数不足（需 ≥4 对）、-2 两组点数不等、1 成功（与 cvFindHomography 返回值一致）。
    /// 原 DLL 的 calcTransform(int) 参数在函数体中从未使用，这里不保留。
    /// </summary>
    public int CalcTransform()
    {
        if (_imagePoints.Count <= 3 || _worldPoints.Count <= 3) return -1;
        if (_imagePoints.Count != _worldPoints.Count) return -2;

        // cvFindHomography(src=image, dst=world, method=0, maxIters=2000)
        using var h = Cv2.FindHomography(
            _imagePoints.Select(p => new Point2d(p.X, p.Y)),
            _worldPoints.Select(p => new Point2d(p.X, p.Y)),
            HomographyMethods.None, 3.0, null, 2000, 0.995);
        if (h.Empty()) return 0;

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                _h[r, c] = h.At<double>(r, c);
        return 1;
    }

    /// <summary>图像坐标 → 世界坐标（原 getWorldCoordinate(float,float)，cvPerspectiveTransform）。</summary>
    public Point2d GetWorldCoordinate(float x, float y) => Apply(_h, x, y);

    /// <summary>双精度重载：原 DLL 会把输入先截断为 float 再运算，这里保持一致。</summary>
    public Point2d GetWorldCoordinate(double x, double y) => Apply(_h, (float)x, (float)y);

    private static Point2d Apply(double[,] h, float x, float y)
    {
        double w = h[2, 0] * x + h[2, 1] * y + h[2, 2];
        if (Math.Abs(w) < 1e-12) return new Point2d(0, 0);
        double px = (h[0, 0] * x + h[0, 1] * y + h[0, 2]) / w;
        double py = (h[1, 0] * x + h[1, 1] * y + h[1, 2]) / w;
        return new Point2d(px, py);
    }

    /// <summary>
    /// 图像里的方向角（度）换算成机台方向角：把方向向量套进矩阵的线性部分。
    /// 标定没做过或算出来是零向量就原样返回。
    /// </summary>
    public double GetWorldAngle(double angleDeg)
    {
        double a = angleDeg * Math.PI / 180.0;
        double dx = Math.Cos(a), dy = Math.Sin(a);
        double wx = _h[0, 0] * dx + _h[0, 1] * dy;
        double wy = _h[1, 0] * dx + _h[1, 1] * dy;
        if (Math.Abs(wx) < 1e-9 && Math.Abs(wy) < 1e-9) return angleDeg;
        return Math.Atan2(wy, wx) * 180.0 / Math.PI;
    }

    /// <summary>取 3×3 矩阵数据，行优先 9 个 double（原 getTransformData）。</summary>
    public double[] GetTransformData()
    {
        var d = new double[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                d[r * 3 + c] = _h[r, c];
        return d;
    }

    /// <summary>写入 3×3 矩阵数据，仅接受 9 个 double（原 setTransformData，否则静默忽略）。</summary>
    public void SetTransformData(double[] data)
    {
        if (data == null || data.Length != 9) return;   // 原 DLL：n != 9 直接忽略
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                _h[r, c] = data[r * 3 + c];
    }

    /// <summary>所有标定点对的最大重投影误差（像素）。</summary>
    public double ReprojectRms()
    {
        int n = Math.Min(_imagePoints.Count, _worldPoints.Count);
        if (n == 0) return 0;
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            var w = Apply(_h, _imagePoints[i].X, _imagePoints[i].Y);
            double dx = w.X - _worldPoints[i].X, dy = w.Y - _worldPoints[i].Y;
            sum += dx * dx + dy * dy;
        }
        return Math.Sqrt(sum / n);
    }

    // ---------------- 流程内共享的标定结果 ----------------

    /// <summary>最近一次标定得到的 3×3 单应（行优先 9 元素），供坐标转换等后续工具使用。</summary>
    public static double[]? LastHomography { get; private set; }

    /// <summary>最近一次标定的来源描述（如 "N点标定 9点"）。</summary>
    public static string LastCalibSource { get; private set; } = "";

    /// <summary>记录一次成功标定，供同流程后续工具（坐标转换）取用。</summary>
    public static void Publish(double[] h9, string source)
    {
        LastHomography = (double[])h9.Clone();
        LastCalibSource = source;
    }
}
