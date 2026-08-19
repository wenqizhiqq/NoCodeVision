using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NoCodeVision.ViewModels;

#region 基础类

public class RelayCommand : ICommand
{
    private readonly Action<object?> _exec;
    private readonly Func<object?, bool>? _can;

    public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null)
    {
        _exec = exec;
        _can = can;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _can == null || _can(parameter);
    public void Execute(object? parameter) => _exec(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

#endregion

#region 列表项模型

public class CameraItem
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "离线";
    public string Resolution { get; set; } = "";
}

public class VarItem
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
}

public class FlowStepItem
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "";
}

#endregion

#region 项目页

public class ProjectViewModel : ViewModelBase
{
    public string ProjectName { get; set; } = "DemoVision-01";
    public string Author { get; set; } = "admin";
    public string ProjectPath { get; set; } = @"D:\Projects\DemoVision-01.ncv";
    public string CreateTime { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

    private bool _autoSave = true;
    public bool AutoSave { get => _autoSave; set => SetField(ref _autoSave, value); }

    private int _saveInterval = 300;
    public int SaveInterval { get => _saveInterval; set => SetField(ref _saveInterval, value); }

    public string[] Languages { get; } = { "简体中文", "English" };
    private string _language = "简体中文";
    public string Language { get => _language; set => SetField(ref _language, value); }

    private string _status = "就绪";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand NewCmd { get; }
    public ICommand OpenCmd { get; }
    public ICommand SaveCmd { get; }

    public ProjectViewModel()
    {
        NewCmd = new RelayCommand(_ => Status = $"已新建项目 · {DateTime.Now:HH:mm:ss}");
        OpenCmd = new RelayCommand(_ => Status = $"已打开 {ProjectPath} · {DateTime.Now:HH:mm:ss}");
        SaveCmd = new RelayCommand(_ => Status = $"已保存 · {DateTime.Now:HH:mm:ss}");
    }
}

#endregion

#region 相机页

public class CameraViewModel : ViewModelBase
{
    public ObservableCollection<CameraItem> Cameras { get; } = new()
    {
        new CameraItem { Name = "Camera_0 (左视野)", Status = "已连接", Resolution = "2448 × 2048" },
        new CameraItem { Name = "Camera_1 (右视野)", Status = "离线", Resolution = "2448 × 2048" },
        new CameraItem { Name = "Camera_2 (顶视野)", Status = "离线", Resolution = "1920 × 1200" },
    };

    private double _exposure = 8.0;
    public double Exposure { get => _exposure; set => SetField(ref _exposure, value); }

    private double _gain = 1.0;
    public double Gain { get => _gain; set => SetField(ref _gain, value); }

    public string[] TriggerModes { get; } = { "连续采集", "软触发", "硬触发" };
    private string _triggerMode = "连续采集";
    public string TriggerMode { get => _triggerMode; set => SetField(ref _triggerMode, value); }

    public string[] PixelFormats { get; } = { "Mono8", "RGB8", "Mono12" };
    private string _pixelFormat = "RGB8";
    public string PixelFormat { get => _pixelFormat; set => SetField(ref _pixelFormat, value); }

    private int _width = 2448;
    public int Width { get => _width; set => SetField(ref _width, value); }

    private int _height = 2048;
    public int Height { get => _height; set => SetField(ref _height, value); }

    private bool _isConnected;
    public bool IsConnected { get => _isConnected; set { if (SetField(ref _isConnected, value)) OnPropertyChanged(nameof(StatusText)); } }

    public string StatusText => IsConnected ? "实时采集中" : "未连接";

    public ICommand ConnectCmd { get; }
    public ICommand StartCmd { get; }
    public ICommand StopCmd { get; }

    public CameraViewModel()
    {
        ConnectCmd = new RelayCommand(_ => IsConnected = true);
        StartCmd = new RelayCommand(_ => IsConnected = true);
        StopCmd = new RelayCommand(_ => IsConnected = false, _ => IsConnected);
    }
}

#endregion

#region 通讯页

public class CommunicationViewModel : ViewModelBase
{
    public string[] CommTypes { get; } = { "串口", "网口" };
    private string _commType = "串口";
    public string CommType { get => _commType; set => SetField(ref _commType, value); }

    public string[] PortOptions { get; } = { "COM1", "COM2", "COM3", "COM4" };
    private string _port = "COM3";
    public string Port { get => _port; set => SetField(ref _port, value); }

    public string[] BaudOptions { get; } = { "9600", "19200", "38400", "57600", "115200" };
    private string _baud = "115200";
    public string Baud { get => _baud; set => SetField(ref _baud, value); }

    public string[] DataBitsOptions { get; } = { "5", "6", "7", "8" };
    private string _dataBits = "8";
    public string DataBits { get => _dataBits; set => SetField(ref _dataBits, value); }

