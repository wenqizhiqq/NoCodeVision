// 标定照片批量找 mark：给一个文件夹 + 一张模板图，把每张照片里 mark 的像素位置和角度都找出来。
// 实现：温启志

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCvSharp;
using GrayMatch;

namespace NoCodeVision.Vision;

/// <summary>一张标定照片里找到的 mark。</summary>
public sealed record CalibShot(string File, double X, double Y, double Angle, double Score);

/// <summary>标定用的「一批照片 → 每张里 mark 在哪」。</summary>
public static class PhotoCalibration
{
    static readonly string[] Ext = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

    /// <summary>
    /// 按文件名顺序处理文件夹里的每张照片：用模板图找 mark，拿最好的那个。
    /// 返回的列表和文件顺序一一对应（没找到的会跳过并在 note 里点名）。
    /// </summary>
    public static List<CalibShot> FindAll(string folder, string templatePath, out string note,
        double scoreThreshold = 0.6, int pyramid = 0,
        double angleStart = -20, double angleStop = 20, double angleStep = 1)
    {
        var list = new List<CalibShot>();
        note = "";
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { note = "照片文件夹不存在：先选一个存标定照片的文件夹"; return list; }
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath)) { note = "模板图不存在：先在图上框住 mark 存一张模板图"; return list; }

        var files = Directory.EnumerateFiles(folder)
            .Where(f => Ext.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0) { note = "文件夹里没有图片"; return list; }

        using var tpl = Cv2.ImRead(templatePath, ImreadModes.Grayscale);
        if (tpl.Empty()) { note = "模板图读不出来：" + Path.GetFileName(templatePath); return list; }

        var matcher = new RotatedTemplateMatcher();
        int miss = 0;
        foreach (var f in files)
        {
            using var src = Cv2.ImRead(f, ImreadModes.Color);
            if (src.Empty()) { miss++; continue; }
            matcher.SetSource(src);
            matcher.SetTemplate(tpl);
            matcher.ScaleRange = 0;
            List<MatchResult> res;
            try { res = matcher.Match(pyramid, angleStart, angleStop, angleStep, scoreThreshold, 0.3, 1, 0); }
            catch { miss++; continue; }
            if (res == null || res.Count == 0) { miss++; continue; }
            var r = res.OrderByDescending(x => x.Score).First();
            list.Add(new CalibShot(Path.GetFileName(f), r.CenterX, r.CenterY, r.Angle, r.Score));
        }
        if (miss > 0) note = $"{miss} 张没找到 mark（模板或相似度要调一下）";
        return list;
    }
}