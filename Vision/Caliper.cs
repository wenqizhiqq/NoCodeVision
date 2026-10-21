// 移植自 LvStudioNi/_recon 逆向成果（lvMeasure/lvBlob/lvHistogram/lvTransform/lvBaseApi），实现：温启志
using OpenCvSharp;

namespace NoCodeVision.Vision;

/// <summary>
/// 对应 lvMeasure.dll 导出的 CMeasureCircle / CMeasureLine / CMeasurePoint /
/// CMeasurePitch / CMeasureExtract。
///
/// <para><b>本文件的每一个步骤都逐条对齐了反汇编</b>（详见 <c>_recon/asm/lvMeasure/</c>
/// 与 <c>_recon/asm/_REPORT.md</c> 第 8 节）。关键结论：</para>
///
/// <list type="number">
///   <item><b>对象布局</b>（<c>iCMeasureExtract</c>）：
///         <c>+0x00</c> 中间剖面 Mat（rows@+8 / cols@+0xc / data@+0x10）；
///         <c>+0x60</c> <b>梯度</b> Mat（data@+0x70，<c>sub_9890</c> 就吃这一张）；
///         <c>+0xc0</c> 平滑核大小、<c>+0xc8</c> 平滑 σ；
///         <c>+0xd0</c> 边缘核大小、<c>+0xd8</c> 边缘 σ；
///         <c>+0xe0</c> 平滑核 Mat（data@+0xf0）、<c>+0x140</c> 边缘核 Mat（data@+0x150）；
///         <c>+0x1a0</c> 极性、<c>+0x1a8</c> 梯度下限、<c>+0x1b0</c> 梯度上限、
///         <c>+0x1b8</c> 置信度、<c>+0x1c0</c> 边缘序号、
///         <c>+0x1c8/+0x1d0</c> 边缘点 xy、<c>+0x1d8</c> 边缘梯度；
///         <c>+0x1e0</c> 采样步长、<c>+0x1e8</c> 测量方向。</item>
///
///   <item><b>剖面抽取</b>（<c>sub_2460</c> 圆弧版 / <c>sub_3130</c> 直线版）：
///         剖面不是「沿测量线采样」再滤波，而是<b>每个输出下标 i 在垂直方向上开窗</b>：
///         <c>gray[i] = Σ_k smooth[k] · I(P_i + (k-half)·n̂)</c>，
///         其中 <c>P_i = from + i·dir</c>（<b>1 像素 1 采样</b>，与采样步长无关）。
///         圆弧版把窗口绕<b>参考点</b>旋转 <c>a = (k-half)/|P_i - ref|</c>，于是窗口贴着圆弧走；
///         直线版直接沿 <c>n̂ = (-dir.y, dir.x)</c> 平移。
///         双线性插值后 <b>+0.001</b>；越界写哨兵 <b>-1.0</b>，再前/后向填充。</item>
///
///   <item><b>梯度</b>：把 gray 剖面与<b>归一化一阶高斯导数核</b>沿 i 方向卷积，
///         越界处夹取端点值。响应直接落在 <c>[GradLo, GradHi]</c> 域（默认 [10,255]），
///         255 灰阶跳变 ≈ 127.5，正好在区间内。</item>
///
///   <item><b>边缘搜索</b>（<c>sub_9890</c> 自动 / <c>sub_9E90</c> 按序号）：
///         极性 0 = 取<b>带符号最大值</b>、1 = 取<b>带符号最小值</b>、≥2 = 取<b>绝对值最大</b>；
///         候选必须是<b>带符号的局部极值</b>且 <c>|v| ∈ [GradLo, GradHi]</c>；
///         自动模式还要求 <c>|v| ≥ (confidence − 0.001)·peak</c>，取<b>第一个</b>满足者；
///         序号模式按 <c>edgeIndex</c> 正/负决定<b>从起点向终点</b>还是<b>从终点向起点</b>数第 N 个。</item>
///
///   <item><b>亚像素</b>：抛物线顶点
///         <c>t = i − 0.25·(v[i+1] − v[i−1]) / Δ</c>，<c>Δ = (v[i−1]+v[i+1])/2 − v[i]</c>，
///         <c>|Δ| ≤ 1e-5</c> 时退化为 <c>t = i</c>（常量 −2.0 在 0xDD48、1e-5 在 0xDD38）。</item>
///
///   <item><b>圆测量</b>（<c>sub_7050</c>）：<c>n = round(arcSpan / samplingStep)</c>，
///         第 i 条卡尺角度 <c>θ = (startAngle + i·samplingStep)·π/180</c>，
///         两端点 <c>center + (r ∓ halfLen)·(cosθ, sinθ)</c>，
///         <c>halfLen = max(卡尺长度, 2.0)/2</c>；<c>setMeasureDirection(0)</c> = 由内向外。
///         有效点 <b>&lt; 3</b> 直接不拟合。</item>
///
///   <item><b>圆拟合</b>（<c>sub_4AF0</c>）：标准代数最小二乘 = Kasa 正规方程
///         <c>A = [[Σx²,Σxy,Σx],[Σxy,Σy²,Σy],[Σx,Σy,n]]</c>、
///         <c>b = −[Σx(x²+y²), Σy(x²+y²), Σ(x²+y²)]</c>，<c>cv::solve(A,b,x,DECOMP_SVD)</c>，
///         圆心 <c>(−x₀/2, −x₁/2)</c>、半径 <c>0.5·√(x₀²+x₁²−4x₂)</c>。</item>
/// </list>
/// </summary>

