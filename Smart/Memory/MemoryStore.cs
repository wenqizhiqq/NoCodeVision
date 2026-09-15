using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NoCodeVision.Smart.Memory;

/// <summary>
/// 记忆模块：负责检测业务数据的存储、检索、统计与持久化。
/// 同时维护“缺陷样本范式”，为联想机制提供特征比对基准。
/// </summary>
public sealed class MemoryStore
{
    private readonly List<InspectionRecord> _records = new();
    private readonly List<DefectSample> _samples = new();
    private int _nextId = 1;

    public IReadOnlyList<InspectionRecord> Records => _records;
    public IReadOnlyList<DefectSample> Samples => _samples;

    public void Add(InspectionRecord r)
    {
        if (r.Id <= 0) r.Id = _nextId++;
        else _nextId = Math.Max(_nextId, r.Id + 1);
        _records.Add(r);
    }

    public void AddSample(DefectSample s) => _samples.Add(s);

    /// <summary>按时间倒序回放最近 n 条（记忆回溯）</summary>
    public IEnumerable<InspectionRecord> Recall(int n = 50)
        => _records.OrderByDescending(r => r.Timestamp).Take(n);

    public IEnumerable<InspectionRecord> QueryByDefect(DefectType d)
        => _records.Where(r => r.Defect == d);

    public IEnumerable<InspectionRecord> QueryByTag(string tag)
        => _records.Where(r => r.Tags.Contains(tag));

    public MemoryStats GetStats()
    {
        var s = new MemoryStats
        {
            Total = _records.Count,
            Ok = _records.Count(r => r.IsOk),
            Ng = _records.Count(r => !r.IsOk),
            AvgCycleTimeSec = _records.Count == 0 ? 0 : _records.Average(r => r.CycleTimeSec),
        };
        foreach (var g in _records.GroupBy(r => r.Defect))
            s.DefectCounts[g.Key] = g.Count();
        return s;
    }

    public void Save(string path)
    {
        var dto = new MemoryDto { Records = _records, Samples = _samples };
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public void Load(string path)
    {
        if (!File.Exists(path)) return;
        var dto = JsonSerializer.Deserialize<MemoryDto>(File.ReadAllText(path));
        if (dto == null) return;
        _records.Clear();
        _samples.Clear();
        _records.AddRange(dto.Records);
        _samples.AddRange(dto.Samples);
        _nextId = _records.Count == 0 ? 1 : _records.Max(r => r.Id) + 1;
    }

    private sealed class MemoryDto
    {
        public List<InspectionRecord> Records { get; set; } = new();
        public List<DefectSample> Samples { get; set; } = new();
    }

    /// <summary>种子数据：让“运行演示”无需手工录入即可看到效果</summary>
    public static MemoryStore Seed(int count = 240)
    {
        var rnd = new Random(20260915);
        var store = new MemoryStore();
        var defects = new[]
        {
            DefectType.Scratch, DefectType.Stain, DefectType.Missing,
            DefectType.Misalign, DefectType.Color, DefectType.Dimension,
        };
        // 缺陷范式（特征中心），用于生成相似特征
        var centers = defects.ToDictionary(d => d, d =>
        {
            var v = new double[8];
            for (int i = 0; i < v.Length; i++) v[i] = rnd.NextDouble();
            return v;
        });
        for (int i = 0; i < count; i++)
        {
            bool ok = rnd.NextDouble() < 0.85;
            var defect = ok ? DefectType.None : defects[rnd.Next(defects.Length)];
            var p = new ParameterSet
            {
                Exposure = 8 + rnd.NextDouble() * 14,
                Gain = 10 + rnd.NextDouble() * 20,
                Threshold = 0.4 + rnd.NextDouble() * 0.4,
                Lighting = 300 + rnd.NextDouble() * 500,
                Contrast = 0.3 + rnd.NextDouble() * 0.6,
            };
            double[] feat = ok
                ? Enumerable.Range(0, 8).Select(_ => rnd.NextDouble()).ToArray()
                : centers[defect].Select(x => Clamp01(x + (rnd.NextDouble() - 0.5) * 0.2)).ToArray();
            store.Add(new InspectionRecord
            {
                Timestamp = DateTime.Now.AddMinutes(-(count - i) * 1.7),
                WorkpieceId = "WP-" + (1000 + i / 3),
                CameraId = "Camera_" + (i % 3),
                Defect = defect,
                Severity = ok ? 0 : 1 + rnd.Next(5),
                CycleTimeSec = 1.6 + rnd.NextDouble() * 1.2,
                Parameters = p,
                Feature = feat,
            });
        }
        foreach (var d in defects)
            store.AddSample(new DefectSample { Defect = d, Feature = centers[d], Description = d.Label() + " 典型特征" });
        return store;
    }

    private static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;
}
