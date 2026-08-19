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

public class VisionFlowStep : ViewModelBase
{
    public int Index { get => _index; set => SetField(ref _index, value); }
    private int _index;

    public string Function { get => _function; set => SetField(ref _function, value); }
    private string _function = "";

    public string Name { get => _name; set => SetField(ref _name, value); }
    private string _name = "";

    public string ParamSummary { get => _paramSummary; set => SetField(ref _paramSummary, value); }
    private string _paramSummary = "";

    public int Timeout { get => _timeout; set => SetField(ref _timeout, value); }
    private int _timeout = 3000;

    public double CostMs { get => _costMs; set => SetField(ref _costMs, value); }
    private double _costMs;

    public string ActualValue { get => _actualValue; set => SetField(ref _actualValue, value); }
    private string _actualValue = "";

    public string Icon { get => _icon; set => SetField(ref _icon, value); }
    private string _icon = "➕";

    public string StepType { get => _stepType; set => SetField(ref _stepType, value); }
    private string _stepType = "";

    public string ImageSource { get => _imageSource; set => SetField(ref _imageSource, value); }
    private string _imageSource = "";

    public string LuaScript { get => _luaScript; set => SetField(ref _luaScript, value); }
    private string _luaScript = "";

    public string Parameters { get => _parameters; set => SetField(ref _parameters, value); }
    private string _parameters = "";
}

public class VisionFlow
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "🔀";
    public ObservableCollection<VisionFlowStep> Steps { get; set; } = new();
}

#endregion

#region 项目页

public class ProjectItem
{
    public string ProjectName { get; set; } = "";
    public string Author { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public string CreateTime { get; set; } = "";
    public bool AutoSave { get; set; } = true;
    public int SaveInterval { get; set; } = 300;
    public string Language { get; set; } = "简体中文";
    public string Status { get; set; } = "就绪";
}

public class ProjectViewModel : ViewModelBase
{
    public ObservableCollection<ProjectItem> Projects { get; } = new()
    {
        new ProjectItem { ProjectName = "DemoVision-01", Author = "admin", ProjectPath = @"D:\Projects\DemoVision-01.ncv", CreateTime = DateTime.Now.ToString("yyyy-MM-dd"), AutoSave = true, SaveInterval = 300, Language = "简体中文" },
        new ProjectItem { ProjectName = "MotorBracket-A", Author = "admin", ProjectPath = @"D:\Projects\MotorBracket-A.ncv", CreateTime = "2026-08-10", AutoSave = true, SaveInterval = 120, Language = "简体中文" },
        new ProjectItem { ProjectName = "PCB-Inspection", Author = "admin", ProjectPath = @"D:\Projects\PCB-Inspection.ncv", CreateTime = "2026-07-22", AutoSave = false, SaveInterval = 600, Language = "English" },
    };

    private ProjectItem? _selectedProject;
    public ProjectItem? SelectedProject { get => _selectedProject; set => SetField(ref _selectedProject, value); }

    public ICommand AddCmd { get; }
    public ICommand DeleteCmd { get; }
    public ICommand NewCmd { get; }
    public ICommand OpenCmd { get; }
    public ICommand SaveCmd { get; }