/// <summary>卡尺（caliper）参数集合：一个工具实例的完整可调项。</summary>
public sealed class CaliperParam
{
    // ---- 灰度平滑滤波（垂直方向一维高斯卷积）----
    /// <summary>平滑高斯核大小（<c>setSmoothFilterParam</c> 第 1 参，原程序 <c>[obj+0xc0]</c>）。</summary>
    public int SmoothKernel { get; set; } = 3;

    /// <summary>平滑高斯 σ；<b>≤0 表示由核大小自动推算</b>（OpenCV 语义）。原程序默认 −1.0。</summary>
    public double SmoothCoef { get; set; } = -1.0;

    // ---- 边缘梯度滤波（一维归一化高斯一阶导数核）----
    /// <summary>边缘导数核大小（<c>setEdgeFilterParam</c> 第 1 参，原程序 <c>[obj+0xd0]</c>）。</summary>
    public int EdgeKernel { get; set; } = 3;

    /// <summary>边缘核的高斯 σ；≤0 自动。原程序默认 −1.0。</summary>
    public double EdgeCoef { get; set; } = -1.0;

    // ---- 梯度阈值（setThreshold：lo 下限 0，hi 上限 255）----
    public double GradLo { get; set; } = 10.0;    // sub_97F0 默认 10.0
    public double GradHi { get; set; } = 255.0;

    // ---- 边缘点选择 ----
    /// <summary>边缘序号（<c>setSpecifiedParam</c> 第 1 参）。0 = 自动；&gt;0 从起点数第 N 条；&lt;0 从终点倒数第 |N| 条。</summary>
    public int EdgeIndex { get; set; } = 0;        // sub_97F0: [obj+0x1c0] = 0

    /// <summary>边缘置信度（<c>setSpecifiedParam</c> 第 2 参，原程序 clamp 到 <c>[0.001, 1.0]</c>）。</summary>
    public double EdgeConfidence { get; set; } = 1.0;

    /// <summary>极性（<c>setPolarity</c>）。原程序默认 <c>[obj+0x1a0] = 0</c> = 由暗到明。</summary>
    public DvPolarity Polarity { get; set; } = DvPolarity.DarkToBright;

    // ---- 圆测量专属 ----
    /// <summary>测量方向（<c>setMeasureDirection</c>）：0 = 由圆心向圆外，1 = 由圆外向圆心。原程序默认 0。</summary>
    public int MeasureDirection { get; set; } = 0;

    /// <summary>兼容旧字段：由外向内 ⇔ <see cref="MeasureDirection"/> = 1。</summary>
    public bool FromOutside
    {
        get => MeasureDirection != 0;
        set => MeasureDirection = value ? 1 : 0;
    }

    /// <summary>采样步长（<c>setSamplingsStep</c>）：圆工具单位是<b>度</b>，原程序 clamp 到 <c>[0.1, 45]</c>，默认 5.0。</summary>
    public double SamplingStep { get; set; } = 5.0;

    public DvFitMethod Fit { get; set; } = DvFitMethod.OrthogonalLeastSquares;

    /// <summary>滤波器核必须是奇数且 ≥1。</summary>
    public static int Odd(int k) => k < 1 ? 1 : (k % 2 == 0 ? k + 1 : k);

    /// <summary>对应 <c>setThreshold</c>：<c>GradLo = max(0, lo)</c>、<c>GradHi = min(255, hi)</c>。</summary>
    public void SetThreshold(double lo, double hi)
    {
        GradLo = lo > 0.0 ? lo : 0.0;
        GradHi = 255.0 > hi ? hi : 255.0;
    }

    /// <summary>对应 <c>setSpecifiedParam</c>：序号直存，置信度 clamp 到 <c>[0.001, 1.0]</c>。</summary>
    public void SetSpecifiedParam(int idx, double conf)
    {
        EdgeIndex = idx;
        EdgeConfidence = Math.Max(0.001, Math.Min(1.0, conf));
    }

    /// <summary>对应 <c>setSamplingsStep</c>：clamp 到 <c>[0.1, 45]</c>。</summary>
    public void SetSamplingsStep(double s)
    {
        SamplingStep = (s > 0.1) ? (45.0 > s ? s : 45.0) : 0.1;
    }

    public CaliperParam Clone() => (CaliperParam)MemberwiseClone();
}

/// <summary>
/// 卡尺滤波核构造 —— 逐条对齐 <c>lvMeasure!sub_4140</c>（平滑）与 <c>lvMeasure!sub_3F30</c>（边缘）。
///
/// <para>原程序两个 setter 的核大小算法（反汇编逐位翻译）：</para>
/// <code>
/// // sub_4140（平滑）：eax = max(1, H); eax = eax/2（向零取整，负数取 0）; ksize = 2*eax+1
/// // sub_3F30（边缘）：eax = max(3, H); 同上
/// </code>
/// 两者实际等价于「**取不超过 max(下限,H) 的最大奇数**」，平滑下限 1、边缘下限 3。
/// </summary>
public static class CaliperKernels
{
    /// <summary>平滑核大小：<c>sub_4140</c> 的 <c>2*trunc(max(1,H)/2)+1</c>。</summary>
    public static int SmoothSize(int h)
    {
        int n = Math.Max(1, h);
        int half = n / 2;                  // C++ 整数除法（向零取整），n≥1 时非负
        if (half < 0) half = 0;
        return 2 * half + 1;
    }

