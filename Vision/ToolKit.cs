// 从开源项目 OpenCVVision（Apache-2.0）移植改写的基础算子：加强、明暗筛选、区域掩膜、金字塔、
// 镜头标定/去畸变、特征点检测与配准、DNN 目标检测。为 NoCodeVision 适配，非逐行拷贝。
// 适配：温启志

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using OpenCvSharp.Features2D;

namespace NoCodeVision.Vision;

/// <summary>图像增强与筛选：加强(Emphasize) 与 明暗筛选(LightDark)，工业检测里用来对付光照不均和反光。</summary>
public static class ToolKit
{
    /// <summary>加强：dst = src + (src − 邻域均值) × 系数。系数越大边缘越突出，也越容易放大噪点。</summary>
    public static Mat Emphasize(Mat src, int maskWidth, int maskHeight, double factor)
    {
        maskWidth = Math.Max(1, maskWidth);
        maskHeight = Math.Max(1, maskHeight);

        using var mean = new Mat();
        Cv2.Blur(src, mean, new Size(maskWidth, maskHeight));
        var dst = src.EmptyClone();

        if (src.Type() == MatType.CV_8UC1)
        {
            var s = src.GetUnsafeGenericIndexer<byte>();
            var m = mean.GetUnsafeGenericIndexer<byte>();
            var d = dst.GetUnsafeGenericIndexer<byte>();
            Parallel.ForEach(Partitioner.Create(0, src.Rows, Math.Max(1, src.Rows / Environment.ProcessorCount)), band =>
            {
                for (int y = band.Item1; y < band.Item2; y++)
                    for (int x = 0; x < src.Cols; x++)
                        d[y, x] = Sat(Math.Round((s[y, x] - m[y, x]) * factor) + s[y, x]);
            });
        }
        else
        {
            var s = src.GetUnsafeGenericIndexer<Vec3b>();
            var m = mean.GetUnsafeGenericIndexer<Vec3b>();
            var d = dst.GetUnsafeGenericIndexer<Vec3b>();
            Parallel.ForEach(Partitioner.Create(0, src.Rows, Math.Max(1, src.Rows / Environment.ProcessorCount)), band =>
            {
                for (int y = band.Item1; y < band.Item2; y++)
                    for (int x = 0; x < src.Cols; x++)
                        d[y, x] = new Vec3b(
                            Sat(Math.Round((s[y, x].Item0 - m[y, x].Item0) * factor) + s[y, x].Item0),
                            Sat(Math.Round((s[y, x].Item1 - m[y, x].Item1) * factor) + s[y, x].Item1),
                            Sat(Math.Round((s[y, x].Item2 - m[y, x].Item2) * factor) + s[y, x].Item2));
            });
        }
        return dst;
    }

