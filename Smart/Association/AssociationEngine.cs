using System;
using System.Collections.Generic;
using System.Linq;
using NoCodeVision.Smart.Memory;

namespace NoCodeVision.Smart.Association;

/// <summary>与查询记录相似的历史条目</summary>
public sealed class RelatedItem
{
    public InspectionRecord Record = null!;
    public double Score; // 余弦相似度 0..1
}

/// <summary>参数推荐结果</summary>
public sealed class ParameterRecommendation
{
    public DefectType Defect;
    public ParameterSet Suggested = new();
    public double Confidence; // 0..1
    public int SampleCount;
}

/// <summary>
/// 联想机制：在记忆之上建立关联。
///  - 特征空间相似度（余弦）→ 检索相似历史
///  - 同工件共现 → 构建缺陷关联矩阵，给出顺带排查建议
///  - 历史 OK 样本聚类 → 推荐最优检测参数
/// </summary>
public sealed class AssociationEngine
{
    private readonly MemoryStore _mem;
    private readonly Dictionary<(DefectType, DefectType), int> _co = new();

    public AssociationEngine(MemoryStore mem) => _mem = mem;

    /// <summary>基于同工件共现，构建缺陷关联矩阵（联想的记忆基础）</summary>
    public void BuildCooccurrence()
    {
        _co.Clear();
        var byWp = _mem.Records.Where(r => !r.IsOk).GroupBy(r => r.WorkpieceId);
        foreach (var g in byWp)
        {
            var ds = g.Select(r => r.Defect).Distinct().Where(d => d != DefectType.None).ToList();
            for (int i = 0; i < ds.Count; i++)
                for (int j = 0; j < ds.Count; j++)
                {
                    var key = (ds[i], ds[j]);
                    _co[key] = _co.GetValueOrDefault(key) + 1;
                }
        }
    }

    /// <summary>在特征空间与查询记录最相似的 K 条历史</summary>
    public List<RelatedItem> GetRelated(InspectionRecord query, int k = 5)
    {
        return _mem.Records
            .Where(r => r.Id != query.Id)
            .Select(r => new RelatedItem { Record = r, Score = Cosine(query.Feature, r.Feature) })
            .OrderByDescending(x => x.Score)
            .Take(k)
            .ToList();
    }

    /// <summary>
    /// 参数推荐：返回“健康(良品)参数画像”，即历史上产出良品时的典型参数组合，
    /// 作为操作员调参的目标区间；置信度取整体良率，反映该画像的可靠程度。
    /// 当良品样本不足时回退到全体样本均值。
    /// </summary>
    public ParameterRecommendation RecommendParameters(DefectType defect)
    {
        var okRecs = _mem.Records.Where(r => r.IsOk).ToList();
        var allRecs = _mem.Records.ToList();
        var rec = new ParameterRecommendation { Defect = defect, SampleCount = okRecs.Count };
        var src = okRecs.Count > 0 ? okRecs : allRecs;
        rec.Suggested = new ParameterSet
        {
            Exposure = src.Average(r => r.Parameters.Exposure),
            Gain = src.Average(r => r.Parameters.Gain),
            Threshold = src.Average(r => r.Parameters.Threshold),
            Lighting = src.Average(r => r.Parameters.Lighting),
            Contrast = src.Average(r => r.Parameters.Contrast),
        };
        rec.Confidence = allRecs.Count == 0 ? 0 : (double)okRecs.Count / allRecs.Count;
        return rec;
    }

    /// <summary>若当前出现 defect，建议顺带排查哪些高共现缺陷</summary>
    public List<(DefectType Defect, int Count)> SuggestInspections(DefectType defect)
    {
        return _co.Where(kv => kv.Key.Item1 == defect && kv.Key.Item2 != defect)
                  .GroupBy(kv => kv.Key.Item2)
                  .Select(g => (g.Key, g.Sum(kv => kv.Value)))
                  .OrderByDescending(x => x.Item2)
                  .ToList();
    }

    public static double Cosine(double[] a, double[] b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