    /// <summary>边缘核大小：<c>sub_3F30</c> 的 <c>2*trunc(max(3,H)/2)+1</c>。</summary>
    public static int EdgeSize(int h)
    {
        int n = Math.Max(3, h);
        int half = n / 2;
        if (half < 0) half = 0;
        return 2 * half + 1;
    }

    /// <summary>
    /// 一维高斯核 —— 对应 <c>cv::getGaussianKernel(ksize, sigma, CV_64F)</c>。
    /// <c>sigma &lt;= 0</c> 时按 OpenCV 公式自动推算：<c>σ = 0.3*((ksize-1)*0.5 - 1) + 0.8</c>。
    /// 返回长度 = ksize 的列向量（与 OpenCV 一样是「列」）。
    /// </summary>
    public static double[] Gaussian(int ksize, double sigma)
    {
        if (ksize < 1) ksize = 1;
        if (ksize % 2 == 0) ksize++;                  // OpenCV 要求奇数
        if (sigma <= 0) sigma = 0.3 * ((ksize - 1) * 0.5 - 1) + 0.8;

        var k = new double[ksize];
        double c = (ksize - 1) * 0.5;
        double sum = 0;
        for (int i = 0; i < ksize; i++)
        {
            double d = i - c;
            k[i] = Math.Exp(-(d * d) / (2 * sigma * sigma));
            sum += k[i];
        }
        if (sum != 0) for (int i = 0; i < ksize; i++) k[i] /= sum;
        return k;
    }

    /// <summary>平滑核（对应 <c>setSmoothFilterParam</c>）。</summary>
    public static double[] Smooth(int h, double sigma) => Gaussian(SmoothSize(h), sigma);

    /// <summary>
    /// 边缘核（对应 <c>setEdgeFilterParam</c>）—— <b>归一化的一维高斯一阶导数</b>。
    ///
    /// <code>
    /// g = getGaussianKernel(ksize, sigma, CV_64F)
    /// for (i = 0; i &lt; ksize; i++) d[i] = 2 * (i - (ksize-1)/2) * g[i]
    /// s = Σ|d[i]|;  for (i) d[i] /= s
    /// </code>
    ///
    /// <para>反汇编里这段是 <c>sub_3F30</c> 的 0x4030–0x40A7：<c>subsd</c> 出 <c>(ksize-1)/2</c>、
    /// <c>addsd xmm1,xmm1</c> 得到因子 2、<c>andps</c> 取绝对值累加、最后 <c>divsd 1/Σ</c> 归一化。</para>
    ///
    /// <para>默认 <c>ksize=3, sigma=-1</c>（σ≈0.8）时得到 <b>[−0.5, 0, +0.5]</b>，即经典中心差分。</para>
    /// </summary>
    public static double[] EdgeDerivative(int h, double sigma)
    {
        int ksize = EdgeSize(h);
        var g = Gaussian(ksize, sigma);
        var d = new double[ksize];
        double c = (ksize - 1) * 0.5;
        double absSum = 0;
        for (int i = 0; i < ksize; i++)
        {
            d[i] = 2.0 * (i - c) * g[i];
            absSum += Math.Abs(d[i]);
        }
        if (absSum > 0) for (int i = 0; i < ksize; i++) d[i] /= absSum;
        return d;
    }
}


/// <summary>卡尺测量引擎（CMeasureCircle / CMeasureLine / CMeasurePoint 的共同内核）。</summary>
public static class Caliper
{
    /// <summary>原程序「采样点在图像外」时写的哨兵值（<c>sub_2460</c> 里的 <c>0xbff0000000000000</c>）。</summary>
    public const double OutsideSentinel = -1.0;

    /// <summary>置信度基准常量（RVA 0xDA58 = 0.001），<c>thr = (conf - 0.001)·peak</c>。</summary>
    public const double ConfidenceBase = 0.001;

    /// <summary>抛物线分母判零阈值（RVA 0xDD38 = 1e-05）。</summary>
    public const double SubPixelEps = 1e-5;

    /// <summary>卡尺最小长度（RVA 0xDC20 = 2.0）。</summary>
    public const double MinCaliperLength = 2.0;

    /// <summary>圆半径下限（RVA 0xDC28 = 3.0）。</summary>
    public const double MinRadius = 3.0;

    // ---------------------------------------------------------------- 采样

    /// <summary>灰度图的一次性托管副本：避免每个采样点都重建 OpenCvSharp 索引器。</summary>
    private sealed class GrayBuf
    {
        public byte[] Data = Array.Empty<byte>();
        public int W, H, Step;

        public static GrayBuf? From(Mat m)
        {
            if (m == null || m.Empty() || m.Channels() != 1) return null;
            if (m.Width < 4 || m.Height < 4) return null;
            var g = new GrayBuf { W = m.Width, H = m.Height, Step = (int)m.Step() };
            g.Data = new byte[g.Step * g.H];
            System.Runtime.InteropServices.Marshal.Copy(m.Data, g.Data, 0, g.Data.Length);
            return g;
        }
    }

