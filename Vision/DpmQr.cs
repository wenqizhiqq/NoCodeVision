// 点阵（DPM）二维码读取：激光/针打在金属上的码是一个个圆点，模块之间留缝，普通解码器对着原图读不出来。
// 实测（2026-10-27，用户 3 张 3000×4000 工件照）：托管 ZXing.Net 0/5；原生 zxing-cpp 对着原图也 0/5；
// 只有「减背景提亮点 → 一档一档缩小让圆点糊成实心模块 → 补点 → 留白」这条链能稳定读通，所以这里把这几步都轮流试一遍。
// 实现：温启志

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCvSharp;
using ZC = ZXingCpp;

namespace NoCodeVision.Vision;

public static class DpmQr
{
    /// <summary>一次读取的结果：文本 + 码制 + 是哪组参数读出来的（现场调参要用）。</summary>
    public sealed class Result
    {
        public string Text = "";
        public string Format = "";
        public string How = "";
        public string CodeFormat = "";
        public long Ms;
        public bool Ok => Text.Length > 0;
        public override string ToString() => Ok ? $"{Format}：{Text}（{How}）" : "没读到";
    }

    /// <summary>先按原图大小试，再一档一档缩小：缩到圆点糊成整块时最好读。</summary>
    static readonly double[] Scales = { 1.0, 0.67, 0.5, 0.33, 0.25, 0.2 };

    /// <summary>两种补点方式：闭运算补点 / 放大后再模糊补点。</summary>
    static readonly (int close, int up, int blur)[] Combos = { (3, 1, 0), (1, 2, 5), (5, 1, 0), (3, 2, 5) };

    /// <summary>码制：want = "qr" 只找二维码 / "1d" 只找一维条码 / 空 = 都找。</summary>
    sealed class Decoders : IDisposable
    {
        public ZC.BarcodeReader? Cpp;
        public string Want = "";

        public static Decoders Open(string want)
        {
            var d = new Decoders { Want = (want ?? "").Trim() };
            try
            {
                d.Cpp = new ZC.BarcodeReader { TryHarder = true, TryRotate = true, TryInvert = true };
                if (d.Want == "qr") d.Cpp.Formats = ZC.BarcodeFormats.Parse("QRCode|DataMatrix");
                else if (d.Want == "1d") d.Cpp.Formats = ZC.BarcodeFormat.AllLinear;
                var probe = new byte[8 * 8];
                d.Cpp.From(new ZC.ImageView(probe, 8, 8, ZC.ImageFormat.Lum, 8, 1));
            }
            catch { d.Cpp = null; }   // 原生库加载不了就只用 ZXing.Net
            return d;
        }

        public string Try(Mat gray)
        {
            if (gray.Empty()) return "";
            if (Cpp != null)
            {
                try
                {
                    var iv = new ZC.ImageView(gray.Data, gray.Width, gray.Height, ZC.ImageFormat.Lum, (int)gray.Step(), 1);
                    foreach (var b in Cpp.From(iv))
                        if (!string.IsNullOrEmpty(b.Text)) return b.Text;
                }
                catch { }
            }
            return TryNet(gray);
        }

        static string TryNet(Mat gray)
        {
            try
            {
                using var bgr = new Mat();
                if (gray.Channels() == 1) Cv2.CvtColor(gray, bgr, ColorConversionCodes.GRAY2BGR); else gray.CopyTo(bgr);
                var px = new byte[bgr.Width * bgr.Height * 3];
                Marshal.Copy(bgr.Data, px, 0, px.Length);
                var src = new ZXing.RGBLuminanceSource(px, bgr.Width, bgr.Height);
                var hints = new Dictionary<ZXing.DecodeHintType, object> { { ZXing.DecodeHintType.TRY_HARDER, true } };
                hints[ZXing.DecodeHintType.POSSIBLE_FORMATS] = AllNetFormats();
                var r = new ZXing.MultiFormatReader().decode(
                    new ZXing.BinaryBitmap(new ZXing.Common.HybridBinarizer(src)), hints);
                return r?.Text ?? "";
            }
            catch { return ""; }
        }

        static List<ZXing.BarcodeFormat> AllNetFormats() => new()
        {
            ZXing.BarcodeFormat.QR_CODE, ZXing.BarcodeFormat.DATA_MATRIX, ZXing.BarcodeFormat.CODE_128,
            ZXing.BarcodeFormat.CODE_39, ZXing.BarcodeFormat.CODE_93, ZXing.BarcodeFormat.ITF,
            ZXing.BarcodeFormat.EAN_13, ZXing.BarcodeFormat.EAN_8, ZXing.BarcodeFormat.CODABAR,
        };

        public void Dispose() => Cpp?.Dispose();
    }

    /// <summary>原生 zxing-cpp 能不能用（识别率明显高于托管 ZXing.Net，点阵码基本只有它能读）。</summary>
    public static bool CppAvailable
    {
        get
        {
            using var d = Decoders.Open("");
            return d.Cpp != null;
        }
    }

    static Mat ToGray(Mat img)
    {
        var g = new Mat();
        if (img.Channels() == 1) img.CopyTo(g);
        else Cv2.CvtColor(img, g, ColorConversionCodes.BGR2GRAY);
        return g;
    }

