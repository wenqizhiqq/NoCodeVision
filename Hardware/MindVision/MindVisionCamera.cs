using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MVSDK;
using OpenCvSharp;

namespace NoCodeVision.Hardware;

/// <summary>一台迈德威视相机（枚举结果，给界面下拉框用）。</summary>
public sealed class MindVisionDeviceInfo
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public string Port { get; init; } = "";
    public string Serial { get; init; } = "";
    public override string ToString() => $"{Index}: {Name}";
}

/// <summary>
/// 迈德威视（MindVision）相机：面阵和线扫共用同一套 SDK，差别只在触发方式和
/// 「行频 / 拼帧行数」这两个参数上，所以一个类里用 <see cref="LineScan"/> 区分。
/// 底层用厂商的 C# 封装（MVSDK.cs，运行期 LoadLibrary），没装 SDK 也不会崩。
/// </summary>
public sealed class MindVisionCamera : ICamera
{
    const string SdkDir64 = @"C:\Program Files (x86)\MindVision\SDK\X64";
    const string SdkDir32 = @"C:\Program Files (x86)\MindVision\SDK";
    static bool _sdkProbed;
    static string _sdkError = "";

    /// <summary>探测 SDK（顺带把安装目录加进 DLL 搜索路径）。没有硬件也能安全调用。</summary>
    public static bool SdkAvailable
    {
        get
        {
            if (_sdkProbed) return _sdkError.Length == 0;
            _sdkProbed = true;
            try
            {
                var dir = Environment.Is64BitProcess ? SdkDir64 : SdkDir32;
                if (System.IO.Directory.Exists(dir)) NativeMethods.SetDllDirectory(dir);
                _ = MvApi.CameraSdkInit;                       // 触发静态构造：LoadLibrary + GetProcAddress
                _sdkError = MvApi.CameraSdkInit == null ? "找不到 MVCAMSDK_X64.dll（没装迈德威视 SDK？）" : "";
            }
            catch (Exception ex)
            {
                _sdkError = "迈德威视 SDK 加载失败：" + ex.Message;
            }
            return _sdkError.Length == 0;
        }
    }

    public static string SdkError => SdkAvailable ? "" : _sdkError;

    /// <summary>枚举相机。失败返回空表，不抛异常。</summary>
    public static List<MindVisionDeviceInfo> Enumerate()
    {
        var list = new List<MindVisionDeviceInfo>();
        if (!SdkAvailable) return list;
        try
        {
            if (MvApi.CameraSdkInit(1) != CameraSdkStatus.CAMERA_STATUS_SUCCESS) return list;
            if (MvApi.CameraEnumerateDevice(out var devs) != CameraSdkStatus.CAMERA_STATUS_SUCCESS) return list;
            for (int i = 0; i < devs.Length; i++)
            {
                var name = Str(devs[i].acFriendlyName);
                if (string.IsNullOrWhiteSpace(name)) name = Str(devs[i].acProductName);
                list.Add(new MindVisionDeviceInfo
                {
                    Index = i,
                    Name = string.IsNullOrWhiteSpace(name) ? $"相机{i}" : name,
                    Port = Str(devs[i].acPortType),
                    Serial = Str(devs[i].acSn),
                });
            }
        }
        catch (Exception ex)
        {
            _sdkError = "枚举相机失败：" + ex.Message;
        }
        return list;
    }

    // ---------------- 实例 ----------------
    private int _handle;                     // CameraHandle 就是 int
    private bool _opened;
    private IntPtr _outBuf;
    private int _outSize;
    private byte[] _copy = Array.Empty<byte>();
    private Thread? _thread;
    private volatile bool _running;

    public MindVisionCamera(int deviceIndex = 0) => DeviceIndex = deviceIndex;

    public int DeviceIndex { get; }
    public bool LineScan { get; set; }
    public int LineRateHz { get; set; } = 10000;
    public int SpliceLines { get; set; } = 1024;
    public bool EncoderTrigger { get; set; }
    public bool SoftTrigger { get; set; }
    public double ExposureUs { get; set; }
    public int Gain { get; set; }

    public string DeviceName { get; private set; } = "";
    public bool IsOpened => _opened && _handle != 0;
    public bool IsGrabbing => _running;

    public event Action<BitmapSource>? FrameReady;
    public event Action<string>? Log;

    // ---------------- 打开 / 关闭 ----------------
    public bool Open()
    {
        if (IsOpened) return true;
        if (!SdkAvailable) { Log?.Invoke("[迈德威视] " + SdkError); return false; }
        try
        {
            if (MvApi.CameraSdkInit(1) != CameraSdkStatus.CAMERA_STATUS_SUCCESS) { Log?.Invoke("[迈德威视] SDK 初始化失败"); return false; }
            if (MvApi.CameraEnumerateDevice(out var devs) != CameraSdkStatus.CAMERA_STATUS_SUCCESS || devs.Length == 0)
            { Log?.Invoke("[迈德威视] 没有找到相机"); return false; }

            int idx = Math.Clamp(DeviceIndex, 0, devs.Length - 1);
            if (idx != DeviceIndex) Log?.Invoke($"[迈德威视] 设备号 {DeviceIndex} 不存在，改用第 {idx} 台（共 {devs.Length} 台）");

            var info = devs[idx];
            var st = MvApi.CameraInit(ref info, -1, -1, ref _handle);
            if (st != CameraSdkStatus.CAMERA_STATUS_SUCCESS || _handle == 0)
            {
                Log?.Invoke($"[迈德威视] 打开相机失败：{MvApi.CameraGetErrorString(st)}");
                _handle = 0;
                return false;
            }
            DeviceName = Str(info.acFriendlyName);
            MvApi.CameraSetIspOutFormat(_handle, (uint)emImageFormat.CAMERA_MEDIA_TYPE_BGR8);

            if (LineScan)
            {
                MvApi.CameraSetTriggerMode(_handle, EncoderTrigger ? 3 : 0);   // 0=连续 3=行触发
                if (LineRateHz > 0) MvApi.CameraSetFrameRate(_handle, LineRateHz);
                if (SpliceLines > 0) MvApi.CameraSetSpliceLines(_handle, SpliceLines);
                if (EncoderTrigger) MvApi.CameraSetRotaryEncTrigger(_handle, 1);
                Log?.Invoke($"[迈德威视] 线扫已打开：{DeviceName} 行频 {LineRateHz}Hz，每帧 {SpliceLines} 行{(EncoderTrigger ? "，编码器触发" : "")}");
            }
            else
            {
                MvApi.CameraSetTriggerMode(_handle, SoftTrigger ? 1 : 0);
                Log?.Invoke($"[迈德威视] 面阵已打开：{DeviceName}{(SoftTrigger ? "（软触发）" : "（连续采集）")}");
            }

            if (ExposureUs > 0) MvApi.CameraSetExposureTime(_handle, ExposureUs);
            if (Gain > 0) MvApi.CameraSetAnalogGain(_handle, Gain);

            _opened = true;
            MvApi.CameraPlay(_handle);
            return true;
        }
        catch (Exception ex)
        {
            Log?.Invoke("[迈德威视] 打开异常：" + ex.Message);
            return false;
        }
    }

