using System;
using System.Collections.Generic;
using System.Linq;
using NoCodeVision.Smart.Memory;
using NoCodeVision.Smart.Association;

namespace NoCodeVision.Smart.Simulation;

/// <summary>仿真推演结果</summary>
public sealed class SimulationResult
{
    public int Steps;
    public int PredictedOk;
    public int PredictedNg;
    public double PredictedYield;
    public double PredictedUph;       // 件/小时
    public Dictionary<DefectType, int> DefectForecast = new();
    public List<double> YieldSeries = new();   // 每步累计良率
    public double YieldMin;
    public double YieldMax;
}

/// <summary>
/// 仿真模块：基于历史分布做蒙特卡洛推演，预测未来 N 次检测的
/// 良率、产能(UPH)、缺陷分布，并给出累计良率曲线与置信区间。
/// </summary>
public sealed class SimulationEngine
{
    private readonly MemoryStore _mem;

    public SimulationEngine(MemoryStore mem) => _mem = mem;

    /// <summary>蒙特卡洛推演未来 steps 次检测（runs 次重复取均值）</summary>
    public SimulationResult Simulate(int steps, int runs = 40)
    {
        var stats = _mem.GetStats();
        double baseOk = stats.Total == 0 ? 0.85 : stats.Yield;

        // 历史 NG 中各类缺陷占比
        var ngDefects = _mem.Records.Where(r => !r.IsOk).ToList();
        var dist = new Dictionary<DefectType, double>();
        foreach (DefectType d in Enum.GetValues<DefectType>())
        {
            if (d == DefectType.None) continue;
            dist[d] = ngDefects.Count == 0 ? 0.0 : (double)ngDefects.Count(x => x.Defect == d) / ngDefects.Count;
        }
        double avgCycle = stats.AvgCycleTimeSec <= 0 ? 2.0 : stats.AvgCycleTimeSec;

        var yields = new List<double>(runs);
        int totalOk = 0, totalNg = 0;
        var totalDefect = new Dictionary<DefectType, int>();
        var seriesAcc = new double[steps];

        for (int run = 0; run < runs; run++)
        {
            var rnd = new Random(1000 + run);
            int ok = 0, ng = 0;
            for (int s = 0; s < steps; s++)
            {
                if (rnd.NextDouble() < baseOk)
                {
                    ok++;
                }
                else
                {
                    ng++;
                    var d = SampleDefect(dist, rnd);
                    totalDefect[d] = totalDefect.GetValueOrDefault(d) + 1;
                }
                seriesAcc[s] += (double)ok / (s + 1);
            }
            totalOk += ok;
            totalNg += ng;
            yields.Add((double)ok / steps);
        }

        var result = new SimulationResult
        {
            Steps = steps,
            PredictedOk = totalOk / runs,
            PredictedNg = totalNg / runs,
            PredictedYield = yields.Average(),
            PredictedUph = 3600.0 / avgCycle, // 单件节拍决定理论产能
            YieldMin = yields.Min(),
            YieldMax = yields.Max(),
        };
        foreach (var kv in totalDefect)
            result.DefectForecast[kv.Key] = kv.Value / runs;
        for (int s = 0; s < steps; s++)
            result.YieldSeries.Add(seriesAcc[s] / runs);
        return result;
    }

    private static DefectType SampleDefect(Dictionary<DefectType, double> dist, Random rnd)
    {
        double r = rnd.NextDouble();
        double acc = 0;
        foreach (var kv in dist)
        {
            acc += kv.Value;
            if (r <= acc) return kv.Key;
        }
        return dist.Keys.FirstOrDefault();
    }
}
