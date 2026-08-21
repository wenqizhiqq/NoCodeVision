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
    private int _w = 640, _h = 480;
    private double _phase;

    public bool IsGrabbing => _run;

    public event Action<BitmapSource>? FrameReady;
    public event Action<string>? Log;

    public void Start(string? serial = null)
    {
        if (_run) return;
        _run = true;
        _phase = 0;
        Log?.Invoke($"[相机] 模拟相机已启动{(serial != null ? $"（{serial}）" : "")}，640×480");
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
        // 网格背景
        for (int x = 0; x < _w; x += 40) Cv2.Line(mat, new CvPoint(x, 0), new CvPoint(x, _h), new Scalar(45, 45, 52), 1);
        for (int y = 0; y < _h; y += 40) Cv2.Line(mat, new CvPoint(0, y), new CvPoint(_w, y), new Scalar(45, 45, 52), 1);
        // 运动目标（圆）
        var cx = (int)(_w / 2 + Math.Cos(_phase) * 180);
        var cy = (int)(_h / 2 + Math.Sin(_phase * 1.3) * 120);
        Cv2.Circle(mat, new CvPoint(cx, cy), 36, new Scalar(52, 199, 89), -1);     // Apple 绿
        Cv2.Circle(mat, new CvPoint(cx, cy), 36, new Scalar(255, 255, 255), 2);
        // 十字准星
        Cv2.Line(mat, new CvPoint(_w / 2 - 20, _h / 2), new CvPoint(_w / 2 + 20, _h / 2), new Scalar(180, 180, 190), 1);
        Cv2.Line(mat, new CvPoint(_w / 2, _h / 2 - 20), new CvPoint(_w / 2, _h / 2 + 20), new Scalar(180, 180, 190), 1);
        // 时间戳文字
        Cv2.PutText(mat, $"SIM CAM {DateTime.Now:HH:mm:ss.fff}",
            new CvPoint(12, _h - 14), HersheyFonts.HersheyPlain, 1.2, new Scalar(230, 230, 235));
        return mat;
    }

    public void Dispose() => Stop();
}
