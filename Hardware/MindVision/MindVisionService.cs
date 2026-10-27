using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace NoCodeVision.Hardware;

/// <summary>
/// 迈德威视相机服务：跑流程和界面「抓一张」共用同一个已打开的相机，避免每抓一帧都重新初始化。
/// 没装 SDK 或没接相机时只返回 null + 一句原因，绝不抛异常。
/// </summary>
public sealed class MindVisionService
{
    public static MindVisionService Instance { get; } = new();

    private readonly object _gate = new();
    private MindVisionCamera? _cam;
    private string _key = "";

    public List<MindVisionDeviceInfo> Devices { get; private set; } = new();
    public string Status { get; private set; } = "未检测";
    public string LastError { get; private set; } = "";

    /// <summary>重新枚举相机（界面「🔄 刷新」按钮）。</summary>
    public void Refresh()
    {
        Devices = MindVisionCamera.Enumerate();
        Status = !MindVisionCamera.SdkAvailable ? MindVisionCamera.SdkError
               : Devices.Count == 0 ? "SDK 正常，但没找到相机（查 USB/网口和电源）"
               : $"找到 {Devices.Count} 台相机";
    }

    /// <summary>抓一帧 BGR 图；失败时 message 里带原因。</summary>
    public Mat? Grab(int index, bool lineScan, int lineRate, int spliceLines, out string message)
    {
        lock (_gate)
        {
            message = "";
            if (!MindVisionCamera.SdkAvailable)
            {
                message = MindVisionCamera.SdkError;
                LastError = message;
                return null;
            }

            var key = $"{index}|{lineScan}|{lineRate}|{spliceLines}";
            if (_cam == null || _key != key)
            {
                CloseCore();
                var cam = new MindVisionCamera(index)
                {
                    LineScan = lineScan,
                    LineRateHz = lineRate,
                    SpliceLines = spliceLines,
                };
                cam.Log += s => LastError = s;
                if (!cam.Open())
                {
                    message = string.IsNullOrWhiteSpace(LastError) ? "打开迈德威视相机失败" : LastError;
                    cam.Dispose();
                    return null;
                }
                _cam = cam;
                _key = key;
            }

            var mat = _cam.GrabMat(1200);
            if (mat == null)
            {
                message = "抓图超时：相机没出图（检查触发方式、曝光、线扫行频）";
                LastError = message;
                return null;
            }
            message = $"{_cam.DeviceName} {mat.Width}×{mat.Height}";
            return mat;
        }
    }

    public void Close() => CloseCore();

    private void CloseCore()
    {
        try { _cam?.Dispose(); } catch { }
        _cam = null;
        _key = "";
    }
}
