using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NoCodeVision.Hardware;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using GrayMatch;
using OpenCvSharp;
using NoCodeVision;

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

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? parameter) => _can == null || _can(parameter);
    public void Execute(object? parameter) => _exec(parameter);
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
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

    public string TemplateFile { get => _templateFile; set => SetField(ref _templateFile, value); }
    private string _templateFile = "";

    public string CaptureMode { get => _captureMode; set => SetField(ref _captureMode, value); }
    private string _captureMode = "打开文件";

    public string FolderPath { get => _folderPath; set => SetField(ref _folderPath, value); }
    private string _folderPath = "";

    public double RoiX { get => _roiX; set => SetField(ref _roiX, value); }
    private double _roiX = 10;

    public double RoiY { get => _roiY; set => SetField(ref _roiY, value); }
    private double _roiY = 10;

    public double RoiW { get => _roiW; set => SetField(ref _roiW, value); }
    private double _roiW = 120;

    public double RoiH { get => _roiH; set => SetField(ref _roiH, value); }
    private double _roiH = 80;

    public double ScoreThreshold { get => _scoreThreshold; set => SetField(ref _scoreThreshold, value); }
    private double _scoreThreshold = 0.85;

    public string MatchMode { get => _matchMode; set => SetField(ref _matchMode, value); }
    private string _matchMode = "灰度匹配";

    public int ContourThreshold { get => _contourThreshold; set => SetField(ref _contourThreshold, value); }
    private int _contourThreshold = 30;

    public double ContourBlur { get => _contourBlur; set => SetField(ref _contourBlur, value); }
    private double _contourBlur = 1.0;
    public int PyramidLevel { get => _pyramidLevel; set => SetField(ref _pyramidLevel, value); }
    private int _pyramidLevel = 3;
    public double AngleStart { get => _angleStart; set => SetField(ref _angleStart, value); }
    private double _angleStart = -180.0;
    public double AngleStop { get => _angleStop; set => SetField(ref _angleStop, value); }
    private double _angleStop = 180.0;
    public double AngleStep { get => _angleStep; set => SetField(ref _angleStep, value); }
    private double _angleStep = 1.0;
    public double Overlap { get => _overlap; set => SetField(ref _overlap, value); }
    private double _overlap = 0.3;
    public int TopN { get => _topN; set => SetField(ref _topN, value); }
    private int _topN = 200;
    public int DenseMode { get => _denseMode; set => SetField(ref _denseMode, value); }
    private int _denseMode = 0;
    public double ScaleRange { get => _scaleRange; set => SetField(ref _scaleRange, value); }
    private double _scaleRange = 0.0;

    public string LuaScript { get => _luaScript; set => SetField(ref _luaScript, value); }
    private string _luaScript = "";

    public string Parameters { get => _parameters; set => SetField(ref _parameters, value); }
    private string _parameters = "";

    public string PreprocessType { get => _preprocessType; set => SetField(ref _preprocessType, value); }
    private string _preprocessType = "灰度化";

    public string MeasureType { get => _measureType; set => SetField(ref _measureType, value); }
    private string _measureType = "圆径";

    public double NominalValue { get => _nominalValue; set => SetField(ref _nominalValue, value); }
    private double _nominalValue = 12.0;

    public double Tolerance { get => _tolerance; set => SetField(ref _tolerance, value); }
    private double _tolerance = 0.05;

    public string LogicExpression { get => _logicExpression; set => SetField(ref _logicExpression, value); }
    private string _logicExpression = "score >= 0.85";

    public string LogicRelation { get => _logicRelation; set => SetField(ref _logicRelation, value); }
    private string _logicRelation = "如果";

    public string OutputAddress { get => _outputAddress; set => SetField(ref _outputAddress, value); }
    private string _outputAddress = "PLC_D200";

    public double OutputValue { get => _outputValue; set => SetField(ref _outputValue, value); }
    private double _outputValue = 1;

    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }
    private string _statusText = "";

    public string Operator { get => _operator; set => SetField(ref _operator, value); }
    private string _operator = "大于";

    public string InputValue { get => _inputValue; set => SetField(ref _inputValue, value); }
    private string _inputValue = "";

    public string AiHint
    {
        get => _aiHint;
        set
        {
            if (SetField(ref _aiHint, value))
                OnPropertyChanged(nameof(HasAiHint));
        }
    }
    private string _aiHint = "";

    public bool HasAiHint => !string.IsNullOrWhiteSpace(_aiHint);
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
    private System.Windows.Media.Imaging.BitmapSource? _liveImage;
    public System.Windows.Media.Imaging.BitmapSource? CameraLiveImage { get => _liveImage; set => SetField(ref _liveImage, value); }

    public ICommand ConnectCmd { get; }
    public ICommand StartCmd { get; }
    public ICommand StopCmd { get; }

    public CameraViewModel()
    {
        SelectedCamera = Cameras[0];
        HardwareManager.Instance.Camera.FrameReady += bmp => System.Windows.Application.Current.Dispatcher.Invoke(() => CameraLiveImage = bmp);

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

        ConnectCmd = new RelayCommand(_ => { IsConnected = true; HardwareManager.Instance.Camera.Start(SelectedCamera?.Name); });
        StartCmd = new RelayCommand(_ => { IsConnected = true; HardwareManager.Instance.Camera.Start(SelectedCamera?.Name); });
        StopCmd = new RelayCommand(_ => { IsConnected = false; HardwareManager.Instance.Camera.Stop(); }, _ => IsConnected);
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
        HardwareManager.Instance.Comm.Log += msg => PushLog(msg);
        HardwareManager.Instance.Comm.DataReceived += msg => PushLog($"[接收] " + msg.TrimEnd());

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

                ConnectCmd = new RelayCommand(async _ =>
        {
            if (_selectedConfig == null) return;
            try
            {
                await HardwareManager.Instance.Comm.ConnectAsync(
                    _selectedConfig.CommType, _selectedConfig.Port, _selectedConfig.Baud,
                    _selectedConfig.DataBits, _selectedConfig.Parity, _selectedConfig.StopBits, _selectedConfig.Flow,
                    _selectedConfig.NetIp, _selectedConfig.NetPort);
                IsConnected = HardwareManager.Instance.Comm.IsOpen;
            }
            catch { IsConnected = false; }
        });
                DisconnectCmd = new RelayCommand(async _ =>
        {
            await HardwareManager.Instance.Comm.DisconnectAsync();
            IsConnected = false;
        }, _ => IsConnected);
                SendCmd = new RelayCommand(async _ =>
        {
            if (string.IsNullOrWhiteSpace(_sendText)) return;
            var t = _sendText;
            await HardwareManager.Instance.Comm.SendAsync(t);
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
    private ObservableCollection<VisionFlow> _flows = new();
    public ObservableCollection<VisionFlow> Flows
    {
        get => _flows;
        set => SetField(ref _flows, value);
    }

    private static ObservableCollection<VisionFlow> CreateDefaultFlows()
    {
        return new ObservableCollection<VisionFlow>
        {
            new VisionFlow
            {
                Name = "主流程", Icon = "🔀",
                Steps = new ObservableCollection<VisionFlowStep>
                {
                    new VisionFlowStep { Index = 1, Function = "图像采集", Name = "采集左视野", ParamSummary = "打开文件", Timeout = 5000, CostMs = 12.3, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = "", StatusText = "未开始" },
                    new VisionFlowStep { Index = 2, Function = "模板匹配", Name = "定位基准", ParamSummary = "tpl_A / score≥0.85", Timeout = 3000, CostMs = 8.7, ActualValue = "(120,88)", Icon = "🎯", StepType = "TemplateMatch", ImageSource = "tpl_A.png", TemplateFile = "tpl_A.png", RoiX=80, RoiY=60, RoiW=160, RoiH=120, ScoreThreshold=0.85, StatusText = "匹配成功" },
                    new VisionFlowStep { Index = 3, Function = "几何测量", Name = "测量孔径", ParamSummary = "圆径 / 12.00±0.05", Timeout = 2000, CostMs = 5.4, ActualValue = "12.03", Icon = "📐", StepType = "Measure", StatusText = "尺寸合格" },
                    new VisionFlowStep { Index = 4, Function = "逻辑判断", Name = "判定合格", ParamSummary = "孔径 OK", Timeout = 1000, CostMs = 0.2, ActualValue = "Pass", Icon = "✅", StepType = "Logic", StatusText = "通过" },
                    new VisionFlowStep { Index = 5, Function = "结果输出", Name = "输出结果", ParamSummary = "PLC_D200=1", Timeout = 1000, CostMs = 0.5, ActualValue = "Done", Icon = "📤", StepType = "Output", StatusText = "已完成" },
                }
            },
            new VisionFlow
            {
                Name = "定位流程", Icon = "🧭",
                Steps = new ObservableCollection<VisionFlowStep>
                {
                    new VisionFlowStep { Index = 1, Function = "图像采集", Name = "采集顶视野", ParamSummary = "打开文件", Timeout = 5000, CostMs = 15.1, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = "", StatusText = "未开始" },
                    new VisionFlowStep { Index = 2, Function = "模板匹配", Name = "找中心点", ParamSummary = "tpl_center / score≥0.80", Timeout = 3000, CostMs = 9.2, ActualValue = "(512,384)", Icon = "🎯", StepType = "TemplateMatch", ImageSource = "tpl_center.png", TemplateFile = "tpl_center.png", RoiX=200, RoiY=150, RoiW=180, RoiH=140, ScoreThreshold=0.80, StatusText = "匹配成功" },
                }
            },
            new VisionFlow
            {
                Name = "检测流程", Icon = "🔍",
                Steps = new ObservableCollection<VisionFlowStep>
                {
                    new VisionFlowStep { Index = 1, Function = "图像采集", Name = "采集检测图", ParamSummary = "打开文件", Timeout = 5000, CostMs = 12.3, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = "", StatusText = "未开始" },
                    new VisionFlowStep { Index = 2, Function = "几何测量", Name = "测量边距", ParamSummary = "距离 / 45.0±0.1", Timeout = 2000, CostMs = 6.8, ActualValue = "45.02", Icon = "📐", StepType = "Measure", MeasureType="边距", RoiX=50, RoiY=50, RoiW=200, RoiH=100, StatusText = "尺寸合格" },
                }
            },
        };
    }


    public string[] StepFunctions { get; } = { "图像采集", "模板匹配", "几何测量", "逻辑判断", "结果输出" };
    public string[] PreprocessTypes { get; } = { "灰度化", "二值化", "高斯模糊", "中值滤波", "边缘检测" };
    public string[] MeasureTypes { get; } = { "圆径", "边距", "角度", "面积", "中心距" };
    public string[] LogicRelations { get; } = { "如果", "并且", "或者", "否则", "循环", "跳出", "并行", "等待" };
    public string[] Operators { get; } = { "大于", "小于", "等于", "大于等于", "小于等于", "不等于", "包含", "不包含", "开头为", "结尾为" };
    public string[] StatusOptions { get; } = { "未开始", "运行中", "已完成", "已跳过", "等待中", "错误", "超时", "已暂停", "通过", "不通过", "告警", "采集成功", "采集失败", "匹配成功", "匹配失败", "ROI 内", "ROI 外", "尺寸合格", "尺寸超差", "亮度正常", "亮度异常", "通讯正常", "通讯中断" };
    public string[] CaptureModes { get; } = { "采集相机", "打开文件夹", "打开文件" };
    public string[] MatchModes { get; } = { "灰度匹配", "轮廓匹配" };
    public string[] PropTabs { get; } = { "图像", "Lua", "参数设置" };

    private VisionFlow? _selectedFlow;
    private int _stepCursor = 0;
    public VisionFlow? SelectedFlow { get => _selectedFlow; set { if (SetField(ref _selectedFlow, value)) _stepCursor = 0; } }

    private VisionFlowStep? _selectedStep;
    public VisionFlowStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (SetField(ref _selectedStep, value))
            {
                _matchOverlays.Clear();
                if (_subscribedStep != null)
                {
                    _subscribedStep.PropertyChanged -= SelectedStep_PropertyChanged;
                    _subscribedStep = null;
                }
                if (_selectedStep != null)
                {
                    _selectedStep.PropertyChanged += SelectedStep_PropertyChanged;
                    _subscribedStep = _selectedStep;
                    LoadTemplatePreview(value);
                    if (_selectedStep.MatchMode == MatchModes[1]) ScheduleTemplateContourRefresh();
                    else TemplateContourOverlay = null;
                }
                else
                {
                    TemplateContourOverlay = null;
                }
            }
        }
    }

    private VisionFlowStep? _subscribedStep;
    private DispatcherTimer? _contourTimer;

    private void SelectedStep_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VisionFlowStep.MatchMode)
            or nameof(VisionFlowStep.ContourThreshold)
            or nameof(VisionFlowStep.ContourBlur)
            or nameof(VisionFlowStep.ScaleRange))
        {
            if (_selectedStep?.MatchMode == MatchModes[1]) ScheduleTemplateContourRefresh();
            else TemplateContourOverlay = null;
        }
    }

    private void ScheduleTemplateContourRefresh()
    {
        if (_contourTimer == null)
        {
            _contourTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            _contourTimer.Tick += (_, _) => { _contourTimer.Stop(); RefreshTemplateContour(); };
        }
        _contourTimer.Stop();
        _contourTimer.Start();
    }

    private void RefreshTemplateContour()
    {
        var step = _selectedStep;
        if (step == null || step.MatchMode != MatchModes[1])
        {
            TemplateContourOverlay = null;
            return;
        }
        try
        {
            using var m = new RotatedTemplateMatcher();
            m.UseContour = true;
            m.ContourThreshold = step.ContourThreshold;
            m.ContourBlur = step.ContourBlur;
            m.ScaleRange = step.ScaleRange;
            bool ok = false;
            if (!string.IsNullOrWhiteSpace(step.TemplateFile) && File.Exists(step.TemplateFile))
            {
                using var tpl = Cv2.ImRead(step.TemplateFile, ImreadModes.Grayscale);
                if (tpl != null && !tpl.Empty()) { m.SetTemplate(tpl); ok = true; }
            }
            if (!ok) ok = LoadContourFromSourceRoi(m, step);
            if (!ok) { TemplateContourOverlay = null; return; }
            m.RecomputeContours();
            var mask = m.TemplateContourMask;
            int w = m.TemplateContourW, h = m.TemplateContourH;
            if (mask == null || w <= 0 || h <= 0) { TemplateContourOverlay = null; return; }
            var bmp = new WriteableBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            int stride = w * 4;
            var pixels = new byte[h * stride];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (mask[y * w + x] != 0)
                    {
                        int i = y * stride + x * 4;
                        pixels[i] = 89;
                        pixels[i + 1] = 199;
                        pixels[i + 2] = 52;
                        pixels[i + 3] = 255;
                    }
            bmp.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), pixels, stride, 0);
            bmp.Freeze();
            TemplateContourOverlay = bmp;
        }
        catch
        {
            TemplateContourOverlay = null;
        }
    }

    private bool LoadContourFromSourceRoi(RotatedTemplateMatcher m, VisionFlowStep step)
    {
        string? src = (!string.IsNullOrWhiteSpace(_currentImagePath) && File.Exists(_currentImagePath)) ? _currentImagePath
            : (!string.IsNullOrWhiteSpace(step.ImageSource) && File.Exists(step.ImageSource) ? step.ImageSource : null);
        if (src == null) return false;
        try
        {
            m.LoadSource(src);
            var roi = new Rect((int)step.RoiX, (int)step.RoiY, Math.Max(4, (int)step.RoiW), Math.Max(4, (int)step.RoiH));
            m.SetTemplateFromRoi(roi);
            return true;
        }
        catch { return false; }
    }

    private string _newStepFunction = "模板匹配";
    public string NewStepFunction { get => _newStepFunction; set => SetField(ref _newStepFunction, value); }

    private string _activePropTab = "图像";
    public string ActivePropTab { get => _activePropTab; set => SetField(ref _activePropTab, value); }

    private string _status = "就绪";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // 流程级共享图像：图像采集得到的 Mat，直接传给后续的模板匹配/几何测量步骤
    private Mat? _sharedImage;
    private string _currentImagePath = "";
    public string CurrentImagePath
    {
        get => _currentImagePath;
        set
        {
            if (SetField(ref _currentImagePath, value))
                LoadCurrentImageSource();
        }
    }

    private BitmapImage? _currentImageSource;
    public BitmapImage? CurrentImageSource { get => _currentImageSource; set => SetField(ref _currentImageSource, value); }

    private ObservableCollection<OverlayItem> _matchOverlays = new();
    public ObservableCollection<OverlayItem> MatchOverlays => _matchOverlays;

    private BitmapImage? _templatePreviewSource;
    public BitmapImage? TemplatePreviewSource { get => _templatePreviewSource; set => SetField(ref _templatePreviewSource, value); }

    private BitmapSource? _templateContourOverlay;
    public BitmapSource? TemplateContourOverlay { get => _templateContourOverlay; set => SetField(ref _templateContourOverlay, value); }

    private string _templatePreviewInfo = "尚未确定模板";
    public string TemplatePreviewInfo { get => _templatePreviewInfo; set => SetField(ref _templatePreviewInfo, value); }

    private static string StateFilePath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoCodeVision", "flows.json");

    public ICommand AddFlowCmd { get; }
    public ICommand DeleteFlowCmd { get; }
    public ICommand AddStepCmd { get; }
    public ICommand DeleteStepCmd { get; }
    public ICommand MoveUpCmd { get; }
    public ICommand MoveDownCmd { get; }
    public ICommand SetPropTabCmd { get; }
    public ICommand RunCmd { get; }
    public ICommand StepRunCmd { get; }
    public ICommand ClearCmd { get; }
    public ICommand PickImageCmd { get; }
    public ICommand ConfirmTemplateCmd { get; }
    public ICommand StartMatchCmd { get; }
    public ICommand AskAiCmd { get; }
    public ICommand RunLuaCmd { get; }
    public ICommand DebugLuaCmd { get; }

    public FlowViewModel()
    {
        if (!LoadState())
            _flows = CreateDefaultFlows();
        WireAutoSave();
        SelectedFlow = Flows.FirstOrDefault();
        SelectedStep = SelectedFlow?.Steps.FirstOrDefault();

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
                "模板匹配" => ("🎯", "TemplateMatch"),
                "几何测量" => ("📐", "Measure"),
                "逻辑判断" => ("✅", "Logic"),
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
                StepType = type,
                LuaScript = type == "Lua" ? "-- 在下方编写 Lua 脚本\nlocal score = vision.match(\"tpl_A.png\")\nif score >= 0.85 then\n    plc.write(200, 1)\nend" : ""
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

        PickImageCmd = new RelayCommand(_ =>
        {
            if (_selectedStep == null || _selectedStep.StepType != "ImageCapture") return;
            string? path = null;
            if (_selectedStep.CaptureMode == "打开文件")
            {
                var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff" };
                if (dlg.ShowDialog() == true) path = dlg.FileName;
            }
            else if (_selectedStep.CaptureMode == "打开文件夹")
            {
                // WPF-only folder picker：ValidateNames=false 让对话框可“选中文件夹”
                var dlg = new OpenFileDialog { ValidateNames = false, CheckFileExists = false, CheckPathExists = true, FileName = "选择此文件夹" };
                if (dlg.ShowDialog() == true)
                {
                    var dir = Path.GetDirectoryName(dlg.FileName);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    {
                        _selectedStep.FolderPath = dir;
                        var imgExt = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
                        path = Directory.EnumerateFiles(dir).FirstOrDefault(f => imgExt.Contains(Path.GetExtension(f).ToLowerInvariant()));
                    }
                }
            }
            else // 采集相机
            {
                try
                {
                    using var cap = new VideoCapture(0);
                    if (!cap.IsOpened()) { _selectedStep.StatusText = "无相机"; _selectedStep.ActualValue = "无相机"; return; }
                    using var frame = new Mat();
                    cap.Read(frame);
                    if (frame.Empty()) { _selectedStep.StatusText = "采集失败"; _selectedStep.ActualValue = "采集失败"; return; }
                    var tmp = Path.Combine(Path.GetTempPath(), $"ncv_cam_{DateTime.Now:yyyyMMddHHmmssfff}.png");
                    Cv2.ImWrite(tmp, frame);
                    path = tmp;
                }
                catch (Exception ex) { _selectedStep.StatusText = "采集失败"; _selectedStep.ActualValue = $"错误:{ex.Message}"; return; }
            }
            if (string.IsNullOrWhiteSpace(path)) return;
            _sharedImage?.Dispose();
            _sharedImage = Cv2.ImRead(path, ImreadModes.Color);
            if (_sharedImage == null || _sharedImage.Empty())
            {
                _sharedImage?.Dispose(); _sharedImage = null;
                _selectedStep.StatusText = "读取失败"; _selectedStep.ActualValue = "读取失败";
            }
            else
            {
                _selectedStep.ImageSource = path;
                CurrentImagePath = path;
                _selectedStep.ActualValue = $"{_sharedImage.Width}x{_sharedImage.Height}";
                _selectedStep.StatusText = "采集成功";
            }
        }, _ => _selectedStep != null && _selectedStep.StepType == "ImageCapture");

        ConfirmTemplateCmd = new RelayCommand(_ =>
        {
            if (_selectedStep == null || _selectedStep.StepType != "TemplateMatch") return;
            Mat? src = _sharedImage;
            bool own = false;
            if (src == null && !string.IsNullOrWhiteSpace(CurrentImagePath) && File.Exists(CurrentImagePath))
            {
                src = Cv2.ImRead(CurrentImagePath, ImreadModes.Color);
                own = true;
            }
            if (src == null || src.Empty())
            {
                _selectedStep.StatusText = "无图像源";
                return;
            }
            var roi = new Rect((int)_selectedStep.RoiX, (int)_selectedStep.RoiY, Math.Max(4, (int)_selectedStep.RoiW), Math.Max(4, (int)_selectedStep.RoiH));
            roi.X = Math.Max(0, Math.Min(roi.X, src.Width - 1));
            roi.Y = Math.Max(0, Math.Min(roi.Y, src.Height - 1));
            roi.Width = Math.Min(roi.Width, src.Width - roi.X);
            roi.Height = Math.Min(roi.Height, src.Height - roi.Y);
            if (roi.Width <= 0 || roi.Height <= 0)
            {
                _selectedStep.StatusText = "ROI无效";
                if (own) src.Dispose();
                return;
            }
            using var tpl = new Mat(src, roi);
            var tplDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoCodeVision", "templates");
            Directory.CreateDirectory(tplDir);
            var tmp = Path.Combine(tplDir, $"ncv_tpl_{DateTime.Now:yyyyMMddHHmmssfff}.png");
            Cv2.ImWrite(tmp, tpl);
            _selectedStep.TemplateFile = tmp;
            _selectedStep.StatusText = "模板已确定";
            _selectedStep.ActualValue = $"{roi.Width}x{roi.Height}";
            LoadTemplatePreview(_selectedStep);
            if (own) src.Dispose();
        }, _ => _selectedStep != null && _selectedStep.StepType == "TemplateMatch");

        StartMatchCmd = new RelayCommand(_ =>
        {
            if (_selectedStep == null || _selectedStep.StepType != "TemplateMatch") return;
            _matchOverlays.Clear();
            Mat? src = _sharedImage;
            bool own = false;
            try
            {
                if (src == null && !string.IsNullOrWhiteSpace(CurrentImagePath) && File.Exists(CurrentImagePath))
                {
                    src = Cv2.ImRead(CurrentImagePath, ImreadModes.Color);
                    own = true;
                }
                if (src == null || src.Empty())
                {
                    _selectedStep.StatusText = "无图像源";
                    _selectedStep.ActualValue = "匹配失败";
                    return;
                }
                var matcher = new RotatedTemplateMatcher();
                matcher.SetSource(src);
                matcher.UseContour = _selectedStep.MatchMode == "轮廓匹配";
                if (_selectedStep.MatchMode == "轮廓匹配")
                {
                    matcher.ContourThreshold = _selectedStep.ContourThreshold;
                    matcher.ContourBlur = _selectedStep.ContourBlur;
                }
                var roi = new Rect((int)_selectedStep.RoiX, (int)_selectedStep.RoiY, Math.Max(4, (int)_selectedStep.RoiW), Math.Max(4, (int)_selectedStep.RoiH));
                matcher.SetTemplateFromRoi(roi);
                var sw = Stopwatch.StartNew();
                // 与 GrayMatch.Wpf 一致：全角度范围 -180..180，步长 1，金字塔 3 层，topN 提高以画出所有匹配
                matcher.ScaleRange = _selectedStep.ScaleRange;
                var results = matcher.Match(_selectedStep.PyramidLevel, _selectedStep.AngleStart, _selectedStep.AngleStop,
                    _selectedStep.AngleStep, _selectedStep.ScoreThreshold, _selectedStep.Overlap, _selectedStep.TopN, _selectedStep.DenseMode);
                sw.Stop();
                _selectedStep.CostMs = sw.Elapsed.TotalMilliseconds;
                if (results.Count > 0)
                {
                    // 画出全部匹配结果（模板可能在图中出现多次）
                    foreach (var r in results)
                    {
                        _matchOverlays.Add(new OverlayItem
                        {
                            X = r.CenterX,
                            Y = r.CenterY,
                            // TemplateWidth/Height 已由原生端乘以 mapFactor（=Scale），此处不可再乘，否则框被放大 mapFactor² 倍
                            W = r.TemplateWidth,
                            H = r.TemplateHeight,
                            // 与 VisionMotion/GrayMatch 已验证路径一致：原生角度取负
                            AngleDeg = -r.Angle,
                            Color = "#007AFF",
                            Label = $"相似度 {r.Score:F2}"
                        });
                    }
                    var r0 = results[0];
                    _selectedStep.ActualValue = $"结果 {results.Count} 个 · 首结果({r0.CenterX:F1},{r0.CenterY:F1}) θ{r0.Angle:F1} 相似度{r0.Score:F2} 耗时{_selectedStep.CostMs:F1}ms";
                    _selectedStep.StatusText = $"匹配成功 · 结果数 {results.Count} · 耗时{_selectedStep.CostMs:F1}ms";
                }
                else
                {
                    _selectedStep.ActualValue = $"源图 {src.Width}×{src.Height}，模板 {roi.Width}×{roi.Height}，未匹配（阈值 {_selectedStep.ScoreThreshold:F2}）";
                    _selectedStep.StatusText = "匹配失败";
                }
                if (own) src.Dispose();
            }
            catch (Exception ex)
            {
                _selectedStep.ActualValue = $"匹配异常：{ex.Message}";
                _selectedStep.StatusText = "匹配失败";
            }
        }, _ => _selectedStep != null && _selectedStep.StepType == "TemplateMatch");

        RunCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null || _selectedFlow.Steps.Count == 0) return;
            Status = $"运行中 · {_selectedFlow.Name} · 共 {_selectedFlow.Steps.Count} 步 · {DateTime.Now:HH:mm:ss}";
            _sharedImage?.Dispose();
            _sharedImage = null;
            CurrentImagePath = "";
            var matcher = new RotatedTemplateMatcher();
            _stepCursor = 0;
            int guard = 0;
            int maxIter = _selectedFlow.Steps.Count * 20 + 50;
            while (_stepCursor >= 0 && _stepCursor < _selectedFlow.Steps.Count && guard++ < maxIter)
            {
                var step = _selectedFlow.Steps[_stepCursor];
                SelectedStep = step;
                RunStep(step, matcher);
                _stepCursor = NextStepIndex(_stepCursor);
            }
            _sharedImage?.Dispose();
            _sharedImage = null;
            Status = "完成 · " + _selectedFlow.Name;
        }, _ => _selectedFlow != null && _selectedFlow.Steps.Count > 0);

        StepRunCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null || _selectedFlow.Steps.Count == 0) return;
            if (_stepCursor >= _selectedFlow.Steps.Count) { _stepCursor = 0; _sharedImage?.Dispose(); _sharedImage = null; }
            var step = _selectedFlow.Steps[_stepCursor];
            SelectedStep = step;
            var matcher = new RotatedTemplateMatcher();
            RunStep(step, matcher);
            _stepCursor = NextStepIndex(_stepCursor);
            Status = "单步完成 · 第" + step.Index + "步 " + step.Name;
        }, _ => _selectedFlow != null && _selectedFlow.Steps.Count > 0);
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

        AskAiCmd = new RelayCommand(_ =>
        {
            if (_selectedStep == null) return;
            _selectedStep.AiHint = $"💡 AI 提示：当前是「{_selectedStep.Function}」步骤。可尝试：\n1. 使用 vision.match(...) 获取匹配分数\n2. 用 if score >= threshold then 做分支判断\n3. 调用 plc.write(addr, value) 输出结果";
            _selectedStep.LuaScript += string.IsNullOrWhiteSpace(_selectedStep.LuaScript) ? "-- 已插入 AI 建议\n" : "\n-- 已插入 AI 建议\n";
        }, _ => _selectedStep != null && _selectedStep.StepType == "Lua");

        RunLuaCmd = new RelayCommand(_ =>
        {
            if (_selectedStep == null) return;
            Status = $"Lua 运行 · {_selectedStep.Name} · {DateTime.Now:HH:mm:ss}";
        }, _ => _selectedStep != null && _selectedStep.StepType == "Lua");

        DebugLuaCmd = new RelayCommand(_ =>
        {
            if (_selectedStep == null) return;
            Status = $"Lua 调试 · {_selectedStep.Name} · 断点待命中 · {DateTime.Now:HH:mm:ss}";
        }, _ => _selectedStep != null && _selectedStep.StepType == "Lua");
    }

    private static string MeasureRoi(Mat src, VisionFlowStep step)
    {
        try
        {
            if (src.Empty()) return "读取失败";
            using var gray = new Mat();
            if (src.Channels() == 1) src.CopyTo(gray); else Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            var roi = new Rect((int)step.RoiX, (int)step.RoiY, Math.Max(4, (int)step.RoiW), Math.Max(4, (int)step.RoiH));
            roi.X = Math.Max(0, Math.Min(roi.X, gray.Width - 1));
            roi.Y = Math.Max(0, Math.Min(roi.Y, gray.Height - 1));
            roi.Width = Math.Min(roi.Width, gray.Width - roi.X);
            roi.Height = Math.Min(roi.Height, gray.Height - roi.Y);
            if (roi.Width <= 0 || roi.Height <= 0) return "ROI无效";
            using var roiMat = new Mat(gray, roi);
            roiMat.FindContours(out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (contours.Length == 0) return "未找到轮廓";
            var largest = contours.OrderByDescending((Point[] c) => Cv2.ContourArea(c)).First();
            var rect = Cv2.BoundingRect(largest);
            Cv2.MinEnclosingCircle(largest, out _, out float radius);
            return step.MeasureType switch
            {
                "面积" => $"面积={Cv2.ContourArea(largest):F1}px²",
                "圆径" => $"直径={radius * 2:F2}px",
                "边距" => $"边距={rect.Width:F2}px",
                "角度" => $"角度=0.0°",
                "中心距" => $"中心=({rect.X + rect.Width / 2.0:F1},{rect.Y + rect.Height / 2.0:F1})",
                _ => "未知类型"
            };
        }
        catch (Exception ex)
        {
            return $"错误:{ex.Message}";
        }
    }

    private Mat? LoadCaptureImage(VisionFlowStep step)
    {
        try
        {
            switch (step.CaptureMode)
            {
                case "采集相机":
                {
                    using var cap = new VideoCapture(0);
                    if (!cap.IsOpened()) return null;
                    using var frame = new Mat();
                    cap.Read(frame);
                    if (frame.Empty()) return null;
                    var tmp = Path.Combine(Path.GetTempPath(), $"ncv_cam_{DateTime.Now:yyyyMMddHHmmssfff}.png");
                    Cv2.ImWrite(tmp, frame);
                    step.ImageSource = tmp;
                    return frame.Clone();
                }
                case "打开文件夹":
                {
                    if (string.IsNullOrWhiteSpace(step.FolderPath) || !Directory.Exists(step.FolderPath)) return null;
                    var imgExt = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
                    var first = Directory.EnumerateFiles(step.FolderPath).FirstOrDefault(f => imgExt.Contains(Path.GetExtension(f).ToLowerInvariant()));
                    return first == null ? null : Cv2.ImRead(first, ImreadModes.Color);
                }
                default: // 打开文件
                    return string.IsNullOrWhiteSpace(step.ImageSource) || !File.Exists(step.ImageSource) ? null : Cv2.ImRead(step.ImageSource, ImreadModes.Color);
            }
        }
        catch
        {
            return null;
        }
    }

    private void LoadCurrentImageSource()
    {
        if (string.IsNullOrWhiteSpace(_currentImagePath) || !File.Exists(_currentImagePath))
        {
            CurrentImageSource = null;
            return;
        }
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(_currentImagePath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            CurrentImageSource = bmp;
        }
        catch
        {
            CurrentImageSource = null;
        }
    }

    private void LoadTemplatePreview(VisionFlowStep? step)
    {
        if (step == null || step.StepType != "TemplateMatch"
            || string.IsNullOrWhiteSpace(step.TemplateFile) || !File.Exists(step.TemplateFile))
        {
            TemplatePreviewSource = null;
            TemplatePreviewInfo = "尚未确定模板";
            return;
        }
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(step.TemplateFile);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            TemplatePreviewSource = bmp;
            TemplatePreviewInfo = $"模板分辨率：{bmp.PixelWidth} × {bmp.PixelHeight} px";
        }
        catch
        {
            TemplatePreviewSource = null;
            TemplatePreviewInfo = "模板读取失败";
        }
    }

    #region 自动保存 / 自动载入

    private bool LoadState()
    {
        try
        {
            var path = StateFilePath;
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;
            var flows = JsonSerializer.Deserialize<List<VisionFlow>>(json);
            if (flows == null || flows.Count == 0) return false;
            _flows = new ObservableCollection<VisionFlow>(flows);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveState()
    {
        try
        {
            var path = StateFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_flows, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // 忽略保存失败（如目录不可写）
        }
    }

    private DispatcherTimer? _saveTimer;
    private void RequestSave()
    {
        if (_saveTimer == null)
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _saveTimer.Tick += (_, _) => { _saveTimer?.Stop(); SaveState(); };
        }
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void WireAutoSave()
    {
        WireFlows();
        _flows.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null) foreach (VisionFlow f in e.NewItems) WireFlow(f);
            if (e.OldItems != null) foreach (VisionFlow f in e.OldItems) UnwireFlow(f);
            RequestSave();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => SaveState();
    }

    private void WireFlows()
    {
        foreach (var f in _flows) WireFlow(f);
    }

    private void WireFlow(VisionFlow flow)
    {
        flow.Steps.CollectionChanged += FlowStepsChanged;
        foreach (var s in flow.Steps) WireStep(s);
    }

    private void UnwireFlow(VisionFlow flow)
    {
        flow.Steps.CollectionChanged -= FlowStepsChanged;
        foreach (var s in flow.Steps) UnwireStep(s);
    }

    private void FlowStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null) foreach (VisionFlowStep s in e.NewItems) WireStep(s);
        if (e.OldItems != null) foreach (VisionFlowStep s in e.OldItems) UnwireStep(s);
        RequestSave();
    }

    private void WireStep(VisionFlowStep step) => step.PropertyChanged += StepPropertyChanged;
    private void UnwireStep(VisionFlowStep step) => step.PropertyChanged -= StepPropertyChanged;
    private void StepPropertyChanged(object? sender, PropertyChangedEventArgs e) => RequestSave();

    #endregion

    private void RunStep(VisionFlowStep step, RotatedTemplateMatcher matcher)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            switch (step.StepType)
            {
                case "ImageCapture":
                {
                    _sharedImage = LoadCaptureImage(step);
                    if (_sharedImage == null || _sharedImage.Empty())
                    {
                        _sharedImage = null;
                        step.ActualValue = "采集失败";
                        step.StatusText = "采集失败";
                    }
                    else
                    {
                        step.ActualValue = $"{_sharedImage.Width}x{_sharedImage.Height}";
                        step.StatusText = "采集成功";
                        if (!string.IsNullOrWhiteSpace(step.ImageSource))
                            CurrentImagePath = step.ImageSource;
                    }
                    break;
                }
                case "TemplateMatch":
                {
                    Mat? srcMat = _sharedImage;
                    bool ownMat = false;
                    if (srcMat == null && !string.IsNullOrWhiteSpace(step.ImageSource) && File.Exists(step.ImageSource))
                    {
                        srcMat = Cv2.ImRead(step.ImageSource, ImreadModes.Color);
                        ownMat = true;
                    }
                    if (srcMat == null || srcMat.Empty())
                    {
                        step.ActualValue = "无图像源";
                        step.StatusText = "匹配失败";
                    }
                    else
                    {
                        matcher.SetSource(srcMat);
                        matcher.UseContour = step.MatchMode == "轮廓匹配";
                        if (step.MatchMode == "轮廓匹配")
                        {
                            matcher.ContourThreshold = step.ContourThreshold;
                            matcher.ContourBlur = step.ContourBlur;
                        }
                        var roi = new Rect((int)step.RoiX, (int)step.RoiY, Math.Max(4, (int)step.RoiW), Math.Max(4, (int)step.RoiH));
                        matcher.SetTemplateFromRoi(roi);
                        matcher.ScaleRange = step.ScaleRange;
                        var results = matcher.Match(step.PyramidLevel, step.AngleStart, step.AngleStop, step.AngleStep,
                            step.ScoreThreshold, step.Overlap, step.TopN, step.DenseMode);
                        if (results.Count > 0)
                        {
                            var r = results[0];
                            step.ActualValue = $"({r.CenterX:F1},{r.CenterY:F1}) θ{r.Angle:F1} score{r.Score:F2}";
                            step.StatusText = "匹配成功";
                        }
                        else
                        {
                            step.ActualValue = "未匹配";
                            step.StatusText = "匹配失败";
                        }
                    }
                    if (ownMat && srcMat != null) srcMat.Dispose();
                    break;
                }
                case "Measure":
                {
                    Mat? msrc = _sharedImage;
                    bool ownMat = false;
                    if (msrc == null && !string.IsNullOrWhiteSpace(step.ImageSource) && File.Exists(step.ImageSource))
                    {
                        msrc = Cv2.ImRead(step.ImageSource, ImreadModes.Color);
                        ownMat = true;
                    }
                    if (msrc == null || msrc.Empty())
                    {
                        step.ActualValue = "无图像源";
                        step.StatusText = "测量失败";
                    }
                    else
                    {
                        var measured = MeasureRoi(msrc, step);
                        step.ActualValue = measured;
                        var numMatch = System.Text.RegularExpressions.Regex.Match(measured, @"[-+]?[0-9]*\.?[0-9]+");
                        if (numMatch.Success && double.TryParse(numMatch.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                        {
                            bool ok = System.Math.Abs(val - step.NominalValue) <= step.Tolerance;
                            step.StatusText = ok ? "尺寸合格" : "尺寸超差";
                            step.ActualValue = measured + (ok ? "  合格" : "  超差");
                        }
                        else
                            step.StatusText = "测量完成";
                    }
                    if (ownMat && msrc != null) msrc.Dispose();
                    break;
                }
                default:
                    step.ActualValue = "OK";
                    break;
            }
        }
        catch (Exception ex)
        {
            step.ActualValue = $"错误:{ex.Message}";
            step.StatusText = "错误";
        }
        sw.Stop();
        step.CostMs = sw.Elapsed.TotalMilliseconds;
    }

    private static void Reindex(VisionFlow flow)
    {
        for (int i = 0; i < flow.Steps.Count; i++) flow.Steps[i].Index = i + 1;
    }

    private int NextStepIndex(int i)
    {
        var steps = _selectedFlow?.Steps;
        if (steps == null || i < 0 || i >= steps.Count) return i + 1;
        var step = steps[i];
        switch (step.LogicRelation)
        {
            case "如果":
                if (EvalLogic(step)) return i + 1;
                for (int j = i + 1; j < steps.Count; j++)
                    if (steps[j].LogicRelation == "否则") return j;
                return i + 1;
            case "否则":
                return i + 1;
            case "循环":
                for (int k = i - 1; k >= 0; k--)
                    if (steps[k].LogicRelation == "如果" || steps[k].LogicRelation == "循环") return k;
                return 0;
            case "跳出":
                for (int j = i + 1; j < steps.Count; j++)
                    if (steps[j].LogicRelation == "如果" || steps[j].LogicRelation == "否则" || steps[j].LogicRelation == "循环") return j + 1;
                return steps.Count;
            default:
                return i + 1;
        }
    }

    private bool EvalLogic(VisionFlowStep step)
    {
        try
        {
            double score = 0;
            var av = step.ActualValue ?? "";
            var m = System.Text.RegularExpressions.Regex.Match(av, "score\\s*([0-9.]+)");
            if (m.Success) double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out score);
            else
            {
                var nums = System.Text.RegularExpressions.Regex.Matches(av, "[0-9]+(?:\\.[0-9]+)?");
                if (nums.Count > 0) double.TryParse(nums[nums.Count - 1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out score);
            }
            var expr = (step.LogicExpression ?? "true").Replace("score", score.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var res = new System.Data.DataTable().Compute(expr, null);
            if (res is bool b) return b;
            if (res is int ii) return ii != 0;
            if (res is double dd) return dd != 0;
            return false;
        }
        catch { return false; }
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