    /// <summary>
    /// 对齐 C 库 <c>round()</c>：半数远离零。C# 默认 <see cref="Math.Round(double)"/> 是
    /// 银行家舍入（半数取偶），而反编译里到处是 <c>(int)ROUND(x)</c>，语义必须一致。
    /// </summary>
    private static int RoundHalfUp(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

    /// <summary>
    /// 双线性采样：<c>value = (1-fx)·lerp(v00,v10,fy) + fx·lerp(v01,v11,fy) + 0.001</c>；
    /// 坐标取 <c>floor</c>（反汇编 <c>cvttsd2si</c> + <c>seta</c>/<c>sub</c> 的负值修正）。
    /// 越界（<c>ix&lt;0 || ix≥w-1 || iy&lt;0 || iy≥h-1</c>）返回 <see cref="OutsideSentinel"/>。
    /// </summary>
    private static double Bilinear(GrayBuf b, double x, double y)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        if (ix < 0 || iy < 0 || ix >= b.W - 1 || iy >= b.H - 1) return OutsideSentinel;

        double fx = x - ix, fy = y - iy;
        int r0 = iy * b.Step + ix, r1 = r0 + b.Step;
        double v00 = b.Data[r0], v01 = b.Data[r0 + 1];
        double v10 = b.Data[r1], v11 = b.Data[r1 + 1];
        double lerp0 = v00 * (1 - fy) + v10 * fy;   // 第 ix 列
        double lerp1 = v01 * (1 - fy) + v11 * fy;   // 第 ix+1 列
        return (1 - fx) * lerp0 + fx * lerp1 + 0.001;
    }

    /// <summary>
    /// 原程序的无效值填充（<c>sub_2460</c> 0x2A2C–0x2B5C / <c>sub_3130</c> 同构）：
    /// 逐对 (i, i+1) 扫描，<c>v[i]&lt;0</c> 而 <c>v[i+1]≥0</c> 时把 <c>v[0..i]</c> 全填成 <c>v[i+1]</c>；
    /// <c>v[i]≥0</c> 而 <c>v[i+1]&lt;0</c> 时把后续连续无效段填成 <c>v[i]</c>。
    /// </summary>
    private static void FillInvalid(double[] v)
    {
        int k = v.Length;
        for (int i = 0; i <= k - 2; i++)
        {
            if (v[i] < 0.0 && v[i + 1] >= 0.0)
            {
                for (int j = 0; j <= i; j++) v[j] = v[i + 1];
            }
            else if (v[i] >= 0.0 && v[i + 1] < 0.0)
            {
                for (int j = i + 1; j < k && v[j] < 0.0; j++) v[j] = v[i];
            }
        }
    }

    /// <summary>
    /// 剖面抽取 —— 对应 <c>lvMeasure!sub_2460</c>（圆弧版，<paramref name="arc"/>=true）
    /// 与 <c>sub_3130</c>（直线版，<paramref name="arc"/>=false）。
    ///
    /// <para>输出长度 <c>n = (int)(|to−from| + 0.001)</c>（<c>cvttsd2si</c> 截断），
    /// <b>每像素一个采样</b>，与采样步长无关。方向 <c>dir = (to−from)/(|to−from|+0.001)</c>。</para>
    ///
    /// <para>对每个 i 在垂直方向开 <c>ksize</c> 长的窗口：
    /// 直线版沿 <c>n̂ = (−dir.y, dir.x)</c> 平移；
    /// 圆弧版把 <c>(P_i − ref)</c> 绕 ref 旋转 <c>a = j/(|P_i − ref| + 0.001)</c>，于是窗口贴着圆弧。</para>
    /// </summary>
    private static bool TryExtract(GrayBuf buf, DvPoint2D refPt, DvPoint2D from, DvPoint2D to,
                                   CaliperParam p, bool arc,
                                   out double[] gray, out double[] grad, out DvVector2D dir)
    {
        gray = Array.Empty<double>();
        grad = Array.Empty<double>();
        dir = default;

        int w = buf.W, h = buf.H;

        double ddx = to.X - from.X, ddy = to.Y - from.Y;
        double len = Math.Sqrt(ddx * ddx + ddy * ddy);
        int n = (int)(len + 0.001);            // sub_2460: cvttsd2si + 修正 → 等价截断
        if (n < 3) return false;               // cv::error(-5, "measure line is too short!")

        double invLen = 1.0 / (len + 0.001);
        dir = new DvVector2D(ddx * invLen, ddy * invLen);

        var smooth = CaliperKernels.Smooth(p.SmoothKernel, p.SmoothCoef);
        int ks = smooth.Length, half = ks / 2;

        gray = new double[n];
        var tmp = new double[ks];

        for (int i = 0; i < n; i++)
        {
            double px = from.X + dir.X * i;
            double py = from.Y + dir.Y * i;

            // 反编译实证（lvMeasure!sub_2460 / sub_3130，同属 iCMeasureExtract::process）：
            // 两版都在开横窗前先用 ROUND 后的点坐标做一次越界预检，越界则整点写哨兵。
            //
            //   arc 版（sub_2460，行 0x114）：
            //       ROUND(px) < 1 || ROUND(px) >= cols-1 || ROUND(py) < 1 || ROUND(py) >= rows-1
            //   line 版（sub_3130，行 0x62）：三分支，快路径要求整窗都进得去
            //       half+2 < ROUND(px) < cols-half-2  且  half+2 < ROUND(py) < rows-half-2
            //     否则退化到"逐样本判界"的慢路径（本实现在 Bilinear 里统一处理）。
            int ixr = RoundHalfUp(px), iyr = RoundHalfUp(py);
            if (arc)
            {
                if (ixr < 1 || ixr >= w - 1 || iyr < 1 || iyr >= h - 1)
                {
                    gray[i] = OutsideSentinel;
                    continue;
                }
            }
            else if (ixr <= half + 2 || ixr >= w - half - 2 ||
                     iyr <= half + 2 || iyr >= h - half - 2)
            {
                // 直线版：整条横向窗口都在图内才走快路径，否则整点无效
                gray[i] = OutsideSentinel;
                continue;
            }

            double dx = px - refPt.X, dy = py - refPt.Y;
            double invL = 0;
            if (arc)
            {
                double L = Math.Sqrt(dx * dx + dy * dy) + 0.001;
                invL = 1.0 / L;
            }

            for (int j = -half; j <= half; j++)
            {
                double sx, sy;
                if (arc)
                {
                    double a = j * invL;
                    double ca = Math.Cos(a), sa = Math.Sin(a);
                    sx = ca * dx - sa * dy + refPt.X;
                    sy = sa * dx + ca * dy + refPt.Y;
                }
                else
                {
                    sx = px - dir.Y * j;         // n̂ = (−dir.y, +dir.x)
                    sy = py + dir.X * j;
                }
                tmp[j + half] = Bilinear(buf, sx, sy);
            }
            FillInvalid(tmp);

            double sum = 0;
            for (int k = 0; k < ks; k++) sum += smooth[k] * tmp[k];
            gray[i] = sum;
        }

        // 剖面自身也要做一次无效值填充（sub_2460 0x2C60 那一遍）
        FillInvalid(gray);

        // ---- 边缘核卷积：越界夹取端点值（sub_2460 0x2DB5 起）----
        var ek = CaliperKernels.EdgeDerivative(p.EdgeKernel, p.EdgeCoef);
        int es = ek.Length, eh = es / 2;
        grad = new double[n];
        for (int i = 0; i < n; i++)
        {
            double sum = 0;
            for (int k = 0; k < es; k++)
            {
                int idx = i + k - eh;
                double v = idx < 0 ? gray[0] : (idx >= n ? gray[n - 1] : gray[idx]);
                sum += ek[k] * v;
            }
            grad[i] = sum;
        }
        return true;
    }