    /// <summary>亮点掩膜：减掉大模糊背景再 Otsu，只留比周围亮的点（金属面上的激光点正是这个特征）。</summary>
    static Mat BrightMask(Mat gray)
    {
        using var bg = new Mat(); Cv2.GaussianBlur(gray, bg, new Size(0, 0), 9);
        using var diff = new Mat(); Cv2.Subtract(gray, bg, diff);
        var m = new Mat(); Cv2.Threshold(diff, m, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        return m;
    }

    /// <summary>
    /// 掩膜里值得试的几种窗口：整块、亮点包围盒、按密度峰重新居中的方框。
    /// 匹配框常常没正好套住码（偏几十像素就把码切掉一半），所以宁可给几个候选让解码器自己挑。
    /// 除「整块」外，其余都是新分配的 Mat，调用方负责释放。
    /// </summary>
    static List<(string Name, Mat Crop)> Windows(Mat m)
    {
        var list = new List<(string, Mat)> { ("整块", m) };
        using (var pts = new Mat())
        {
            Cv2.FindNonZero(m, pts);
            if (pts.Rows > 200)
            {
                var bb = Cv2.BoundingRect(pts);
                if (bb.Width >= 24 && bb.Height >= 24) list.Add(("包围盒", new Mat(m, Clamp(bb, m))));
            }
        }
        int side0 = Math.Min(m.Rows, m.Cols);
        foreach (var frac in new[] { 0.5, 0.75, 1.0 })
        {
            int side = (int)(side0 * frac);
            if (side < 40 || side > side0) continue;
            using var den = new Mat(); Cv2.BoxFilter(m, den, MatType.CV_32F, new Size(side, side));
            Cv2.MinMaxLoc(den, out _, out _, out _, out var peak);
            int x = Math.Clamp(peak.X - side / 2, 0, Math.Max(0, m.Cols - side));
            int y = Math.Clamp(peak.Y - side / 2, 0, Math.Max(0, m.Rows - side));
            list.Add(("密度峰" + frac.ToString("0.##"), new Mat(m, new Rect(x, y, side, side))));
        }
        return list;
    }

    /// <summary>
    /// 读码。roi 给了就只在那一块里找（模板匹配定位后传进来，又准又快）；
    /// plainFirst=true 先按普通印刷码读一次，省掉点阵增强的开销。
    /// </summary>
    public static Result Read(Mat image, Rect? roi = null, bool plainFirst = true, string want = "")
    {
        var res = new Result();
        if (image == null || image.Empty()) return res;
        var sw = Stopwatch.StartNew();

        Mat src;
        if (roi is { } r)
        {
            var rr = Clamp(r, image);
            src = rr.Width < 8 || rr.Height < 8 ? new Mat() : new Mat(image, rr);
        }
        else src = image;

        var owned = new List<Mat>();
        using var dec = Decoders.Open(want);
        try
        {
            if (src.Empty()) return res;
            using var gray = ToGray(src);

            if (plainFirst)
            {
                var t0 = dec.Try(gray);
                if (t0.Length > 0)
                {
                    res.Text = t0; res.Format = "普通码"; res.How = "原图直读"; res.Ms = sw.ElapsedMilliseconds;
                    return res;
                }
            }

            foreach (var sc in Scales)
            {
                using var small = new Mat();
                if (Math.Abs(sc - 1.0) < 0.01) gray.CopyTo(small);
                else Cv2.Resize(gray, small, new Size(Math.Max(16, (int)(gray.Width * sc)), Math.Max(16, (int)(gray.Height * sc))),
                    0, 0, InterpolationFlags.Area);

                // 缩小的灰度图本身也值得试一次：模糊插值已经把圆点糊到一起了
                var ts = dec.Try(small);
                if (ts.Length > 0)
                {
                    res.Text = ts; res.Format = "点阵码"; res.How = $"缩到{sc:P0} 灰度直读";
                    res.Ms = sw.ElapsedMilliseconds; return res;
                }

                using var mask = BrightMask(small);
                foreach (var (wname, w) in Windows(mask))
                {
                    if (!ReferenceEquals(w, mask)) owned.Add(w);
                    if (w.Rows < 24 || w.Cols < 24) continue;

                    foreach (var (close, up, blur) in Combos)
                    {
                        using var closed = new Mat(); w.CopyTo(closed);
                        if (close > 1)
                        {
                            using var se = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(close, close));
                            Cv2.MorphologyEx(closed, closed, MorphTypes.Close, se);
                        }
                        using var padded = new Mat();
                        Cv2.CopyMakeBorder(closed, padded, 30, 30, 30, 30, BorderTypes.Constant, new Scalar(0));
                        using var outm = new Mat();
                        if (up > 1) Cv2.Resize(padded, outm, new Size(padded.Cols * up, padded.Rows * up), 0, 0, InterpolationFlags.Nearest);
                        else padded.CopyTo(outm);
                        if (blur > 0) Cv2.GaussianBlur(outm, outm, new Size(blur | 1, blur | 1), 0);

                        var txt = dec.Try(outm);
                        if (txt.Length > 0)
                        {
                            res.Text = txt; res.Format = "点阵码";
                            res.How = $"缩到{sc:P0} {wname} 补点{close} 放大{up}";
                            res.Ms = sw.ElapsedMilliseconds;
                            return res;
                        }
                    }
                }
            }
            res.Ms = sw.ElapsedMilliseconds;
            res.How = "100%~20% 各尺度、各窗口都试过";
            return res;
        }
        finally
        {
            foreach (var o in owned) o.Dispose();
            if (!ReferenceEquals(src, image)) src.Dispose();
        }
    }

    static Rect Clamp(Rect r, Mat m)
    {
        int x = Math.Max(0, Math.Min(r.X, m.Cols - 1));
        int y = Math.Max(0, Math.Min(r.Y, m.Rows - 1));
        int w = Math.Max(1, Math.Min(r.Width, m.Cols - x));
        int h = Math.Max(1, Math.Min(r.Height, m.Rows - y));
        return new Rect(x, y, w, h);
    }
}