    public string[] ParityOptions { get; } = { "无", "奇校验", "偶校验" };
    private string _parity = "无";
    public string Parity { get => _parity; set => SetField(ref _parity, value); }

    public string[] StopBitsOptions { get; } = { "1", "1.5", "2" };
    private string _stopBits = "1";
    public string StopBits { get => _stopBits; set => SetField(ref _stopBits, value); }

    public string[] FlowOptions { get; } = { "无", "RTS/CTS", "XON/XOFF" };
    private string _flow = "无";
    public string Flow { get => _flow; set => SetField(ref _flow, value); }

    private string _netIp = "192.168.1.100";
    public string NetIp { get => _netIp; set => SetField(ref _netIp, value); }

    private string _netPort = "5000";
    public string NetPort { get => _netPort; set => SetField(ref _netPort, value); }

    private bool _autoReconnect = true;
    public bool AutoReconnect { get => _autoReconnect; set => SetField(ref _autoReconnect, value); }

    private bool _isConnected;
    public bool IsConnected { get => _isConnected; set { if (SetField(ref _isConnected, value)) OnPropertyChanged(nameof(ConnText)); } }
    public string ConnText => IsConnected ? "已连接" : "未连接";

    public ObservableCollection<string> DebugLog { get; } = new();
    private string _sendText = "";
    public string SendText { get => _sendText; set => SetField(ref _sendText, value); }

    public ICommand ConnectCmd { get; }
    public ICommand DisconnectCmd { get; }
    public ICommand SendCmd { get; }
    public ICommand ClearLogCmd { get; }
    public ICommand AutoScanCmd { get; }

    public CommunicationViewModel()
    {
        ConnectCmd = new RelayCommand(_ =>
        {
            IsConnected = true;
            PushLog($"[连接] {(_commType == "串口" ? $"{_port} @ {_baud}" : $"{_netIp}:{_netPort}")}");
        });
        DisconnectCmd = new RelayCommand(_ =>
        {
            IsConnected = false;
            PushLog("[断开] 连接已关闭");
        }, _ => IsConnected);
        SendCmd = new RelayCommand(_ =>
        {
            if (string.IsNullOrWhiteSpace(_sendText)) return;
            PushLog($"[发送] {_sendText}");
            _sendText = "";
            OnPropertyChanged(nameof(SendText));
        });
        ClearLogCmd = new RelayCommand(_ => DebugLog.Clear());
        AutoScanCmd = new RelayCommand(_ => PushLog("[扫描] 发现设备 COM3 / 192.168.1.100:5000"));
    }

    private void PushLog(string line)
    {
        DebugLog.Insert(0, $"{DateTime.Now:HH:mm:ss} {line}");
        while (DebugLog.Count > 300) DebugLog.RemoveAt(DebugLog.Count - 1);
    }
}

#endregion

#region 变量页

public class VariablesViewModel : ViewModelBase
{
    public ObservableCollection<VarItem> Variables { get; } = new()
    {
        new VarItem { Name = "nProductCount", Type = "整数", Value = "0" },
        new VarItem { Name = "dThreshold", Type = "浮点数", Value = "0.85" },
        new VarItem { Name = "sModelName", Type = "字符串", Value = "Bracket_A" },
        new VarItem { Name = "bPass", Type = "布尔", Value = "True" },
    };

    public string[] Types { get; } = { "整数", "浮点数", "字符串", "布尔" };
    private string _newName = "";
    public string NewName { get => _newName; set => SetField(ref _newName, value); }
    private string _newType = "整数";
    public string NewType { get => _newType; set => SetField(ref _newType, value); }
    private string _newValue = "0";
    public string NewValue { get => _newValue; set => SetField(ref _newValue, value); }

    private VarItem? _selected;
    public VarItem? Selected { get => _selected; set => SetField(ref _selected, value); }

    public ICommand AddCmd { get; }
    public ICommand RemoveCmd { get; }

    public VariablesViewModel()
    {
        AddCmd = new RelayCommand(_ =>
        {
            if (string.IsNullOrWhiteSpace(_newName)) return;
            Variables.Add(new VarItem { Name = _newName, Type = _newType, Value = _newValue });
            _newName = ""; OnPropertyChanged(nameof(NewName));
            _newValue = "0"; OnPropertyChanged(nameof(NewValue));
        });
        RemoveCmd = new RelayCommand(p =>
        {
            if (p is VarItem v) Variables.Remove(v);
        }, p => p is VarItem);
    }
}

#endregion

#region 流程页

public class FlowViewModel : ViewModelBase
{
    public ObservableCollection<FlowStepItem> Steps { get; } = new()
    {
        new FlowStepItem { Index = 1, Name = "采集左视野", Type = "图像采集", Icon = "📷" },
        new FlowStepItem { Index = 2, Name = "灰度化", Type = "图像预处理", Icon = "🎨" },
        new FlowStepItem { Index = 3, Name = "定位基准", Type = "模板匹配", Icon = "🎯" },
        new FlowStepItem { Index = 4, Name = "测量孔径", Type = "几何测量", Icon = "📐" },
        new FlowStepItem { Index = 5, Name = "判定合格", Type = "逻辑判断", Icon = "✅" },
        new FlowStepItem { Index = 6, Name = "输出结果", Type = "结果输出", Icon = "📤" },
    };