    public ProjectViewModel()
    {
        SelectedProject = Projects[0];

        AddCmd = new RelayCommand(_ =>
        {
            var next = Projects.Count + 1;
            Projects.Add(new ProjectItem
            {
                ProjectName = $"新建项目-{next}",
                Author = "admin",
                ProjectPath = $"D:\\Projects\\NewProject-{next}.ncv",
                CreateTime = DateTime.Now.ToString("yyyy-MM-dd"),
                AutoSave = true,
                SaveInterval = 300,
                Language = "简体中文"
            });
        });
        DeleteCmd = new RelayCommand(_ =>
        {
            if (_selectedProject != null)
            {
                Projects.Remove(_selectedProject);
                SelectedProject = Projects.Count > 0 ? Projects[0] : null;
            }
        }, _ => _selectedProject != null);
        NewCmd = new RelayCommand(_ =>
        {
            if (_selectedProject != null)
                _selectedProject.Status = $"已新建项目 · {DateTime.Now:HH:mm:ss}";
            OnPropertyChanged(nameof(SelectedProject));
        });
        OpenCmd = new RelayCommand(_ =>
        {
            if (_selectedProject != null)
                _selectedProject.Status = $"已打开 {_selectedProject.ProjectPath} · {DateTime.Now:HH:mm:ss}";
            OnPropertyChanged(nameof(SelectedProject));
        });
        SaveCmd = new RelayCommand(_ =>
        {
            if (_selectedProject != null)
                _selectedProject.Status = $"已保存 · {DateTime.Now:HH:mm:ss}";
            OnPropertyChanged(nameof(SelectedProject));
        });
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

    private CameraItem? _selectedCamera;
    public CameraItem? SelectedCamera { get => _selectedCamera; set => SetField(ref _selectedCamera, value); }

    public ICommand AddCmd { get; }
    public ICommand DeleteCmd { get; }

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
        SelectedCamera = Cameras[0];

        AddCmd = new RelayCommand(_ =>
        {
            var next = Cameras.Count;
            Cameras.Add(new CameraItem { Name = $"Camera_{next} (新相机)", Status = "离线", Resolution = "1920 × 1080" });
        });
        DeleteCmd = new RelayCommand(_ =>
        {
            if (_selectedCamera != null)
            {
                Cameras.Remove(_selectedCamera);
                SelectedCamera = Cameras.Count > 0 ? Cameras[0] : null;
            }
        }, _ => _selectedCamera != null);

        ConnectCmd = new RelayCommand(_ => IsConnected = true);
        StartCmd = new RelayCommand(_ => IsConnected = true);
        StopCmd = new RelayCommand(_ => IsConnected = false, _ => IsConnected);
    }
}

#endregion

#region 通讯页

public class CommConfigItem
{
    public string Name { get; set; } = "";
    public string CommType { get; set; } = "串口";
    public string Port { get; set; } = "COM3";
    public string Baud { get; set; } = "115200";
    public string DataBits { get; set; } = "8";
    public string Parity { get; set; } = "无";
    public string StopBits { get; set; } = "1";
    public string Flow { get; set; } = "无";
    public string NetIp { get; set; } = "192.168.1.100";
    public string NetPort { get; set; } = "5000";
    public bool AutoReconnect { get; set; } = true;
}

public class CommunicationViewModel : ViewModelBase
{
    public string[] CommTypes { get; } = { "串口", "网口" };

    public string[] PortOptions { get; } = { "COM1", "COM2", "COM3", "COM4" };
    public string[] BaudOptions { get; } = { "9600", "19200", "38400", "57600", "115200" };
    public string[] DataBitsOptions { get; } = { "5", "6", "7", "8" };
    public string[] ParityOptions { get; } = { "无", "奇校验", "偶校验" };
    public string[] StopBitsOptions { get; } = { "1", "1.5", "2" };
    public string[] FlowOptions { get; } = { "无", "RTS/CTS", "XON/XOFF" };

    public ObservableCollection<CommConfigItem> Configs { get; } = new()
    {
        new CommConfigItem { Name = "PLC-串口", CommType = "串口", Port = "COM3", Baud = "115200" },
        new CommConfigItem { Name = "上位机-网口", CommType = "网口", NetIp = "192.168.1.100", NetPort = "5000" },
    };

    private CommConfigItem? _selectedConfig;
    public CommConfigItem? SelectedConfig { get => _selectedConfig; set => SetField(ref _selectedConfig, value); }

    public ICommand AddCmd { get; }
    public ICommand DeleteCmd { get; }

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
        SelectedConfig = Configs[0];

        AddCmd = new RelayCommand(_ =>
        {
            var next = Configs.Count + 1;
            Configs.Add(new CommConfigItem { Name = $"新配置-{next}", CommType = "串口", Port = "COM1", Baud = "9600" });
        });
        DeleteCmd = new RelayCommand(_ =>
        {
            if (_selectedConfig != null)
            {
                Configs.Remove(_selectedConfig);
                SelectedConfig = Configs.Count > 0 ? Configs[0] : null;
            }
        }, _ => _selectedConfig != null);

