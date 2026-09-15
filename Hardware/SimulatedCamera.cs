using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;

namespace NoCodeVision.Hardware;

/// <summary>
/// 模拟相机：用 OpenCvSharp 实时生成带运动目标的测试图样（真实像素帧，非静态占位）。
/// 没有硬件时使用。接入真实相机 SDK 后，新建一个 ICamera 实现替换它即可。
/// </summary>
public sealed class SimulatedCamera : ICamera
{
    private Thread? _thread;
    private volatile bool _run;
    private int _w = 1920, _h = 1080; // 16:9 1080p 比例（原 640×480 为 4:3，预览会被拉伸）
    private double _phase;
    private readonly string _cameraId;
    // 每个通道不同的视觉特征（颜色 / 标签偏移）
    private readonly Scalar _primaryColor;
    private readonly double _phaseOffset;

    public bool IsGrabbing => _run;

    public event Action<BitmapSource>? FrameReady;
    public event Action<string>? Log;

    /// <summary>创建模拟相机。cameraId 用于多通道区分（不同颜色/相位）。</summary>
    public SimulatedCamera(string cameraId = "default")
    {
        _cameraId = cameraId;
        // 根据 ID 哈希确定每个通道的独有颜色和相位偏移（保证视觉可区分）
        var hash = Math.Abs(cameraId.GetHashCode());
        var hues = new[] {
            new Scalar(52, 199, 89),   // Apple 绿
            new Scalar(0, 122, 255),   // Apple 蓝
            new Scalar(255, 149, 0),   // Apple 橙
            new Scalar(255, 59, 48),   // Apple 红
            new Scalar(175, 82, 222),  // Apple 紫
            new Scalar(90, 200, 250),  // Apple 青
        };
        _primaryColor = hues[hash % hues.Length];
        _phaseOffset = (hash % 100) * 0.0628;
    }

    public void Start(string? serial = null)
    {
        if (_run) return;
        _run = true;
        _phase = 0;
        Log?.Invoke($"[相机] 模拟相机已启动{(serial != null ? $"（{serial}）" : "")}，1920×1080 (16:9)");
        _thread = new Thread(Loop) { IsBackground = true, Name = "SimCamera" };
        _thread.Start();
    }

    public void Stop()
    {
        if (!_run) return;
        _run = false;
        Log?.Invoke("[相机] 模拟相机已停止");
    }

    public BitmapSource? GrabOne()
    {
        using var mat = Render();
        var wb = new WriteableBitmap(mat.Width, mat.Height, 96, 96, PixelFormats.Bgr24, null);
        int stride = mat.Width * 3;
        wb.WritePixels(new Int32Rect(0, 0, mat.Width, mat.Height), mat.Data, stride * mat.Height, stride);
        wb.Freeze();
        return wb;
    }

    private void Loop()
    {
        while (_run)
        {
            try
            {
                var bmp = GrabOne();
                if (bmp != null) FrameReady?.Invoke(bmp);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[相机错误] {ex.Message}");
            }
            Thread.Sleep(33); // ~30fps
        }
    }

    private Mat Render()
    {
        _phase += 0.06;
        var mat = new Mat(_h, _w, MatType.CV_8UC3, Scalar.All(28));
        // 网格背景（间距随分辨率等比缩放，保持视觉密度一致）
        int step = Math.Max(40, _w / 24);
        for (int x = 0; x < _w; x += step) Cv2.Line(mat, new CvPoint(x, 0), new CvPoint(x, _h), new Scalar(45, 45, 52), 1);
        for (int y = 0; y < _h; y += step) Cv2.Line(mat, new CvPoint(0, y), new CvPoint(_w, y), new Scalar(45, 45, 52), 1);
        // 运动目标（圆）— 每通道不同颜色和相位，振幅随分辨率等比缩放
        var cx = (int)(_w / 2 + Math.Cos(_phase + _phaseOffset) * _w * 0.18);
        var cy = (int)(_h / 2 + Math.Sin(_phase * 1.3 + _phaseOffset) * _h * 0.20);
        int r = (int)(_h * 0.05);
        Cv2.Circle(mat, new CvPoint(cx, cy), r, _primaryColor, -1);
        Cv2.Circle(mat, new CvPoint(cx, cy), r, new Scalar(255, 255, 255), 2);
        // 十字准星
        int cross = (int)(_h * 0.02);
        Cv2.Line(mat, new CvPoint(_w / 2 - cross, _h / 2), new CvPoint(_w / 2 + cross, _h / 2), new Scalar(180, 180, 190), 1);
        Cv2.Line(mat, new CvPoint(_w / 2, _h / 2 - cross), new CvPoint(_w / 2, _h / 2 + cross), new Scalar(180, 180, 190), 1);
        // 时间戳 + 通道 ID 文字（字号随分辨率等比缩放）
        double fontScale = Math.Max(1.0, _w / 960.0);
        Cv2.PutText(mat, $"{_cameraId}  {DateTime.Now:HH:mm:ss.fff}",
            new CvPoint(16, _h - 20), HersheyFonts.HersheyPlain, fontScale, new Scalar(230, 230, 235));
        return mat;
    }

    public void Dispose() => Stop();
}
// 温启志：18719361399  混淆: 温h启f志g：l1u8i7l1f9j3g6b1x3w9q9
