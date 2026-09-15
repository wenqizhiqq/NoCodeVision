using System;
using System.Linq;
using NoCodeVision.Smart.Memory;
using NoCodeVision.Smart.Association;
using NoCodeVision.Smart.Simulation;

namespace NoCodeVision.Smart;

/// <summary>
/// 三模块串联自测：记忆 → 联想 → 仿真。
/// 可作为无界面（headless）运行验证，亦供智能中心页复用同一套逻辑。
/// </summary>
public static class SmartDemo
{
    public static int SelfTest()
    {
        Console.WriteLine("===== NoCodeVision 智能三模块自测 =====");

        // —— 记忆 ——
        var mem = MemoryStore.Seed(240);
        var stats = mem.GetStats();
        Console.WriteLine($"[记忆] 记录总数={stats.Total} 良品={stats.Ok} 不良={stats.Ng} 良率={stats.Yield:P1} 平均节拍={stats.AvgCycleTimeSec:F2}s");
        Console.WriteLine("[记忆] 缺陷分布: " + string.Join(", ",
            stats.DefectCounts.Where(kv => kv.Key != DefectType.None)
                              .Select(kv => $"{kv.Key.Label()}={kv.Value}")));

        // —— 联想 ——
        var assoc = new AssociationEngine(mem);
        assoc.BuildCooccurrence();

        var scratch = mem.Records.FirstOrDefault(r => r.Defect == DefectType.Scratch) ?? mem.Records[0];
        Console.WriteLine($"\n[联想] 以缺陷「{scratch.Defect.Label()}」(ID={scratch.Id}) 为锚点:");
        foreach (var it in assoc.GetRelated(scratch, 5))
            Console.WriteLine($"   关联记录 ID={it.Record.Id} 缺陷={it.Record.Defect.Label()} 相似度={it.Score:F3}");

        var rec = assoc.RecommendParameters(DefectType.Scratch);
        Console.WriteLine($"   参数推荐(置信度={rec.Confidence:P1}, 样本={rec.SampleCount}): " +
                          $"曝光={rec.Suggested.Exposure:F1}ms 增益={rec.Suggested.Gain:F1}dB " +
                          $"阈值={rec.Suggested.Threshold:F2} 光照={rec.Suggested.Lighting:F0}lux");

        var sug = assoc.SuggestInspections(DefectType.Scratch);
        Console.WriteLine("   顺带排查建议: " + (sug.Count == 0 ? "无显著共现" :
            string.Join(", ", sug.Select(x => $"{x.Defect.Label()}({x.Count})"))));

        // —— 仿真 ——
        Console.WriteLine("\n[仿真] 蒙特卡洛推演 120 步 (40 次):");
        var sim = new SimulationEngine(mem).Simulate(120, 40);
        Console.WriteLine($"   预测良率={sim.PredictedYield:P1} (区间 {sim.YieldMin:P1}~{sim.YieldMax:P1})  预测UPH={sim.PredictedUph:F0} 件/小时");
        Console.WriteLine("   缺陷预测: " + string.Join(", ", sim.DefectForecast.Select(kv => $"{kv.Key.Label()}={kv.Value:F1}")));
        Console.WriteLine("   累计良率序列(前10): " + string.Join(" ", sim.YieldSeries.Take(10).Select(y => y.ToString("F2"))));

        Console.WriteLine("===== 自测完成 =====");
        return 0;
    }
}