    static byte Sat(double v) => v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)v;

    /// <summary>
    /// 明暗筛选：把图切成小块，取块内中位数当"局部背景"，只保留比背景明显更暗/更亮的像素。
    /// mode 0=只留暗的 1=只留亮的 2=明暗都留。用来消除光照不均（金属、反光面）。
    /// </summary>
    public static Mat LightDarkFilter(Mat gray, int kernelSize, int blockWidth, int mode, double lightScale, double darkScale)
    {
        if (kernelSize < 1) kernelSize = 9;
        if (blockWidth < 1) blockWidth = 3;
        if (kernelSize % blockWidth != 0) blockWidth = 1;
        int blockHeight = kernelSize / blockWidth;
        lightScale = Math.Clamp(lightScale, 0, 100) / 100.0;
        darkScale = Math.Clamp(darkScale, 0, 100) / 100.0;

        var dst = gray.Clone();
        var ind = dst.GetUnsafeGenericIndexer<byte>();
        var tmp = new byte[kernelSize];

        for (int y = 0; y < dst.Rows; y += blockHeight)
        {
            for (int x = 0; x < dst.Cols; x += blockWidth)
            {
                if (x + blockWidth > dst.Cols || y + blockHeight > dst.Rows) continue;

                for (int j = 0; j < blockHeight; j++)
                    for (int k = 0; k < blockWidth; k++)
                        tmp[j * blockWidth + k] = ind[y + j, x + k];

                Array.Sort(tmp, 0, kernelSize);
                double mid = tmp[kernelSize / 2];
                double maxVal = mid + (255 - mid) * lightScale;
                double minVal = mid - mid * darkScale;

                for (int j = 0; j < blockHeight; j++)
                {
                    for (int k = 0; k < blockWidth; k++)
                    {
                        double v = ind[y + j, x + k];
                        bool keep = mode switch
                        {
                            0 => v < minVal,
                            1 => v > maxVal,
                            _ => v < minVal || v > maxVal
                        };
                        ind[y + j, x + k] = keep ? (byte)255 : (byte)0;
                    }
                }
            }
        }
        return dst;
    }

    /// <summary>只留这块 / 挖掉这块。mode 0=只留框内 1=只留框外。</summary>
    public static Mat MaskRegion(Mat src, Rect roi, int mode)
    {
        roi = Clip(roi, src.Size());
        using var mask = new Mat(src.Size(), MatType.CV_8UC1, new Scalar(mode == 0 ? 0 : 255));
        using (var sub = new Mat(mask, roi)) sub.SetTo(mode == 0 ? 255 : 0);
        var dst = Mat.Zeros(src.Size(), src.Type()).ToMat();
        src.CopyTo(dst, mask);
        return dst;
    }

    static Rect Clip(Rect r, Size s)
    {
        int x = Math.Max(0, Math.Min(r.X, s.Width - 1));
        int y = Math.Max(0, Math.Min(r.Y, s.Height - 1));
        return new Rect(x, y, Math.Max(1, Math.Min(r.Width, s.Width - x)), Math.Max(1, Math.Min(r.Height, s.Height - y)));
    }

    /// <summary>拉普拉斯金字塔：缩小 n 次再放大回来取差，得到低频残差，用来看整体明暗漂移。</summary>
    public static Mat LaplacianPyramid(Mat src, int levels)
    {
        levels = Math.Max(1, Math.Min(6, levels));
        using var down = new Mat();
        Cv2.PyrDown(src, down);
        for (int i = 1; i < levels; i++) Cv2.PyrDown(down, down);
        using var up = new Mat();
        Cv2.PyrUp(down, up, src.Size());
        var dst = new Mat();
        Cv2.Absdiff(src, up, dst);
        return dst;
    }

    public static Mat ToGray(Mat src)
    {
        var g = new Mat();
        if (src.Channels() > 1) Cv2.CvtColor(src, g, ColorConversionCodes.BGR2GRAY);
        else src.CopyTo(g);
        return g;
    }
}

/// <summary>镜头标定与去畸变（棋盘格）。畸变是卡尺精度的头号杀手，标定一次存盘长期复用。</summary>
public sealed class LensCalibration
{
    public static readonly string StoreDir = Path.Combine(AppContext.BaseDirectory, "Data", "Calib");
    static readonly string StoreFile = "lens.yaml";

    public Mat CameraMatrix { get; private set; } = new();
    public Mat DistCoeffs { get; private set; } = new();
    public Size ImageSize { get; private set; }
    public double ReprojectError { get; private set; } = -1;
    public bool IsLoaded => !CameraMatrix.Empty() && !DistCoeffs.Empty();

    Mat? _map1, _map2;
    Size _mapFor;

    public static LensCalibration Shared { get; } = new();

