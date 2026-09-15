using System;
using System.Collections.Generic;

namespace NoCodeVision.Smart.Memory;

/// <summary>缺陷类型（检测业务域）</summary>
public enum DefectType
{
    None = 0,      // 良品
    Scratch,       // 划痕
    Stain,         // 脏污
    Missing,       // 缺料
    Misalign,      // 错位
    Color,         // 色差
    Dimension,     // 尺寸超差
}

public static class DefectTypeExtensions
{
    public static string Label(this DefectType t) => t switch
    {
        DefectType.None => "良品",
        DefectType.Scratch => "划痕",
        DefectType.Stain => "脏污",
        DefectType.Missing => "缺料",
        DefectType.Misalign => "错位",
        DefectType.Color => "色差",
        DefectType.Dimension => "尺寸超差",
        _ => t.ToString()
    };
}

/// <summary>一次检测的关键参数快照（曝光/增益/阈值/光照/对比度）</summary>
public sealed class ParameterSet
{
    public double Exposure { get; set; }   // ms
    public double Gain { get; set; }       // dB
    public double Threshold { get; set; }  // 0..1
    public double Lighting { get; set; }   // lux
    public double Contrast { get; set; }   // 0..1

    public ParameterSet Clone() => new()
    {
        Exposure = Exposure,
        Gain = Gain,
        Threshold = Threshold,
        Lighting = Lighting,
        Contrast = Contrast,
    };

    public double[] ToVector() => new[] { Exposure, Gain, Threshold, Lighting, Contrast };
}

/// <summary>单条检测记录——记忆的基本单元</summary>
public sealed class InspectionRecord
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string WorkpieceId { get; set; } = "";   // 同工件多个相机结果可共享
    public string CameraId { get; set; } = "";
    public DefectType Defect { get; set; }
    public bool IsOk => Defect == DefectType.None;
    public int Severity { get; set; }              // 1..5（良品为 0）
    public double CycleTimeSec { get; set; }       // 单件节拍
    public ParameterSet Parameters { get; set; } = new();
    public double[] Feature { get; set; } = Array.Empty<double>(); // 图像特征向量
    public List<string> Tags { get; set; } = new();
    public string Notes { get; set; } = "";
}

/// <summary>缺陷样本——记忆中“某类缺陷长什么样”的范式</summary>
public sealed class DefectSample
{
    public DefectType Defect { get; set; }
    public double[] Feature { get; set; } = Array.Empty<double>();
    public string Description { get; set; } = "";
}

/// <summary>记忆统计摘要</summary>
public sealed class MemoryStats
{
    public int Total;
    public int Ok;
    public int Ng;
    public double Yield => Total == 0 ? 0 : (double)Ok / Total;
    public Dictionary<DefectType, int> DefectCounts = new();
    public double AvgCycleTimeSec;
}