    public string[] Toolbox { get; } = { "图像采集", "图像预处理", "模板匹配", "几何测量", "逻辑判断", "结果输出" };

    private string _newStepType = "图像预处理";
    public string NewStepType { get => _newStepType; set => SetField(ref _newStepType, value); }

    private FlowStepItem? _selected;
    public FlowStepItem? Selected { get => _selected; set => SetField(ref _selected, value); }

    private string _status = "流程就绪";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand AddStepCmd { get; }
    public ICommand RunCmd { get; }
    public ICommand ClearCmd { get; }

    public FlowViewModel()
    {
        AddStepCmd = new RelayCommand(_ =>
        {
            var next = Steps.Count + 1;
            var icon = _newStepType switch
            {
                "图像采集" => "📷", "图像预处理" => "🎨", "模板匹配" => "🎯",
                "几何测量" => "📐", "逻辑判断" => "✅", "结果输出" => "📤", _ => "➕"
            };
            Steps.Add(new FlowStepItem { Index = next, Name = $"{_newStepType} {next}", Type = _newStepType, Icon = icon });
        });
        RunCmd = new RelayCommand(_ => Status = $"流程运行中 · 共 {Steps.Count} 步 · {DateTime.Now:HH:mm:ss}");
        ClearCmd = new RelayCommand(_ => { Steps.Clear(); Status = "流程已清空"; });
    }
}

#endregion

#region 工程师页

public class EngineerViewModel : ViewModelBase
{
    public string[] LogLevels { get; } = { "详细", "信息", "警告", "错误" };
    private string _logLevel = "信息";
    public string LogLevel { get => _logLevel; set => SetField(ref _logLevel, value); }

    public string[] RunningModes { get; } = { "开发", "调试", "生产" };
    private string _runningMode = "调试";
    public string RunningMode { get => _runningMode; set => SetField(ref _runningMode, value); }

    public string Version { get; } = "NoCodeVision 1.0.0 (build 2026.08.19)";
    public string Runtime { get; } = ".NET 10 / WPF";
    public string EngineInfo { get; } = "NCC 匹配内核 · SIMD+FFT 加速";
    public string License { get; } = "商用授权 · 有效期 2027-12-31";

    public ObservableCollection<string> LogEntries { get; } = new()
    {
        "[信息] 视觉引擎初始化完成",
        "[信息] 加载方案 DemoVision-01",
        "[警告] Camera_1 未连接，已跳过",
        "[详细] 模板匹配耗时 12.4 ms",
    };

    public ICommand ExportCmd { get; }
    public ICommand ClearCmd { get; }

    public EngineerViewModel()
    {
        ExportCmd = new RelayCommand(_ => LogEntries.Insert(0, $"[信息] 已导出日志 {DateTime.Now:HH:mm:ss}"));
        ClearCmd = new RelayCommand(_ => LogEntries.Clear());
    }
}

#endregion

#region 操作员页

public class OperatorViewModel : ViewModelBase
{
    private bool _isRunning;
    public bool IsRunning { get => _isRunning; set { if (SetField(ref _isRunning, value)) { OnPropertyChanged(nameof(RunButtonText)); UpdateStatus(); } } }

    private int _total;
    public int Total { get => _total; set { if (SetField(ref _total, value)) OnPropertyChanged(nameof(YieldText)); } }
    private int _ok;
    public int Ok { get => _ok; set { if (SetField(ref _ok, value)) OnPropertyChanged(nameof(YieldText)); } }
    private int _ng;
    public int Ng { get => _ng; set => SetField(ref _ng, value); }

    public string YieldText => Total == 0 ? "—" : $"{(_ok * 100.0 / _total):F1}%";
    public string RunButtonText => _isRunning ? "停止检测" : "开始检测";

    private string _status = "待机";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand StartCmd { get; }
    public ICommand StopCmd { get; }
    public ICommand SampleCmd { get; }

    public OperatorViewModel()
    {
        StartCmd = new RelayCommand(_ => IsRunning = true, _ => !_isRunning);
        StopCmd = new RelayCommand(_ => IsRunning = false, _ => _isRunning);
        SampleCmd = new RelayCommand(_ => { if (!_isRunning) return; Total++; if (DateTime.Now.Millisecond % 10 != 0) Ok++; else Ng++; }, _ => _isRunning);
    }

    private void UpdateStatus() => Status = _isRunning ? "检测中…" : "已停止";
}

#endregion