    public void Start(string? serial = null)
    {
        if (_running) return;
        if (!Open()) return;
        _running = true;
        _thread = new Thread(Loop) { IsBackground = true, Name = "MindVisionCam" };
        _thread.Start();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        Log?.Invoke("[迈德威视] 已停止采集");
    }

    private void Loop()
    {
        while (_running)
        {
            var bmp = GrabOne();
            if (bmp != null) FrameReady?.Invoke(bmp);
            else Thread.Sleep(20);
        }
    }

    public BitmapSource? GrabOne()
    {
        using var mat = GrabMat();
        return mat == null ? null : ToBitmap(mat);
    }

    /// <summary>抓一帧 BGR Mat（流程里的采集步骤直接用它）。</summary>
    public Mat? GrabMat(int timeoutMs = 1000)
    {
        if (!IsOpened) return null;
        var st = MvApi.CameraGetImageBuffer(_handle, out var head, out var raw, (uint)timeoutMs);
        if (st != CameraSdkStatus.CAMERA_STATUS_SUCCESS) return null;
        try
        {
            int w = head.iWidthZoomSw > 0 ? head.iWidthZoomSw : head.iWidth;
            int h = head.iHeightZoomSw > 0 ? head.iHeightZoomSw : head.iHeight;
            if (w <= 0 || h <= 0) return null;

            int need = w * h * 3;
            if (_outBuf == IntPtr.Zero || _outSize < need)
            {
                if (_outBuf != IntPtr.Zero) Marshal.FreeHGlobal(_outBuf);
                _outBuf = Marshal.AllocHGlobal(need);
                _outSize = need;
            }

            var headForProcess = head;
            if (MvApi.CameraImageProcess(_handle, raw, _outBuf, ref headForProcess) != CameraSdkStatus.CAMERA_STATUS_SUCCESS)
                return null;

            // 黑白相机即使设了 BGR8 也可能出 MONO8，按实际格式取字节数
            bool mono = headForProcess.uiMediaType == (uint)emImageFormat.CAMERA_MEDIA_TYPE_MONO8;
            int used = mono ? w * h : need;
            if (_copy.Length < used) _copy = new byte[used];
            Marshal.Copy(_outBuf, _copy, 0, used);

            var mat = new Mat(h, w, mono ? MatType.CV_8UC1 : MatType.CV_8UC3);
            Marshal.Copy(_copy, 0, mat.Data, used);
            return mat;
        }
        finally
        {
            MvApi.CameraReleaseImageBuffer(_handle, raw);
        }
    }

    public void Close()
    {
        _running = false;
        try { _thread?.Join(300); } catch { }
        _thread = null;
        try
        {
            if (_handle != 0)
            {
                MvApi.CameraPause(_handle);
                MvApi.CameraUnInit(_handle);
            }
        }
        catch { }
        _handle = 0;
        _opened = false;
        if (_outBuf != IntPtr.Zero) { Marshal.FreeHGlobal(_outBuf); _outBuf = IntPtr.Zero; _outSize = 0; }
    }

    public void Dispose() => Close();

    // ---------------- 工具 ----------------
    private static BitmapSource ToBitmap(Mat bgr)
    {
        int stride = bgr.Width * 3;
        var bytes = new byte[stride * bgr.Height];
        Marshal.Copy(bgr.Data, bytes, 0, bytes.Length);
        var wb = new WriteableBitmap(bgr.Width, bgr.Height, 96, 96, PixelFormats.Bgr24, null);
        wb.WritePixels(new System.Windows.Int32Rect(0, 0, bgr.Width, bgr.Height), bytes, stride, 0);
        wb.Freeze();
        return wb;
    }

    /// <summary>SDK 里的定长 byte[] 字段（GBK 编码）转字符串。</summary>
    private static string Str(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return "";
        int n = Array.IndexOf(bytes, (byte)0);
        if (n < 0) n = bytes.Length;
        if (n == 0) return "";
        try { return Gbk.GetString(bytes, 0, n).Trim(); }
        catch { return Encoding.UTF8.GetString(bytes, 0, n).Trim(); }
    }

    private static readonly Encoding Gbk = CreateGbk();

    private static Encoding CreateGbk()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(936);
        }
        catch { return Encoding.UTF8; }
    }
}

internal static class NativeMethods
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool SetDllDirectory(string lpPathName);
}