    /// <summary>用一批棋盘格照片标定，返回（用到的张数，平均重投影误差 mm）。</summary>
    public (int used, double error) CalibrateFromFolder(string folder, int boardCols, int boardRows, double squareMm)
    {
        if (boardCols < 4 || boardRows < 4) return (0, -1);
        var boardSize = new Size(boardCols, boardRows);

        var oneBoard = new Point3f[boardCols * boardRows];
        for (int r = 0; r < boardRows; r++)
            for (int c = 0; c < boardCols; c++)
                oneBoard[r * boardCols + c] = new Point3f(c * (float)squareMm, r * (float)squareMm, 0);

        var objMats = new List<Mat>();
        var imgMats = new List<Mat>();
        Size imgSize = default;

        var files = Directory.Exists(folder)
            ? Directory.GetFiles(folder).Where(p => new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(Path.GetExtension(p).ToLowerInvariant())).ToList()
            : new List<string>();

        foreach (var path in files)
        {
            using var src = Cv2.ImRead(path, ImreadModes.Color);
            if (src.Empty()) continue;
            using var gray = ToolKit.ToGray(src);
            imgSize = src.Size();

            if (!Cv2.FindChessboardCorners(gray, boardSize, out Point2f[] found)) continue;
            Cv2.Find4QuadCornerSubpix(gray, found, new Size(11, 11));

            objMats.Add(Mat.FromArray(oneBoard));
            imgMats.Add(Mat.FromArray(found));
        }

        if (objMats.Count < 3)
        {
            foreach (var m in objMats) m.Dispose();
            foreach (var m in imgMats) m.Dispose();
            return (objMats.Count, -1);
        }

        double rms;
        try
        {
            rms = Cv2.CalibrateCamera(objMats, imgMats,
                imgSize, CameraMatrix, DistCoeffs, out _, out _);
        }
        finally
        {
            foreach (var m in objMats) m.Dispose();
            foreach (var m in imgMats) m.Dispose();
        }

        ImageSize = imgSize;
        ReprojectError = rms;
        _map1?.Dispose(); _map2?.Dispose();
        _map1 = null; _map2 = null;
        Save();
        return (imgMats.Count, rms);
    }

    public void Save()
    {
        if (!IsLoaded) return;
        Directory.CreateDirectory(StoreDir);
        using var fs = new FileStorage(Path.Combine(StoreDir, StoreFile), FileStorage.Modes.Write);
        fs.Write("k", CameraMatrix);
        fs.Write("d", DistCoeffs);
        fs.Write("w", ImageSize.Width);
        fs.Write("h", ImageSize.Height);
        fs.Write("rms", ReprojectError);
    }

    public bool TryLoad()
    {
        if (IsLoaded) return true;
        var path = Path.Combine(StoreDir, StoreFile);
        if (!System.IO.File.Exists(path)) return false;
        try
        {
            using var fs = new FileStorage(path, FileStorage.Modes.Read);
            if (!fs.IsOpened()) return false;
            CameraMatrix = fs["k"]!.ReadMat();
            DistCoeffs = fs["d"]!.ReadMat();
            ImageSize = new Size((int)fs["w"]!.ReadInt(), (int)fs["h"]!.ReadInt());
            ReprojectError = fs["rms"]!.ReadDouble();
            return IsLoaded;
        }
        catch { return false; }
    }

    /// <summary>去畸变。没标定过就原样返回。</summary>
    public Mat Undistort(Mat src)
    {
        if (!TryLoad()) return src.Clone();
        if (_map1 == null || _map2 == null || _mapFor != src.Size())
        {
            _map1?.Dispose(); _map2?.Dispose();
            _map1 = new Mat(); _map2 = new Mat();
            Cv2.InitUndistortRectifyMap(CameraMatrix, DistCoeffs, new Mat(), CameraMatrix, src.Size(), MatType.CV_32FC1, _map1, _map2);
            _mapFor = src.Size();
        }
        var dst = new Mat();
        Cv2.Remap(src, dst, _map1, _map2, InterpolationFlags.Linear);
        return dst;
    }
}

