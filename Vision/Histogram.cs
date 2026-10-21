// 移植自 LvStudioNi/_recon 逆向成果（lvMeasure/lvBlob/lvHistogram/lvTransform/lvBaseApi），实现：温启志
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>
/// 对应 lvHistogram.dll 导出的 DipperVision::CHistogram。
///   getHistogram(const IplImage*, bool)  —— 累计直方图开关
///   getHistValue(int) / getIntHistValue(int) / getIntergelHist()
///   getAverageValue() / getMedianValue() / getMinMaxHistValue(int&amp;,int&amp;,int&amp;,int&amp;)
/// 手册对应 6.5 节的灰度直方图工具（均支持 ROI 区域裁剪）。
/// </summary>
public sealed class Histogram
{
    static readonly Mat NoMask = new();

    private readonly Mat _src = new();
    private readonly Mat _hist = new();
    private readonly int _bins = 256;
    private readonly int[] _histSize = { 256 };
    private readonly Rangef[] _ranges = { new Rangef(0, 256) };
    private bool _cumulative;

    /// <summary>InitProcess(int w, int h)。</summary>
    public void InitProcess(int w, int h) => _src.Create(h, w, MatType.CV_8UC1);

    public void ResetRegion() { /* ROI 清空，实现里由调用方传掩膜 */ }

    /// <summary>getHistogram：按给定掩膜（或全图）计算直方图。</summary>
    public void GetHistogram(Mat gray, bool cumulative, Mat? mask = null)
    {
        _cumulative = cumulative;
        Cv2.CalcHist(new[] { gray }, new[] { 0 }, mask ?? NoMask, _hist, 1,
            _histSize, _ranges);
        if (cumulative)
        {
            // 累计直方图
            for (int i = 1; i < _bins; i++)
                _hist.At<float>(i) += _hist.At<float>(i - 1);
        }
    }

    /// <summary>getIntergelHist：积分直方图别名。</summary>
    public void GetIntergelHist(Mat gray, Mat? mask = null) => GetHistogram(gray, true, mask);

    /// <summary>getHistValue(int idx)：指定灰度级的像素数。</summary>
    public int GetHistValue(int idx)
        => idx < 0 || idx >= _bins || _hist.Empty() ? 0 : (int)_hist.At<float>(idx);

    /// <summary>getIntHistValue(int idx)：累计到该灰度级的像素总数。</summary>
    public int GetIntHistValue(int idx)
    {
        if (idx < 0 || idx >= _bins || _hist.Empty()) return 0;
        float sum = 0;
        for (int i = 0; i <= idx; i++) sum += _hist.At<float>(i);
        return (int)sum;
    }

    /// <summary>getAverageValue：ROI 内平均灰度。</summary>
    public double GetAverageValue(Mat gray, Mat? mask = null)
        => Cv2.Mean(gray, mask ?? NoMask).Val0;

    /// <summary>getMedianValue：ROI 内灰度中值。</summary>
    public int GetMedianValue(Mat gray, Mat? mask = null)
    {
        // 注意：绝不能对入参 gray 套 using——掩膜为空时 src 就是 gray 本身，
        // using 会在返回时把调用方的图释放掉，之后任何访问都是野指针。
        Mat? owned = null;
        var src = gray;
        if (mask != null)
        {
            owned = new Mat();
            Cv2.BitwiseAnd(gray, gray, owned, mask);
            src = owned;
        }
        int total = 0;
        for (int i = 0; i < _bins; i++) total += GetHistValue(i);
        if (total == 0) { owned?.Dispose(); return 0; }
        int half = total / 2, acc = 0;
        for (int i = 0; i < _bins; i++)
        {
            acc += GetHistValue(i);
            if (acc >= half) { owned?.Dispose(); return i; }
        }
        owned?.Dispose();
        return _bins - 1;
    }

    /// <summary>
    /// getMinMaxHistValue(int&amp; min, int&amp; max, int&amp; argMin, int&amp; argMax)：
    /// 返回最小/最大频次及其灰度级。
    /// </summary>
    public (int min, int max, int argMin, int argMax) GetMinMaxHistValue()
    {
        int mn = int.MaxValue, mx = 0, amn = 0, amx = 0;
        for (int i = 0; i < _bins; i++)
        {
            int v = GetHistValue(i);
            if (v < mn) { mn = v; amn = i; }
            if (v > mx) { mx = v; amx = i; }
        }
        return (mn == int.MaxValue ? 0 : mn, mx, amn, amx);
    }

    /// <summary>整个直方图（供 UI 画曲线）。</summary>
    public int[] ToArray()
    {
        var a = new int[_bins];
        for (int i = 0; i < _bins; i++) a[i] = GetHistValue(i);
        return a;
    }

    public bool IsCumulative => _cumulative;
    public int Bins => _bins;
}

/// <summary>
/// 对应 lvBaseFunc.dll 的 DipperVision::CShadingCorrection（平场/阴影校正）。
///   InitProcess(int w, int h)
///   ShadingCorrection(IplImage* src, IplImage* dst, int, int, int, bool)
///   GenerateRegion(点集/CvRect/_dvCircle/_dvEllipse/_dvRect64d/_dvRing/_dvRotRect, bool)
///   GetAverageValue() / ResetRegion(bool)
/// </summary>
public static class ShadingCorrection
{
    /// <summary>
    /// 平场校正：用参考平场图（全白/全亮图）的灰度分布逐像素归一化输入图。
    /// meanTarget = 0 时用参考图自身均值。
    /// </summary>
    public static Mat Correct(Mat src, Mat? flatField, double meanTarget = 0, int smooth = 0)
    {
        if (flatField == null || flatField.Empty())
            return src.Clone();

        // 平场图先做可选高斯平滑，抑制参考图自身噪声
        using var ffRaw = smooth > 1 ? new Mat() : flatField.Clone();
        if (smooth > 1) Cv2.GaussianBlur(flatField, ffRaw, new Size(smooth, smooth), 0);

        // 增益 = 目标均值 / 参考图均值，逐像素相乘
        double refMean = Cv2.Mean(ffRaw).Val0;
        if (refMean < 1e-6) return src.Clone();
        double gain = (meanTarget > 0 ? meanTarget : refMean) / refMean;

        using var ff = new Mat();
        ffRaw.ConvertTo(ff, MatType.CV_32F, gain);
        using var sf = new Mat();
        src.ConvertTo(sf, MatType.CV_32F);
        var dst = new Mat();
        Cv2.Multiply(sf, ff, dst);
        dst.ConvertTo(dst, MatType.CV_8UC1);
        return dst;
    }

}