    // ---------------------------------------------------------------- 找边

    /// <summary>
    /// 判断候选点是否满足极性 + 梯度区间 + <b>带符号局部极值</b> 三个条件
    /// （<c>sub_9890</c> / <c>sub_9E90</c> 的三个极性分支逐位翻译）。
    /// </summary>
    private static bool Candidate(double[] v, int i, CaliperParam p)
    {
        double cur = v[i];
        switch ((int)p.Polarity)
        {
            case 0:   // 由暗到明：带符号最大值
                if (!(cur >= p.GradLo)) return false;
                if (cur > p.GradHi) return false;
                if (!(cur > v[i - 1])) return false;
                if (!(cur > v[i + 1])) return false;
                return true;

            case 1:   // 由明到暗：带符号最小值
                if (!(cur <= -p.GradLo)) return false;
                if (-p.GradHi > cur) return false;
                if (!(v[i - 1] > cur)) return false;
                if (!(v[i + 1] > cur)) return false;
                return true;

            default:  // 任意极性：绝对值最大，极值判定仍按带符号
                double a = Math.Abs(cur);
                if (p.GradLo > a) return false;
                if (a > p.GradHi) return false;
                if (cur >= 0.0)
                {
                    if (!(cur > v[i - 1])) return false;
                    if (!(cur > v[i + 1])) return false;
                }
                else
                {
                    if (!(cur < v[i - 1])) return false;
                    if (!(cur < v[i + 1])) return false;
                }
                return true;
        }
    }

    /// <summary>
    /// 抛物线亚像素 —— <c>sub_9890</c>/<c>sub_9E90</c> 尾部同一段：
    /// <c>t = i − 0.25·(v[i+1]−v[i−1])/Δ</c>，<c>Δ = (v[i−1]+v[i+1])/2 − v[i]</c>；
    /// <c>|Δ| ≤ 1e-5</c> 时 <c>t = i</c>。
    /// </summary>
    private static double SubPixel(double[] v, int i, int n)
    {
        if (i <= 0 || i >= n - 1) return i;
        double delta = (v[i - 1] + v[i + 1]) * 0.5 - v[i];
        if (Math.Abs(delta) <= SubPixelEps) return i;
        return i - 0.25 * (v[i + 1] - v[i - 1]) / delta;
    }

    private static DvEdgePoint MakePoint(double[] v, int i, int n, DvPoint2D from, DvVector2D dir)
    {
        double t = SubPixel(v, i, n);
        return new DvEdgePoint
        {
            Position = new DvPoint2D(from.X + dir.X * t, from.Y + dir.Y * t),
            Gradient = v[i],
        };
    }