/// <summary>特征点：检测 + 描述子匹配 + 仿射配准，用于"先找特征再把测量框对齐过去"。</summary>
public static class FeatureTools
{
    public static readonly string[] Detectors = { "ORB", "SIFT", "AKAZE", "BRISK", "FAST", "角点(GFTT)" };

    public static (KeyPoint[] kp, Mat desc) Detect(string detector, Mat gray, int maxPoints)
    {
        if (maxPoints <= 0) maxPoints = 500;
        Feature2D f = detector switch
        {
            "SIFT" => SIFT.Create(maxPoints),
            "AKAZE" => AKAZE.Create(),
            "BRISK" => BRISK.Create(),
            "FAST" => FastFeatureDetector.Create(15, true),
            "角点(GFTT)" => GFTTDetector.Create(maxPoints, 0.01, 8.0),
            _ => ORB.Create(maxPoints)
        };
        var desc = new Mat();
        KeyPoint[] kp;
        using (f) f.DetectAndCompute(gray, null, out kp, desc);
        return (kp, desc);
    }

    /// <summary>把 img2 按特征对应关系仿射对齐到 img1，返回匹配数与对齐后的图。</summary>
    public static (int matched, Mat? aligned) Register(string detector, Mat img1, Mat img2, bool useFlann)
    {
        using var g1 = ToolKit.ToGray(img1);
        using var g2 = ToolKit.ToGray(img2);
        var (kp1, d1) = Detect(detector, g1, 1000);
        var (kp2, d2) = Detect(detector, g2, 1000);

        if (kp1.Length < 3 || kp2.Length < 3 || d1.Empty() || d2.Empty())
        {
            d1.Dispose(); d2.Dispose();
            return (0, null);
        }

        DMatch[][] knn;
        using (var matcher = useFlann
            ? (DescriptorMatcher)new FlannBasedMatcher()
            : new BFMatcher(NormType(detector)))
        {
            try { knn = matcher.KnnMatch(d1, d2, 2); }
            catch { knn = Array.Empty<DMatch[]>(); }
        }
        d1.Dispose(); d2.Dispose();

        var good = new List<DMatch>();
        foreach (var pair in knn.Where(x => x != null && x.Length >= 2))
            if (pair[0].Distance < 0.7f * pair[1].Distance) good.Add(pair[0]);

        if (good.Count < 3) return (good.Count, null);

        var src = good.Select(g => kp2[g.TrainIdx].Pt).ToArray();
        var dst = good.Select(g => kp1[g.QueryIdx].Pt).ToArray();
        using var aff = Cv2.GetAffineTransform(src, dst);
        var aligned = new Mat();
        Cv2.WarpAffine(img2, aligned, aff, img2.Size());
        return (good.Count, aligned);
    }

    static NormTypes NormType(string detector) =>
        detector is "ORB" or "BRISK" ? NormTypes.Hamming : NormTypes.L2;
}

/// <summary>DNN 目标检测：优先 ONNX，其次 Darknet(.weights + .cfg)。模型由用户自己放到 Data\Models。</summary>
public static class DnnDetect
{
    public static readonly string ModelDir = Path.Combine(AppContext.BaseDirectory, "Data", "Models");