        ConnectCmd = new RelayCommand(_ =>
        {
            IsConnected = true;
            if (_selectedConfig != null)
                PushLog($"[连接] {(_selectedConfig.CommType == "串口" ? $"{_selectedConfig.Port} @ {_selectedConfig.Baud}" : $"{_selectedConfig.NetIp}:{_selectedConfig.NetPort}")}");
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
        RemoveCmd = new RelayCommand(_ =>
        {
            if (_selected != null)
            {
                Variables.Remove(_selected);
                Selected = Variables.Count > 0 ? Variables[0] : null;
            }
        }, _ => _selected != null);
    }
}

#endregion

#region 流程页

public class FlowViewModel : ViewModelBase
{
    public ObservableCollection<VisionFlow> Flows { get; } = new()
    {
        new VisionFlow
        {
            Name = "主流程", Icon = "🔀",
            Steps = new ObservableCollection<VisionFlowStep>
            {
                new VisionFlowStep { Index = 1, Function = "图像采集", Name = "采集左视野", ParamSummary = "Camera_0 / Mono8", Timeout = 5000, CostMs = 12.3, ActualValue = "OK", Icon = "📷", StepType = "ImageCapture", ImageSource = "Camera_0" },
                new VisionFlowStep { Index = 2, Function = "模板匹配", Name = "定位基准", ParamSummary = "tpl_A / score≥0.85", Timeout = 3000, CostMs = 8.7, ActualValue = "(120,88)", Icon = "🎯", StepType = "TemplateMatch", ImageSource = "tpl_A.png" },
                new VisionFlowStep { Index = 3, Function = "几何测量", Name = "测量孔径", ParamSummary = "圆径 / 12.00±0.05", Timeout = 2000, CostMs = 5.4, ActualValue = "12.03", Icon = "📐", StepType = "Measure" },
                new VisionFlowStep { Index = 4, Function = "逻辑判断", Name = "判定合格", ParamSummary = "孔径 OK", Timeout = 1000, CostMs = 0.2, ActualValue = "Pass", Icon = "✅", StepType = "Logic" },
                new VisionFlowStep { Index = 5, Function = "结果输出", Name = "输出结果", ParamSummary = "PLC_D200=1", Timeout = 1000, CostMs = 0.5, ActualValue = "Done", Icon = "📤", StepType = "Output" },
            }
        },
        new VisionFlow
        {
            Name = "定位流程", Icon = "🧭",
            Steps = new ObservableCollection<VisionFlowStep>
            {
                new VisionFlowStep { Index = 1, Function = "图像采集", Name = "采集顶视野", ParamSummary = "Camera_2 / RGB8", Timeout = 5000, CostMs = 15.1, ActualValue = "OK", Icon = "📷", StepType = "ImageCapture", ImageSource = "Camera_2" },
                new VisionFlowStep { Index = 2, Function = "模板匹配", Name = "找中心点", ParamSummary = "tpl_center / score≥0.80", Timeout = 3000, CostMs = 9.2, ActualValue = "(512,384)", Icon = "🎯", StepType = "TemplateMatch", ImageSource = "tpl_center.png" },
            }
        },
        new VisionFlow
        {
            Name = "检测流程", Icon = "🔍",
            Steps = new ObservableCollection<VisionFlowStep>
            {
                new VisionFlowStep { Index = 1, Function = "图像预处理", Name = "灰度化", ParamSummary = "RGB→Gray", Timeout = 2000, CostMs = 3.1, ActualValue = "Done", Icon = "🎨", StepType = "Preprocess" },
                new VisionFlowStep { Index = 2, Function = "几何测量", Name = "测量边距", ParamSummary = "距离 / 45.0±0.1", Timeout = 2000, CostMs = 6.8, ActualValue = "45.02", Icon = "📐", StepType = "Measure" },
            }
        },
    };

    public string[] StepFunctions { get; } = { "图像采集", "图像预处理", "模板匹配", "几何测量", "逻辑判断", "Lua脚本", "结果输出" };
    public string[] PropTabs { get; } = { "图像", "Lua", "参数设置" };