    /// <summary>
    /// 自动找边 —— 对应 <c>lvMeasure!sub_9890</c>（2958 B）。
    ///
    /// <para>第一遍按极性求全局峰值 <c>peak</c>；阈值 <c>thr = (confidence − 0.001)·peak</c>；
    /// 第二遍取<b>第一个</b>满足 <c>|v| ∈ [GradLo, GradHi]</c>、<c>|v| ≥ thr</c> 的带符号局部极值。
    /// 若一个都没有，则退回全局峰值（要求它在阈值区间内），否则返回 null（原程序输出 (−1,−1)）。</para>
    /// </summary>
    public static DvEdgePoint? FindEdgeAuto(double[] grad, int n, DvPoint2D from, DvVector2D dir,
                                            CaliperParam p)
    {
        if (grad == null || n < 3) return null;

        int pol = (int)p.Polarity;
        double peak = pol == 1 ? 1000.0 : -1000.0;
        int peakIdx = 0;
        for (int i = 1; i <= n - 2; i++)
        {
            double v = grad[i];
            if (pol == 0) { if (v > peak) { peak = v; peakIdx = i; } }
            else if (pol == 1) { if (v < peak) { peak = v; peakIdx = i; } }
            else { double a = Math.Abs(v); if (a > peak) { peak = a; peakIdx = i; } }
        }

        double thr = (p.EdgeConfidence - ConfidenceBase) * peak;

        int found = -1;
        for (int i = 1; i <= n - 2; i++)
        {
            double cur = grad[i];
            bool ok = (int)p.Polarity switch
            {
                0 => Candidate(grad, i, p) && !(thr > cur),
                1 => Candidate(grad, i, p) && thr > cur,
                _ => Candidate(grad, i, p) && !(thr > Math.Abs(cur)),
            };
            if (ok) { found = i; break; }
        }

        if (found < 0)
        {
            // 退回全局峰值（sub_9890 0x9D8B 分支）
            double m = peak;
            if (pol == 1 && !(m < 0.0)) return null;
            double am = Math.Abs(m);
            if (am <= p.GradLo) return null;
            if (p.GradHi <= am) return null;
            found = peakIdx;
        }

        return MakePoint(grad, found, n, from, dir);
    }

    /// <summary>
    /// 按序号找边 —— 对应 <c>lvMeasure!sub_9E90</c>（221 B 主流程 + 复用 sub_9890）。
    ///
    /// <para><c>edgeIndex &gt; 0</c>：从起点向终点扫，取第 <c>edgeIndex</c> 个满足条件的局部极值；
    /// <c>edgeIndex &lt; 0</c>：从终点向起点扫，取倒数第 <c>|edgeIndex|</c> 个；
    /// <c>edgeIndex == 0</c>：直接转 <see cref="FindEdgeAuto"/>。此路径<b>不做置信度过滤</b>。</para>
    /// </summary>
    public static DvEdgePoint? FindEdgeAt(double[] grad, int n, DvPoint2D from, DvVector2D dir,
                                          CaliperParam p, int edgeIndex)
    {
        if (grad == null || n < 3) return null;
        if (edgeIndex == 0) return FindEdgeAuto(grad, n, from, dir, p);

        int target = Math.Abs(edgeIndex);
        int count = 0;

        if (edgeIndex > 0)
        {
            for (int i = 1; i <= n - 2; i++)
            {
                if (!Candidate(grad, i, p)) continue;
                if (++count == target) return MakePoint(grad, i, n, from, dir);
            }
        }
        else
        {
            for (int i = n - 2; i >= 1; i--)
            {
                if (!Candidate(grad, i, p)) continue;
                if (++count == target) return MakePoint(grad, i, n, from, dir);
            }
        }
        return null;
    }

    /// <summary>按 <see cref="CaliperParam.EdgeIndex"/> 分派自动 / 序号两种找边。</summary>
    public static DvEdgePoint? FindEdge(double[] grad, int n, DvPoint2D from, DvVector2D dir,
                                        CaliperParam p)
        => p.EdgeIndex == 0
            ? FindEdgeAuto(grad, n, from, dir, p)
            : FindEdgeAt(grad, n, from, dir, p, p.EdgeIndex);

    /// <summary>单条卡尺：抽剖面 → 找边（对应 <c>iCMeasurePoint::process</c>，圆弧版 <c>sub_A430</c>）。</summary>
    private static DvEdgePoint? MeasureOne(GrayBuf buf, DvPoint2D refPt, DvPoint2D from, DvPoint2D to,
                                           CaliperParam p)
    {
        if (!TryExtract(buf, refPt, from, to, p, arc: true, out _, out var grad, out var dir))
            return null;
        return FindEdge(grad, grad.Length, from, dir, p);
    }

    /// <summary>单条卡尺（直线版，对应 <c>iCMeasurePoint::process</c> 2 点重载 <c>sub_A530</c>）。</summary>
    private static DvEdgePoint? MeasureOneStraight(GrayBuf buf, DvPoint2D from, DvPoint2D to,
                                                   CaliperParam p)
    {
        if (!TryExtract(buf, from, from, to, p, arc: false, out _, out var grad, out var dir))
            return null;
        return FindEdge(grad, grad.Length, from, dir, p);
    }

    /// <summary>单条卡尺（圆弧版，公开入口）。</summary>
    public static DvEdgePoint? MeasureOne(Mat img, DvPoint2D refPt, DvPoint2D from, DvPoint2D to,
                                          CaliperParam p)
    {
        var buf = GrayBuf.From(img);
        return buf == null ? null : MeasureOne(buf, refPt, from, to, p);
    }

    /// <summary>单条卡尺（直线版，公开入口）。</summary>
    public static DvEdgePoint? MeasureOneStraight(Mat img, DvPoint2D from, DvPoint2D to, CaliperParam p)
    {
        var buf = GrayBuf.From(img);
        return buf == null ? null : MeasureOneStraight(buf, from, to, p);
    }

    // ---------------------------------------------------------------- 圆

