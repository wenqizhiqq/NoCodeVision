using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using NoCodeVision.Smart.Memory;
using NoCodeVision.Smart.Association;
using NoCodeVision.Smart.Simulation;

namespace NoCodeVision;

/// <summary>
/// 智能中心页 ViewModel：把记忆 / 联想 / 仿真三模块串成可交互演示。
/// 为避免 WPF 的 wpftmp 临时工程无法解析主程序集中的 ViewModelBase/RelayCommand，
/// 此处内置最小 INPC 与命令实现，保证既能编进主程序集，也能被 XAML 临时工程独立编译。
/// </summary>
public class SmartCenterViewModel : INotifyPropertyChanged
{
    private MemoryStore _mem = null!;
    private AssociationEngine _assoc = null!;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool SetField<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    public ObservableCollection<string> LogLines { get; } = new();
    public ObservableCollection<string> RelatedLines { get; } = new();
    public ObservableCollection<string> ForecastLines { get; } = new();

    public string StatsText { get => _statsText; private set => SetField(ref _statsText, value, nameof(StatsText)); }
    private string _statsText = "";

    public string RecText { get => _recText; private set => SetField(ref _recText, value, nameof(RecText)); }
    private string _recText = "";

    public double PredYield { get => _predYield; private set => SetField(ref _predYield, value, nameof(PredYield)); }
    private double _predYield;

    public double PredUph { get => _predUph; private set => SetField(ref _predUph, value, nameof(PredUph)); }
    private double _predUph;

    public string SimPolylinePoints { get => _simPts; private set => SetField(ref _simPts, value, nameof(SimPolylinePoints)); }
    private string _simPts = "";

    public ICommand RunDemoCmd { get; }

    public SmartCenterViewModel()
    {
        RunDemoCmd = new MiniCommand(_ => RunDemo());
        Init();
    }

    private void Init()
    {
        _mem = MemoryStore.Seed(240);
        _assoc = new AssociationEngine(_mem);
        _assoc.BuildCooccurrence();
        var s = _mem.GetStats();
        StatsText = $"记录 {s.Total} · 良品 {s.Ok} · 不良 {s.Ng} · 良率 {s.Yield:P1} · 节拍 {s.AvgCycleTimeSec:F2}s";
    }

    private void RunDemo()
    {
        LogLines.Clear();
        RelatedLines.Clear();
        ForecastLines.Clear();

        // —— 记忆 ——
        LogLines.Add("[记忆] 已载入 " + _mem.Records.Count + " 条检测记录");

        // —— 联想 ——
        var anchor = _mem.Records.First(r => r.Defect == DefectType.Scratch);
        RelatedLines.Add("锚点缺陷：" + anchor.Defect.Label() + " (ID=" + anchor.Id + ")");
        foreach (var it in _assoc.GetRelated(anchor, 5))
            RelatedLines.Add($"  关联 ID={it.Record.Id} {it.Record.Defect.Label()} 相似度 {it.Score:F3}");

        var rec = _assoc.RecommendParameters(DefectType.Scratch);
        RecText = $"曝光 {rec.Suggested.Exposure:F1}ms · 增益 {rec.Suggested.Gain:F1}dB · 阈值 {rec.Suggested.Threshold:F2} · 光照 {rec.Suggested.Lighting:F0}lux（置信 {rec.Confidence:P1}）";

        // —— 仿真 ——
        var sim = new SimulationEngine(_mem).Simulate(120, 40);
        PredYield = sim.PredictedYield;
        PredUph = sim.PredictedUph;
        ForecastLines.Add($"预测良率 {sim.PredictedYield:P1}（区间 {sim.YieldMin:P1}~{sim.YieldMax:P1}）");
        ForecastLines.Add($"预测 UPH {sim.PredictedUph:F0} 件/小时");
        foreach (var kv in sim.DefectForecast)
            ForecastLines.Add($"  {kv.Key.Label()} ≈ {kv.Value:F1} 件");

        RebuildPolyline(sim.YieldSeries);
        LogLines.Add("[仿真] 完成 120 步 / 40 次推演");
    }

    private void RebuildPolyline(IList<double> series)
    {
        if (series.Count < 2) { SimPolylinePoints = ""; return; }
        const double W = 320, H = 140, pad = 10;
        double min = 0, max = 1; // 良率区间 0..1
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < series.Count; i++)
        {
            double x = pad + (double)i / (series.Count - 1) * (W - 2 * pad);
            double y = H - pad - (series[i] - min) / (max - min) * (H - 2 * pad);
            sb.Append(x.ToString("F1")).Append(',').Append(y.ToString("F1")).Append(' ');
        }
        SimPolylinePoints = sb.ToString().Trim();
    }

    private sealed class MiniCommand : ICommand
    {
        private readonly Action<object?> _exec;
        public MiniCommand(Action<object?> exec) => _exec = exec;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _exec(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