    private VisionFlow? _selectedFlow;
    public VisionFlow? SelectedFlow { get => _selectedFlow; set => SetField(ref _selectedFlow, value); }

    private VisionFlowStep? _selectedStep;
    public VisionFlowStep? SelectedStep { get => _selectedStep; set => SetField(ref _selectedStep, value); }

    private string _newStepFunction = "模板匹配";
    public string NewStepFunction { get => _newStepFunction; set => SetField(ref _newStepFunction, value); }

    private string _activePropTab = "图像";
    public string ActivePropTab { get => _activePropTab; set => SetField(ref _activePropTab, value); }

    private string _status = "就绪";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand AddFlowCmd { get; }
    public ICommand DeleteFlowCmd { get; }
    public ICommand AddStepCmd { get; }
    public ICommand DeleteStepCmd { get; }
    public ICommand MoveUpCmd { get; }
    public ICommand MoveDownCmd { get; }
    public ICommand SetPropTabCmd { get; }
    public ICommand RunCmd { get; }
    public ICommand ClearCmd { get; }

    public FlowViewModel()
    {
        SelectedFlow = Flows[0];
        SelectedStep = SelectedFlow.Steps[0];

        AddFlowCmd = new RelayCommand(_ =>
        {
            var next = Flows.Count + 1;
            Flows.Add(new VisionFlow { Name = $"新流程-{next}", Icon = "🔀" });
        });
        DeleteFlowCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow != null)
            {
                Flows.Remove(_selectedFlow);
                SelectedFlow = Flows.Count > 0 ? Flows[0] : null;
                SelectedStep = _selectedFlow?.Steps.FirstOrDefault();
            }
        }, _ => _selectedFlow != null);

        AddStepCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null) return;
            var next = _selectedFlow.Steps.Count + 1;
            var (icon, type) = _newStepFunction switch
            {
                "图像采集" => ("📷", "ImageCapture"),
                "图像预处理" => ("🎨", "Preprocess"),
                "模板匹配" => ("🎯", "TemplateMatch"),
                "几何测量" => ("📐", "Measure"),
                "逻辑判断" => ("✅", "Logic"),
                "Lua脚本" => ("📝", "Lua"),
                "结果输出" => ("📤", "Output"),
                _ => ("➕", "Other")
            };
            var step = new VisionFlowStep
            {
                Index = next,
                Function = _newStepFunction,
                Name = $"{_newStepFunction}{next}",
                ParamSummary = "-",
                Timeout = 3000,
                CostMs = 0,
                ActualValue = "-",
                Icon = icon,
                StepType = type
            };
            _selectedFlow.Steps.Add(step);
            Reindex(_selectedFlow);
            SelectedStep = step;
        }, _ => _selectedFlow != null);

        DeleteStepCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null || _selectedStep == null) return;
            _selectedFlow.Steps.Remove(_selectedStep);
            Reindex(_selectedFlow);
            SelectedStep = _selectedFlow.Steps.Count > 0 ? _selectedFlow.Steps[0] : null;
        }, _ => _selectedFlow != null && _selectedStep != null);

        MoveUpCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null || _selectedStep == null) return;
            var list = _selectedFlow.Steps;
            var idx = list.IndexOf(_selectedStep);
            if (idx > 0) { list.Move(idx, idx - 1); Reindex(_selectedFlow); }
        }, _ => _selectedFlow != null && _selectedStep != null && _selectedFlow.Steps.IndexOf(_selectedStep) > 0);

        MoveDownCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null || _selectedStep == null) return;
            var list = _selectedFlow.Steps;
            var idx = list.IndexOf(_selectedStep);
            if (idx < list.Count - 1) { list.Move(idx, idx + 1); Reindex(_selectedFlow); }
        }, _ => _selectedFlow != null && _selectedStep != null && _selectedFlow.Steps.IndexOf(_selectedStep) < _selectedFlow.Steps.Count - 1);

        SetPropTabCmd = new RelayCommand(p => { if (p is string s) ActivePropTab = s; });

        RunCmd = new RelayCommand(_ => Status = $"运行中 · {_selectedFlow?.Name} · 共 {_selectedFlow?.Steps.Count ?? 0} 步 · {DateTime.Now:HH:mm:ss}");
        ClearCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow != null)
            {
                _selectedFlow.Steps.Clear();
                Reindex(_selectedFlow);
                SelectedStep = null;
                Status = "已清空";
            }
        }, _ => _selectedFlow != null && _selectedFlow.Steps.Count > 0);
    }

    private static void Reindex(VisionFlow flow)
    {
        for (int i = 0; i < flow.Steps.Count; i++) flow.Steps[i].Index = i + 1;
    }
}