    public static string? FindModel(string name, params string[] exts)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            if (System.IO.File.Exists(name)) return name;
            foreach (var e in exts)
            {
                var guess = Path.Combine(ModelDir, name.EndsWith(e, StringComparison.OrdinalIgnoreCase) ? name : name + e);
                if (System.IO.File.Exists(guess)) return guess;
            }
        }
        if (!Directory.Exists(ModelDir)) return null;
        return Directory.GetFiles(ModelDir)
            .FirstOrDefault(p => exts.Contains(Path.GetExtension(p).ToLowerInvariant()));
    }

    public static List<string> LoadLabels(string? labelsPath)
    {
        string? file = null;
        if (!string.IsNullOrWhiteSpace(labelsPath))
            file = System.IO.File.Exists(labelsPath) ? labelsPath : Path.Combine(ModelDir, labelsPath);
        if (file == null || !System.IO.File.Exists(file))
            file = Directory.Exists(ModelDir) ? Directory.GetFiles(ModelDir, "*.txt").FirstOrDefault() : null;
        if (file == null || !System.IO.File.Exists(file)) return new List<string>();
        return System.IO.File.ReadAllLines(file).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    public record Box(float X, float Y, float W, float H, float Score, int Cls);

    public static List<Box> Run(Mat img, string model, string? cfg, string? labels, double conf, double nms, out string error)
    {
        error = "";
        Net? net = null;
        try
        {
            var ext = Path.GetExtension(model).ToLowerInvariant();
            net = ext == ".onnx" ? CvDnn.ReadNetFromOnnx(model) : CvDnn.ReadNetFromDarknet(cfg ?? "", model);
            net!.SetPreferableBackend(Backend.OPENCV);
            net.SetPreferableTarget(Target.CPU);

            using var blob = CvDnn.BlobFromImage(img, 1 / 255.0, new Size(640, 640), new Scalar(), true, false);
            net.SetInput(blob);
            using var outMat = net.Forward();
            return Parse(outMat, img.Size(), conf, nms);
        }
        catch (Exception ex) { error = ex.Message; return new List<Box>(); }
        finally { net?.Dispose(); }
    }

    static List<Box> Parse(Mat output, Size img, double conf, double nms)
    {
        if (output.Empty()) return new List<Box>();
        var data = new float[output.Total()];
        output.GetArray(out data);

        int n, dim;
        bool transposed = false;
        if (output.Dims >= 3)
        {
            int d1 = output.Size(1), d2 = output.Size(2);
            if (d2 > d1) { n = d2; dim = d1; transposed = true; }
            else { n = d1; dim = d2; }
        }
        else { n = output.Rows; dim = output.Cols; }

        float At(int i, int j) => transposed ? data[j * n + i] : data[i * dim + j];
        int classes = Math.Max(1, dim - 5);

        var boxes = new List<Box>();
        for (int i = 0; i < n; i++)
        {
            float best = 0; int cls = 0;
            if (dim >= 6)
            {
                for (int j = 5; j < dim; j++)
                {
                    float s = At(i, j);
                    if (s > best) { best = s; cls = j - 5; }
                }
                if (dim >= 85) best = Math.Max(At(i, 4), best);       // Darknet v3：obj 置信度 × 类别概率
                else best *= At(i, 4) < 2 ? 1 : 1;                     // ONNX(YOLOv5/v8) 第 4 列已是 obj
            }
            else best = At(i, 4);

            if (best < conf) continue;

            float cx = At(i, 0), cy = At(i, 1), w = At(i, 2), h = At(i, 3);
            boxes.Add(new Box((cx - w / 2) * img.Width / 640f, (cy - h / 2) * img.Height / 640f,
                w * img.Width / 640f, h * img.Height / 640f, best, cls));
        }

        if (boxes.Count == 0) return boxes;

        int[] keep;
        CvDnn.NMSBoxes(boxes.Select(b => new Rect((int)b.X, (int)b.Y, (int)b.W, (int)b.H)).ToList(),
            boxes.Select(b => b.Score).ToList(), (float)conf, (float)nms, out keep);
        return keep.Select(k => boxes[k]).ToList();
    }

    public static void Draw(Mat img, IReadOnlyCollection<Box> boxes, IReadOnlyList<string> labels)
    {
        foreach (var b in boxes)
        {
            var r = new Rect((int)b.X, (int)b.Y, (int)b.W, (int)b.H);
            Cv2.Rectangle(img, r, new Scalar(0, 200, 90), 2);
            var name = labels.Count > b.Cls ? labels[b.Cls] : b.Cls.ToString();
            Cv2.PutText(img, $"{name} {b.Score:F2}", new Point(r.X, Math.Max(12, r.Y - 4)),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 200, 90), 1);
        }
    }
}