    /// <summary>
    /// 圆查找（<c>CMeasureCircle::process</c> = <c>sub_7050</c>，2165 B）。
    ///
    /// <para>卡尺数 <c>n = round(arcSpan / samplingStep)</c>；第 i 条角度
    /// <c>θ = (startAngle + i·samplingStep)·π/180</c>；半长 <c>h = max(caliperLen, 2)/2</c>；
    /// <c>MeasureDirection == 0</c> 时两脚为 <c>r−h → r+h</c>（由内向外），否则反向。
    /// 每条的参考点（横向窗口的旋转中心）就是<b>圆心</b>，于是窗口贴着圆弧。
    /// 有效点不足 3 个时不拟合，返回半径为 0 的圆。</para>
    /// </summary>
    public static (DvCircle circle, List<DvEdgePoint> pts, double validRatio) MeasureCircle(
        Mat img, DvArc arc, double caliperLength, CaliperParam p)
    {
        var pts = new List<DvEdgePoint>();
        var buf = GrayBuf.From(img);
        if (buf == null) return (new DvCircle(arc.Center, 0), pts, 0);

        double r = Math.Max(MinRadius, arc.Radius);   // sub_7050: if (r <= 3.0) r = 3.0
        double step = Math.Max(0.1, p.SamplingStep);
        // sub_7050: iVar18 = (int)ROUND(span / step) —— C 的 round() 是半数远离零，
        // 且这里没有上限 clamp（步长的 [0.1,45] clamp 在 setSamplingsStep 里做）
        int n = RoundHalfUp(arc.SpanDeg / step);
        if (n < 1) n = 1;
        double half = Math.Max(caliperLength, MinCaliperLength) * 0.5;
        bool outward = p.MeasureDirection == 0;

        for (int i = 0; i < n; i++)
        {
            double th = (arc.StartAngleDeg + i * step) * Math.PI / 180.0;
            double c = Math.Cos(th), s = Math.Sin(th);
            double rA = outward ? r - half : r + half;
            double rB = outward ? r + half : r - half;
            var a = new DvPoint2D(arc.Center.X + c * rA, arc.Center.Y + s * rA);
            var b = new DvPoint2D(arc.Center.X + c * rB, arc.Center.Y + s * rB);
            var e = MeasureOne(buf, arc.Center, a, b, p);
            if (e != null) pts.Add(e);
        }

        double ratio = n == 0 ? 0 : (double)pts.Count / n;
        if (pts.Count < 3) return (new DvCircle(arc.Center, 0), pts, ratio);

        var list = pts.Select(x => x.Position).ToList();
        DvCircle circle = p.Fit switch
        {
            DvFitMethod.AlgebraicLeastSquares => DvGeometry.FitCircleKasa(list),
            DvFitMethod.WeightedLeastSquares => DvGeometry.FitCircleWeighted(pts),
            _ => DvGeometry.FitCircleOrthogonal(list),
        };
        return (circle, pts, ratio);
    }

    /// <summary>圆环 ROI 重载：取环中线为标称半径、环宽为卡尺长度，扫整圈。</summary>
    public static (DvCircle circle, List<DvEdgePoint> pts, double validRatio) MeasureCircle(
        Mat img, DvRing ring, int caliperCount, CaliperParam p)
    {
        double rMid = (ring.OuterRadius + ring.InnerRadius) * 0.5;
        double width = Math.Max(ring.Width, MinCaliperLength);
        if (caliperCount > 0)
        {
            // 兼容调用方显式指定卡尺条数：换算成等效步长
            var q = p.Clone();
            q.SamplingStep = 360.0 / caliperCount;
            return MeasureCircle(img, new DvArc(ring.Center, rMid, 0, 360), width, q);
        }
        return MeasureCircle(img, new DvArc(ring.Center, rMid, 0, 360), width, p);
    }

    // ---------------------------------------------------------------- 直线