#endregion

#region 工程师页

public class ModuleItem
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "正常";
}

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

    public ObservableCollection<ModuleItem> Modules { get; } = new()
    {
        new ModuleItem { Name = "视觉引擎", Status = "运行中" },
        new ModuleItem { Name = "相机模块", Status = "正常" },
        new ModuleItem { Name = "通讯模块", Status = "正常" },
        new ModuleItem { Name = "数据库", Status = "离线" },
    };
    private ModuleItem? _selectedModule;
    public ModuleItem? SelectedModule { get => _selectedModule; set => SetField(ref _selectedModule, value); }

    public ICommand AddCmd { get; }
    public ICommand DeleteCmd { get; }

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
        SelectedModule = Modules[0];

        AddCmd = new RelayCommand(_ =>
        {
            var next = Modules.Count + 1;
            Modules.Add(new ModuleItem { Name = $"扩展模块-{next}", Status = "正常" });
        });
        DeleteCmd = new RelayCommand(_ =>
        {
            if (_selectedModule != null)
            {
                Modules.Remove(_selectedModule);
                SelectedModule = Modules.Count > 0 ? Modules[0] : null;
            }
        }, _ => _selectedModule != null);

        ExportCmd = new RelayCommand(_ => LogEntries.Insert(0, $"[信息] 已导出日志 {DateTime.Now:HH:mm:ss}"));
        ClearCmd = new RelayCommand(_ => LogEntries.Clear());
    }
}

#endregion

#region 操作员页

public class TaskItem
{
    public string Name { get; set; } = "";
    public string Time { get; set; } = "";
    public string Result { get; set; } = "待检";
}

public class OperatorViewModel : ViewModelBase
{
    public ObservableCollection<TaskItem> Tasks { get; } = new()
    {
        new TaskItem { Name = "批次-20260819-001", Time = "08:30", Result = "OK" },
        new TaskItem { Name = "批次-20260819-002", Time = "09:15", Result = "NG" },
        new TaskItem { Name = "批次-20260819-003", Time = "10:02", Result = "OK" },
    };
    private TaskItem? _selectedTask;
    public TaskItem? SelectedTask { get => _selectedTask; set => SetField(ref _selectedTask, value); }

    public ICommand AddCmd { get; }
    public ICommand DeleteCmd { get; }

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
        SelectedTask = Tasks[0];

        AddCmd = new RelayCommand(_ =>
        {
            var next = Tasks.Count + 1;
            Tasks.Add(new TaskItem { Name = $"批次-{DateTime.Now:yyyyMMdd}-{next:D3}", Time = DateTime.Now.ToString("HH:mm"), Result = "待检" });
        });
        DeleteCmd = new RelayCommand(_ =>
        {
            if (_selectedTask != null)
            {
                Tasks.Remove(_selectedTask);
                SelectedTask = Tasks.Count > 0 ? Tasks[0] : null;
            }
        }, _ => _selectedTask != null);

        StartCmd = new RelayCommand(_ => IsRunning = true, _ => !_isRunning);
        StopCmd = new RelayCommand(_ => IsRunning = false, _ => _isRunning);
        SampleCmd = new RelayCommand(_ =>
        {
            if (!_isRunning) return;
            Total++;
            if (DateTime.Now.Millisecond % 10 != 0) Ok++; else Ng++;
            if (_selectedTask != null) _selectedTask.Result = Ng > 0 && DateTime.Now.Millisecond % 10 == 0 ? "NG" : "OK";
        }, _ => _isRunning);
    }

    private void UpdateStatus() => Status = _isRunning ? "检测中…" : "已停止";
}

#endregion