    /// <summary>
    /// 直线查找（<c>CMeasureLine::process</c> = <c>lvMeasure!sub_85B0</c>）。
    ///
    /// <para>反编译实证的卡尺布法（<c>sub_85B0</c>）：</para>
    /// <code>
    /// rad = angle° * π/180 ;  û = (cos, sin) ； n̂ = (−sin, cos)
    /// A  = center − (Width/2)·û
    /// p1 = A + (Height/2)·n̂ ;  p2 = A − (Height/2)·n̂     // 一条卡尺的两端 = 高边
    /// n  = (int)ROUND(Width / samplingStep) − 1
    /// for i = 1 .. n:  off = i·step·û
    ///     from = p1 + off ;  to = p2 + off                 // 卡尺沿「高」方向扫
    /// </code>
    /// <para>即卡尺沿 ROI 的<b>高度</b>方向排布、沿<b>宽度</b>方向以 <c>i·step</c>（<b>i 从 1 起</b>）
    /// 偏移，两端各留一个步长。每条走直线版剖面抽取（<c>sub_3130</c>，横向窗口垂直于测量线）。</para>
    ///
    /// <para>拟合走 <c>cv::fitLine(points, DIST_L2/DIST_WELSCH, param=0, reps=0.001, aeps=0.001)</c>
    /// —— 常量 <c>0xDD00/0xDD08 = 0.001</c> 逐字节确认；输出方向会被强制与 û 同向
    /// （<c>sub_85B0</c> 尾部 <c>dir·û &lt; 0</c> 则取反），线上点取 ROI 中心在拟合线上的垂足。</para>
    ///
    /// <para>注意：原程序内部把 <c>_dvRotRect</c> 的 5 个字段先降精度成 <c>float</c>
    /// （<c>CMeasureLine::process</c> 里的 <c>(float)</c> 窄化）再送进 sub_85B0，本实现保留双精度 ——
    /// 那是 lossy 的实现细节，对 &lt;32768 px 的坐标影响约 1e-4 px。</para>
    /// </summary>
    public static (DvLine line, List<DvEdgePoint> pts, double validRatio) MeasureLine(
        Mat img, DvRotRect roi, int caliperCount, CaliperParam p)
    {
        var pts = new List<DvEdgePoint>();
        var buf = GrayBuf.From(img);
        var ux = roi.UnitX();                     // û（宽方向）
        var uy = roi.UnitY();                     // n̂（高方向）
        if (buf == null) return (new DvLine(roi.Center, ux), pts, 0);

        double w = Math.Max(1, roi.Width), h = Math.Max(1, roi.Height);
        double step = Math.Max(0.1, p.SamplingStep);

        int n = caliperCount > 0
            ? caliperCount
            : RoundHalfUp(w / step) - 1;          // sub_85B0: (int)ROUND(w/step) − 1
        if (n < 1) n = 1;

        // A = center − (w/2)·û ；p1/p2 = A ± (h/2)·n̂
        var a = new DvPoint2D(roi.Center.X - w * 0.5 * ux.X, roi.Center.Y - w * 0.5 * ux.Y);
        var p1 = new DvPoint2D(a.X + h * 0.5 * uy.X, a.Y + h * 0.5 * uy.Y);
        var p2 = new DvPoint2D(a.X - h * 0.5 * uy.X, a.Y - h * 0.5 * uy.Y);

        for (int i = 1; i <= n; i++)
        {
            double ox = i * step * ux.X, oy = i * step * ux.Y;
            var from = new DvPoint2D(p1.X + ox, p1.Y + oy);
            var to = new DvPoint2D(p2.X + ox, p2.Y + oy);
            var e = MeasureOneStraight(buf, from, to, p);
            if (e != null) pts.Add(e);
        }

        double ratio = (double)pts.Count / n;
        if (pts.Count < 2) return (new DvLine(roi.Center, ux), pts, ratio);

        // cv::fitLine —— 与原程序同一个 OpenCV 调用；拟合方式 0→L2，其余→WELSCH
        var dt = p.Fit == DvFitMethod.AlgebraicLeastSquares
            ? OpenCvSharp.DistanceTypes.L2
            : OpenCvSharp.DistanceTypes.Welsch;
        var fl = Cv2.FitLine(
            pts.Select(x => new Point2f((float)x.Position.X, (float)x.Position.Y)),
            dt, 0.0, 0.001, 0.001);
        var dir = new DvVector2D(fl.Vx, fl.Vy).Normalized();
        if (dir.Dot(ux) < 0) dir = new DvVector2D(-dir.X, -dir.Y);   // 强制与 û 同向
        // 垂足：roi.Center 向拟合线（过 fl 的 (X1,Y1)，方向 dir）作垂线的交点
        var lp = new DvPoint2D(fl.X1, fl.Y1);
        double tt = (roi.Center.X - lp.X) * dir.X + (roi.Center.Y - lp.Y) * dir.Y;
        return (new DvLine(new DvPoint2D(lp.X + dir.X * tt, lp.Y + dir.Y * tt), dir), pts, ratio);
    }

    /// <summary>轴对齐 ROI 的兼容重载（角度 0）。</summary>
    public static (DvLine line, List<DvEdgePoint> pts, double validRatio) MeasureLine(
        Mat img, DvRect64d roi, int caliperCount, CaliperParam p)
        => MeasureLine(img, DvRotRect.FromDeg(roi.Center, Math.Max(1, roi.Width),
                                             Math.Max(1, roi.Height), 0), caliperCount, p);

    // ---------------------------------------------------------------- 点 / 节距

    /// <summary>
    /// 点测量（<c>CMeasurePoint::process</c>）：单条卡尺的亚像素边缘点。
    /// <paramref name="from"/> 起、沿 <paramref name="dir"/> 长 <paramref name="length"/>。
    /// </summary>
    public static DvEdgePoint? MeasurePoint(Mat img, DvPoint2D from, DvVector2D dir, double length,
                                            CaliperParam p)
    {
        var to = new DvPoint2D(from.X + dir.X * length, from.Y + dir.Y * length);
        var buf = GrayBuf.From(img);
        return buf == null ? null : MeasureOne(buf, from, from, to, p);
    }

    /// <summary>
    /// 节距测量（<c>CMeasurePitch::process</c> = <c>sub_96E0</c>）：
    /// 沿一条扫描线按序号依次取边，再按最小间距过滤。
    /// </summary>
    public static List<DvEdgePoint> MeasurePitch(Mat img, DvPoint2D from, DvVector2D dir,
                                                 double length, double minSpace, CaliperParam p)
    {
        var keep = new List<DvEdgePoint>();
        var buf = GrayBuf.From(img);
        if (buf == null) return keep;

        var to = new DvPoint2D(from.X + dir.X * length, from.Y + dir.Y * length);
        if (!TryExtract(buf, from, from, to, p, arc: true, out _, out var grad, out var gdir))
            return keep;

        for (int k = 1; k <= grad.Length; k++)
        {
            var e = FindEdgeAt(grad, grad.Length, from, gdir, p, k);
            if (e == null) break;
            if (keep.Count == 0 || e.Position.DistTo(keep[^1].Position) >= minSpace)
                keep.Add(e);
        }
        return keep;
    }
}
