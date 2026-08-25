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
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.Win32;
using GrayMatch;
using OpenCvSharp;
using NoCodeVision;
using NoCodeVision.Scripting;
using NoCodeVision.Services;
using NoCodeVision.Comm;
using System.Threading;
using System.Threading.Tasks;

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
    public string Remark { get; set; } = "";
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

    // ===== 运动控制：轴运动 =====
    public string TargetAxis { get => _targetAxis; set => SetField(ref _targetAxis, value); }
    private string _targetAxis = "X轴";
    public string MoveType { get => _moveType; set => SetField(ref _moveType, value); }
    private string _moveType = "绝对运动";
    public string MoveMode { get => _moveMode; set => SetField(ref _moveMode, value); }
    private string _moveMode = "点位运动";
    public double TargetX { get => _targetX; set => SetField(ref _targetX, value); }
    private double _targetX;
    public double TargetY { get => _targetY; set => SetField(ref _targetY, value); }
    private double _targetY;
    public double TargetZ { get => _targetZ; set => SetField(ref _targetZ, value); }
    private double _targetZ;
    public double Speed { get => _speed; set => SetField(ref _speed, value); }
    private double _speed = 50;
    public double Accel { get => _accel; set => SetField(ref _accel, value); }
    private double _accel = 100;
    public double Decel { get => _decel; set => SetField(ref _decel, value); }
    private double _decel = 100;
    public int InPosTimeout { get => _inPosTimeout; set => SetField(ref _inPosTimeout, value); }
    private int _inPosTimeout = 5000;
    public int ServoOn { get => _servoOn; set => SetField(ref _servoOn, value); }
    private int _servoOn = 1;

    // ===== 运动控制：IO 控制 =====
    public string IoChannel { get => _ioChannel; set => SetField(ref _ioChannel, value); }
    private string _ioChannel = "DO_0";
    public string IoType { get => _ioType; set => SetField(ref _ioType, value); }
    private string _ioType = "输出";
    public int IoValue { get => _ioValue; set => SetField(ref _ioValue, value); }
    private int _ioValue = 1;
    public int WaitDone { get => _waitDone; set => SetField(ref _waitDone, value); }
    private int _waitDone = 1;

    // ===== 运动控制：气缸动作 =====
    public string CylName { get => _cylName; set => SetField(ref _cylName, value); }
    private string _cylName = "气缸1";
    public string CylAction { get => _cylAction; set => SetField(ref _cylAction, value); }
    private string _cylAction = "伸出";
    public int CylTimeout { get => _cylTimeout; set => SetField(ref _cylTimeout, value); }
    private int _cylTimeout = 3000;
    public string SensorIn { get => _sensorIn; set => SetField(ref _sensorIn, value); }
    private string _sensorIn = "IN_0";
    public string SensorOut { get => _sensorOut; set => SetField(ref _sensorOut, value); }
    private string _sensorOut = "IN_1";

    // ===== 运动控制：等待延时 =====
    public string WaitType { get => _waitType; set => SetField(ref _waitType, value); }
    private string _waitType = "时间";
    public int WaitTime { get => _waitTime; set => SetField(ref _waitTime, value); }
    private int _waitTime = 1000;
    public string WaitCondition { get => _waitCondition; set => SetField(ref _waitCondition, value); }
    private string _waitCondition = "signal == 1";

    // ===== 运动控制：通讯指令 =====
    public string CommChannel { get => _commChannel; set => SetField(ref _commChannel, value); }
    private string _commChannel = "PLC-串口";
    public string CommCmd { get => _commCmd; set => SetField(ref _commCmd, value); }
    private string _commCmd = "发送";
    public string CommContent { get => _commContent; set => SetField(ref _commContent, value); }
    private string _commContent = "M100=1";
    public string CommEncoding { get => _commEncoding; set => SetField(ref _commEncoding, value); }
    private string _commEncoding = "ASCII";

    // ===== 图像采集扩展参数 =====
    public double Exposure { get => _exposure; set => SetField(ref _exposure, value); }
    private double _exposure = 8000;
    public double Gain { get => _gain; set => SetField(ref _gain, value); }
    private double _gain = 10;
    public string TriggerMode { get => _triggerMode; set => SetField(ref _triggerMode, value); }
    private string _triggerMode = "连续采集";
    public string PixelFormat { get => _pixelFormat; set => SetField(ref _pixelFormat, value); }
    private string _pixelFormat = "RGB8";
    public int ImageWidth { get => _imageWidth; set => SetField(ref _imageWidth, value); }
    private int _imageWidth = 2448;
    public int ImageHeight { get => _imageHeight; set => SetField(ref _imageHeight, value); }
    private int _imageHeight = 2048;
    public int AutoExposure { get => _autoExposure; set => SetField(ref _autoExposure, value); }
    private int _autoExposure = 0;
    public int SaveImage { get => _saveImage; set => SetField(ref _saveImage, value); }
    private int _saveImage = 0;
    public string ImageFormat { get => _imageFormat; set => SetField(ref _imageFormat, value); }
    private string _imageFormat = "PNG";

    // ===== 模板匹配扩展参数 =====
    public string SortBy { get => _sortBy; set => SetField(ref _sortBy, value); }
    private string _sortBy = "相似度";
    public string BorderMode { get => _borderMode; set => SetField(ref _borderMode, value); }
    private string _borderMode = "常数填充";
    public int UseMask { get => _useMask; set => SetField(ref _useMask, value); }
    private int _useMask = 0;
    public double MinDistance { get => _minDistance; set => SetField(ref _minDistance, value); }
    private double _minDistance = 10;

    // ===== 几何测量扩展参数 =====
    public string MeasureUnit { get => _measureUnit; set => SetField(ref _measureUnit, value); }
    private string _measureUnit = "像素";
    public int Decimals { get => _decimals; set => SetField(ref _decimals, value); }
    private int _decimals = 3;
    public int CaliperCount { get => _caliperCount; set => SetField(ref _caliperCount, value); }
    private int _caliperCount = 20;
    public string EdgePolarity { get => _edgePolarity; set => SetField(ref _edgePolarity, value); }
    private string _edgePolarity = "由亮到暗";
    public double EdgeThreshold { get => _edgeThreshold; set => SetField(ref _edgeThreshold, value); }
    private double _edgeThreshold = 20;
    public string OutputVar { get => _outputVar; set => SetField(ref _outputVar, value); }
    private string _outputVar = "dMeasure";

    // ===== 逻辑判断扩展参数 =====
    public string CompareVar { get => _compareVar; set => SetField(ref _compareVar, value); }
    private string _compareVar = "score";
    public string CompareSource { get => _compareSource; set => SetField(ref _compareSource, value); }
    private string _compareSource = "匹配分数";
    public string Remark { get => _remark; set => SetField(ref _remark, value); }
    private string _remark = "";

    // ===== 结果输出扩展参数 =====
    public string OutputType { get => _outputType; set => SetField(ref _outputType, value); }
    private string _outputType = "PLC";
    public string DataType { get => _dataType; set => SetField(ref _dataType, value); }
    private string _dataType = "整数";
    public string Trigger { get => _trigger; set => SetField(ref _trigger, value); }
    private string _trigger = "立即";
    public int Verify { get => _verify; set => SetField(ref _verify, value); }
    private int _verify = 0;
    public string OnFail { get => _onFail; set => SetField(ref _onFail, value); }
    private string _onFail = "忽略";

    // ===== 新增步骤类型参数（视觉增强 / 逻辑控制 / 数据运算） =====
    public string ColorChannel { get => _colorChannel; set => SetField(ref _colorChannel, value); }
    private string _colorChannel = "灰度";
    public string TargetColor { get => _targetColor; set => SetField(ref _targetColor, value); }
    private string _targetColor = "#FF0000";
    public double ColorTolerance { get => _colorTolerance; set => SetField(ref _colorTolerance, value); }
    private double _colorTolerance = 30;

    public string CountSource { get => _countSource; set => SetField(ref _countSource, value); }
    private string _countSource = "上一步结果";
    public double CountMinArea { get => _countMinArea; set => SetField(ref _countMinArea, value); }
    private double _countMinArea = 50;
    public string CountOutputVar { get => _countOutputVar; set => SetField(ref _countOutputVar, value); }
    private string _countOutputVar = "nCount";

    public string CodeType { get => _codeType; set => SetField(ref _codeType, value); }
    private string _codeType = "QR";
    public string CodeOutputVar { get => _codeOutputVar; set => SetField(ref _codeOutputVar, value); }
    private string _codeOutputVar = "sCode";

    public string OcrLang { get => _ocrLang; set => SetField(ref _ocrLang, value); }
    private string _ocrLang = "数字";
    public string OcrOutputVar { get => _ocrOutputVar; set => SetField(ref _ocrOutputVar, value); }
    private string _ocrOutputVar = "sText";

    public string BranchCondition { get => _branchCondition; set => SetField(ref _branchCondition, value); }
    private string _branchCondition = "score >= 0.85";
    public string BranchTrue { get => _branchTrue; set => SetField(ref _branchTrue, value); }
    private string _branchTrue = "合格分支";
    public string BranchFalse { get => _branchFalse; set => SetField(ref _branchFalse, value); }
    private string _branchFalse = "不合格分支";

    public string LoopType { get => _loopType; set => SetField(ref _loopType, value); }
    private string _loopType = "次数";
    public int LoopCount { get => _loopCount; set => SetField(ref _loopCount, value); }
    private int _loopCount = 3;
    public string LoopCondition { get => _loopCondition; set => SetField(ref _loopCondition, value); }
    private string _loopCondition = "i < 10";
    public string LoopVar { get => _loopVar; set => SetField(ref _loopVar, value); }
    private string _loopVar = "i";

    public string SubFlowName { get => _subFlowName; set => SetField(ref _subFlowName, value); }
    private string _subFlowName = "";

    public string CalcExpression { get => _calcExpression; set => SetField(ref _calcExpression, value); }
    private string _calcExpression = "a + b * 2";
    public string CalcOutputVar { get => _calcOutputVar; set => SetField(ref _calcOutputVar, value); }
    private string _calcOutputVar = "result";

    public string SaveFormat { get => _saveFormat; set => SetField(ref _saveFormat, value); }
    private string _saveFormat = "CSV";
    public string SavePath { get => _savePath; set => SetField(ref _savePath, value); }
    private string _savePath = @"D:\Data\result.csv";
    public string SaveFields { get => _saveFields; set => SetField(ref _saveFields, value); }
    private string _saveFields = "time,score,result";

    public string NotifyMessage { get => _notifyMessage; set => SetField(ref _notifyMessage, value); }
    private string _notifyMessage = "检测完成";
    public string NotifyType { get => _notifyType; set => SetField(ref _notifyType, value); }
    private string _notifyType = "弹窗";
    public string NotifyLevel { get => _notifyLevel; set => SetField(ref _notifyLevel, value); }
    private string _notifyLevel = "信息";

    // ===== ROI 旋转矩形工具参数 =====
    public double RoiAngle { get => _roiAngle; set => SetField(ref _roiAngle, value); }
    private double _roiAngle = 0;

    // ===== 缺陷检测参数 =====
    public double DiffThreshold { get => _diffThreshold; set => SetField(ref _diffThreshold, value); }
    private double _diffThreshold = 45;
    public double MinAreaFrac { get => _minAreaFrac; set => SetField(ref _minAreaFrac, value); }
    private double _minAreaFrac = 0.004;
    public double GlobalBrightnessThresh { get => _globalBrightnessThresh; set => SetField(ref _globalBrightnessThresh, value); }
    private double _globalBrightnessThresh = 28;
    public int EdgeTolerance { get => _edgeTolerance; set => SetField(ref _edgeTolerance, value); }
    private int _edgeTolerance = 0;
    public double EdgeGradThresh { get => _edgeGradThresh; set => SetField(ref _edgeGradThresh, value); }
    private double _edgeGradThresh = 30;
    public int ErodeSize { get => _erodeSize; set => SetField(ref _erodeSize, value); }
    private int _erodeSize = 2;
    public int DilateSize { get => _dilateSize; set => SetField(ref _dilateSize, value); }
    private int _dilateSize = 3;

    // ===== 时序确定性编排（专利：表格化时序标记 + 同步组） =====
    public string TimingMarker { get => _timingMarker; set => SetField(ref _timingMarker, value); }
    private string _timingMarker = "";
    public string SyncGroup { get => _syncGroup; set => SetField(ref _syncGroup, value); }
    private string _syncGroup = "";
    // 编译/运行后回填的监控字段
    public double ExpectedMs { get => _expectedMs; set => SetField(ref _expectedMs, value); }
    private double _expectedMs = -1;
    public double ActualMs { get => _actualMs; set => SetField(ref _actualMs, value); }
    private double _actualMs = -1;
    public double DeviationMs { get => _deviationMs; set => SetField(ref _deviationMs, value); }
    private double _deviationMs;
    public bool TimingAlarm { get => _timingAlarm; set => SetField(ref _timingAlarm, value); }
    private bool _timingAlarm;
    public string TimingStatusText { get => _timingStatusText; set => SetField(ref _timingStatusText, value); }
    private string _timingStatusText = "未编排";

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

public class VisionFlow : ViewModelBase
{
    private string _name = "";
    public string Name { get => _name; set => SetField(ref _name, value); }
    public string Icon { get; set; } = "🔀";
    public string FlowKind { get; set; } = "Normal";
    public string ScriptContent { get; set; } = "-- 脚本流程示例：视觉 + 运控 + 计算\n-- 1) 视觉：采集图像并匹配模板\nlocal ok = vision.grab()                  -- 抓取一帧图像\nlocal score = vision.match(\"tpl_A.png\") -- 模板匹配，返回相似度 0~1\nprint(\"匹配分数: \" .. tostring(score))\n\n-- 2) 计算：判定是否合格，并换算位移量\nlocal threshold = 0.85\nlocal pass = score >= threshold\nlocal dx = (score - threshold) * 100     -- 分数差 → 位移量(px)\nlocal dist = math.sqrt(dx * dx)           -- 用 math 做计算\nprint(\"是否合格: \" .. tostring(pass) .. \"  位移量: \" .. tostring(dist))\n\n-- 3) 运控：合格则驱动 PLC/轴动作，不合格报警\nif pass then\n    plc.write(200, 1)                     -- 触发运动 / 输出\n    plc.write(201, math.floor(dist))      -- 下发放大后的位移量\n    print(\"运控：已发送到位指令\")\nelse\n    plc.write(200, 0)                     -- 复位 / 报警\n    print(\"运控：未达标，已停止\")\nend\n\nsleep(50)\n";
    public ObservableCollection<VisionFlowStep> Steps { get; set; } = new();
}

#endregion

#region 项目页

public class ProjectItem
{
    // 项目信息
    public string ProjectName { get; set; } = "";
    public string Author { get; set; } = "";
    public string Customer { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tags { get; set; } = "";
    public string ProjectVersion { get; set; } = "1.0.0";
    public string CreateTime { get; set; } = "";
    public string ModifyTime { get; set; } = "";
    public string ProjectPath { get; set; } = "";

    // 工程设置
    public bool AutoSave { get; set; } = true;
    public int SaveInterval { get; set; } = 300;
    public string Language { get; set; } = "简体中文";
    public string Theme { get; set; } = "浅色";
    public string DefaultUnit { get; set; } = "毫米";
    public string LogLevel { get; set; } = "Info";
    public int LogKeepDays { get; set; } = 30;
    public bool AutoStart { get; set; } = false;
    public bool AutoRunAfterStart { get; set; } = false;
    public bool EmergencyStopOnError { get; set; } = true;
    public string ImageSavePath { get; set; } = @"D:\Images";
    public string DataSavePath { get; set; } = @"D:\Data";
    public string BackupPath { get; set; } = @"D:\Backup";
    public int MaxBackupCount { get; set; } = 10;
    public bool PasswordEnabled { get; set; } = false;
    public int AutoLockMinutes { get; set; } = 5;
    public bool AllowRemote { get; set; } = false;
    public int MaxUndoSteps { get; set; } = 50;
    public int ResultRetainDays { get; set; } = 90;
    public bool DebugMode { get; set; } = false;

    // 状态
    public string Status { get; set; } = "就绪";
}

public class ProjectViewModel : ViewModelBase
{
    public ObservableCollection<ProjectItem> Projects { get; } = new()
    {
        new ProjectItem
        {
            ProjectName = "DemoVision-01",
            Author = "admin",
            Customer = "内部测试",
            Description = "演示项目：电机支架视觉检测",
            Tags = "检测,电机,支架",
            ProjectVersion = "1.0.0",
            ProjectPath = @"D:\Projects\DemoVision-01.ncv",
            CreateTime = DateTime.Now.ToString("yyyy-MM-dd"),
            ModifyTime = DateTime.Now.ToString("yyyy-MM-dd"),
            AutoSave = true,
            SaveInterval = 300,
            Language = "简体中文",
            Theme = "浅色",
            DefaultUnit = "毫米",
            LogLevel = "Info",
            LogKeepDays = 30,
            AutoStart = false,
            AutoRunAfterStart = false,
            EmergencyStopOnError = true,
            ImageSavePath = @"D:\Images",
            DataSavePath = @"D:\Data",
            BackupPath = @"D:\Backup",
            MaxBackupCount = 10,
            PasswordEnabled = false,
            AutoLockMinutes = 5,
            AllowRemote = false,
            MaxUndoSteps = 50,
            ResultRetainDays = 90,
            DebugMode = false
        },
        new ProjectItem
        {
            ProjectName = "MotorBracket-A",
            Author = "admin",
            Customer = "客户 A",
            Description = "电机支架外观检测",
            Tags = "外观,支架",
            ProjectVersion = "1.2.0",
            ProjectPath = @"D:\Projects\MotorBracket-A.ncv",
            CreateTime = "2026-08-10",
            ModifyTime = "2026-08-18",
            AutoSave = true,
            SaveInterval = 120,
            Language = "简体中文",
            Theme = "浅色",
            DefaultUnit = "毫米",
            LogLevel = "Info",
            LogKeepDays = 30,
            AutoStart = false,
            AutoRunAfterStart = true,
            EmergencyStopOnError = true,
            ImageSavePath = @"D:\Images",
            DataSavePath = @"D:\Data",
            BackupPath = @"D:\Backup",
            MaxBackupCount = 10,
            PasswordEnabled = false,
            AutoLockMinutes = 5,
            AllowRemote = false,
            MaxUndoSteps = 50,
            ResultRetainDays = 90,
            DebugMode = false
        },
        new ProjectItem
        {
            ProjectName = "PCB-Inspection",
            Author = "admin",
            Customer = "客户 B",
            Description = "PCB 焊点与元件检测",
            Tags = "PCB,焊点,SMT",
            ProjectVersion = "2.0.1",
            ProjectPath = @"D:\Projects\PCB-Inspection.ncv",
            CreateTime = "2026-07-22",
            ModifyTime = "2026-08-15",
            AutoSave = false,
            SaveInterval = 600,
            Language = "English",
            Theme = "深色",
            DefaultUnit = "毫米",
            LogLevel = "Warning",
            LogKeepDays = 15,
            AutoStart = true,
            AutoRunAfterStart = false,
            EmergencyStopOnError = true,
            ImageSavePath = @"D:\Images",
            DataSavePath = @"D:\Data",
            BackupPath = @"D:\Backup",
            MaxBackupCount = 5,
            PasswordEnabled = true,
            AutoLockMinutes = 10,
            AllowRemote = true,
            MaxUndoSteps = 30,
            ResultRetainDays = 60,
            DebugMode = true
        },
    };

    private ProjectItem? _selectedProject;
    public ProjectItem? SelectedProject { get => _selectedProject; set => SetField(ref _selectedProject, value); }

    public string[] Languages { get; } = { "简体中文", "繁體中文", "English" };
    public string[] Themes { get; } = { "浅色", "深色", "跟随系统" };
    public string[] Units { get; } = { "毫米", "厘米", "英寸", "像素" };
    public string[] LogLevels { get; } = { "Debug", "Info", "Warning", "Error" };

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
                Customer = "",
                Description = "",
                Tags = "",
                ProjectVersion = "1.0.0",
                ProjectPath = $"D:\\Projects\\NewProject-{next}.ncv",
                CreateTime = DateTime.Now.ToString("yyyy-MM-dd"),
                ModifyTime = DateTime.Now.ToString("yyyy-MM-dd"),
                AutoSave = true,
                SaveInterval = 300,
                Language = "简体中文",
                Theme = "浅色",
                DefaultUnit = "毫米",
                LogLevel = "Info",
                LogKeepDays = 30,
                AutoStart = false,
                AutoRunAfterStart = false,
                EmergencyStopOnError = true,
                ImageSavePath = @"D:\Images",
                DataSavePath = @"D:\Data",
                BackupPath = @"D:\Backup",
                MaxBackupCount = 10,
                PasswordEnabled = false,
                AutoLockMinutes = 5,
                AllowRemote = false,
                MaxUndoSteps = 50,
                ResultRetainDays = 90,
                DebugMode = false
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

    // 图像参数扩展
    private double _frameRate = 30.0;
    public double FrameRate { get => _frameRate; set => SetField(ref _frameRate, value); }

    private double _gamma = 1.0;
    public double Gamma { get => _gamma; set => SetField(ref _gamma, value); }

    private double _brightness;
    public double Brightness { get => _brightness; set => SetField(ref _brightness, value); }

    private double _contrast;
    public double Contrast { get => _contrast; set => SetField(ref _contrast, value); }

    private double _sharpness;
    public double Sharpness { get => _sharpness; set => SetField(ref _sharpness, value); }

    private int _exposureAuto;
    public int ExposureAuto { get => _exposureAuto; set => SetField(ref _exposureAuto, value); }

    private int _gainAuto;
    public int GainAuto { get => _gainAuto; set => SetField(ref _gainAuto, value); }

    private int _whiteBalanceAuto = 1;
    public int WhiteBalanceAuto { get => _whiteBalanceAuto; set => SetField(ref _whiteBalanceAuto, value); }

    private int _whiteBalanceR = 512;
    public int WhiteBalanceR { get => _whiteBalanceR; set => SetField(ref _whiteBalanceR, value); }

    private int _whiteBalanceG = 512;
    public int WhiteBalanceG { get => _whiteBalanceG; set => SetField(ref _whiteBalanceG, value); }

    private int _whiteBalanceB = 512;
    public int WhiteBalanceB { get => _whiteBalanceB; set => SetField(ref _whiteBalanceB, value); }

    // ROI
    private int _roiX;
    public int RoiX { get => _roiX; set => SetField(ref _roiX, value); }

    private int _roiY;
    public int RoiY { get => _roiY; set => SetField(ref _roiY, value); }

    private int _roiW = 2448;
    public int RoiW { get => _roiW; set => SetField(ref _roiW, value); }

    private int _roiH = 2048;
    public int RoiH { get => _roiH; set => SetField(ref _roiH, value); }

    // 触发与采集
    public string[] TriggerSources { get; } = { "内部", "外部Line0", "外部Line1", "软件" };
    private string _triggerSource = "内部";
    public string TriggerSource { get => _triggerSource; set => SetField(ref _triggerSource, value); }

    public string[] TriggerEdges { get; } = { "上升沿", "下降沿", "任意沿" };
    private string _triggerEdge = "上升沿";
    public string TriggerEdge { get => _triggerEdge; set => SetField(ref _triggerEdge, value); }

    public string[] AcquisitionModes { get; } = { "连续", "单帧", "多帧" };
    private string _acquisitionMode = "连续";
    public string AcquisitionMode { get => _acquisitionMode; set => SetField(ref _acquisitionMode, value); }

    private int _bufferCount = 3;
    public int BufferCount { get => _bufferCount; set => SetField(ref _bufferCount, value); }

    private int _timeoutMs = 5000;
    public int TimeoutMs { get => _timeoutMs; set => SetField(ref _timeoutMs, value); }

    // 处理与输出
    public string[] Binnings { get; } = { "1×1", "2×2", "4×4" };
    private string _binning = "1×1";
    public string Binning { get => _binning; set => SetField(ref _binning, value); }

    private int _flipH;
    public int FlipH { get => _flipH; set => SetField(ref _flipH, value); }

    private int _flipV;
    public int FlipV { get => _flipV; set => SetField(ref _flipV, value); }

    private int _packetSize = 1500;
    public int PacketSize { get => _packetSize; set => SetField(ref _packetSize, value); }

    private int _interPacketDelay;
    public int InterPacketDelay { get => _interPacketDelay; set => SetField(ref _interPacketDelay, value); }

    private int _saveImage;
    public int SaveImage { get => _saveImage; set => SetField(ref _saveImage, value); }

    public string[] ImageFormats { get; } = { "PNG", "BMP", "JPG", "TIFF" };
    private string _imageFormat = "PNG";
    public string ImageFormat { get => _imageFormat; set => SetField(ref _imageFormat, value); }

    private string _saveImagePath = @"D:\Images";
    public string SaveImagePath { get => _saveImagePath; set => SetField(ref _saveImagePath, value); }

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

    // 新增通讯方式（UDP / Modbus-TCP / MQTT / HTTP / WebSocket / PLC）所需字段
    public string Url { get; set; } = "";
    public string Topic { get; set; } = "";
    public string SubTopics { get; set; } = "";
    public string UnitId { get; set; } = "1";
    public string RegAddress { get; set; } = "0";
    public string RegCount { get; set; } = "1";
    public string LocalPort { get; set; } = "0";
    public bool Broadcast { get; set; }
    public string Method { get; set; } = "GET";
    public string ClientId { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Rack { get; set; } = "0";
    public string Slot { get; set; } = "1";
    public string Db { get; set; } = "1";
    public string Element { get; set; } = "D";
    public string Station { get; set; } = "0";
    public string FinsDna { get; set; } = "1";
    public string FinsSna { get; set; } = "0";
}

public class CommunicationViewModel : ViewModelBase
{
    public string[] CommTypes { get; } = { "串口", "网口", "UDP", "Modbus-TCP", "MQTT", "HTTP/REST", "WebSocket", "西门子S7", "三菱MC", "欧姆龙FINS" };

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
            new CommConfigItem { Name = "设备-UDP", CommType = "UDP", NetIp = "192.168.1.50", NetPort = "6000" },
            new CommConfigItem { Name = "从站-Modbus", CommType = "Modbus-TCP", NetIp = "192.168.1.60", NetPort = "502", UnitId = "1" },
            new CommConfigItem { Name = "Broker-MQTT", CommType = "MQTT", NetIp = "192.168.1.10", NetPort = "1883", SubTopics = "test/in" },
            new CommConfigItem { Name = "S7-1200", CommType = "西门子S7", NetIp = "192.168.1.20", NetPort = "102", Rack = "0", Slot = "1", Db = "1" },
            new CommConfigItem { Name = "FX-MC", CommType = "三菱MC", NetIp = "192.168.1.30", NetPort = "5007", Element = "D" },
            new CommConfigItem { Name = "CP-FINS", CommType = "欧姆龙FINS", NetIp = "192.168.1.40", NetPort = "9600", FinsDna = "1", FinsSna = "0" },
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
        CommHub.Instance.Log += msg => PushLog(msg);
        CommHub.Instance.DataReceived += msg => PushLog($"[接收] " + msg.TrimEnd());
        CommHub.Instance.StateChanged += open => IsConnected = open;

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
            await CommHub.Instance.ConnectAsync(_selectedConfig);
            IsConnected = CommHub.Instance.IsOpen;
        });
                DisconnectCmd = new RelayCommand(async _ =>
        {
            await CommHub.Instance.DisconnectAsync();
            IsConnected = false;
        }, _ => IsConnected);
                SendCmd = new RelayCommand(async _ =>
        {
            if (string.IsNullOrWhiteSpace(_sendText)) return;
            var t = _sendText;
            await CommHub.Instance.SendAsync(t);
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
        new VarItem { Name = "产品数量", Type = "整数", Value = "0" },
        new VarItem { Name = "合格阈值", Type = "浮点数", Value = "0.85" },
        new VarItem { Name = "型号名称", Type = "字符串", Value = "支架A" },
        new VarItem { Name = "是否合格", Type = "布尔", Value = "是" },
        new VarItem { Name = "运动位置", Type = "浮点数", Value = "120.5" },
    };

    private VarItem? _selected;
    public VarItem? Selected { get => _selected; set => SetField(ref _selected, value); }

    public ICommand AddCmd { get; }
    public ICommand RemoveCmd { get; }
    public ICommand ClearSearchCmd { get; }

    // Excel 批量编辑
    public string ExcelPath { get => _excelPath; set => SetField(ref _excelPath, value); }
    private string _excelPath = "";
    public string ExcelStatus { get => _excelStatus; set => SetField(ref _excelStatus, value); }
    private string _excelStatus = "";
    public ICommand ExportVarsCmd { get; }
    public ICommand ImportVarsCmd { get; }

    private ICollectionView? _view;
    public ICollectionView VariablesView
    {
        get
        {
            _view ??= System.Windows.Data.CollectionViewSource.GetDefaultView(Variables);
            return _view;
        }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetField(ref _searchText, value)) return;
            OnPropertyChanged(nameof(HasSearchText));
            _view ??= System.Windows.Data.CollectionViewSource.GetDefaultView(Variables);
            _view.Filter = FilterRow;
            _view.Refresh();
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(_searchText);

    private bool FilterRow(object obj)
    {
        if (obj is not VarItem v) return false;
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        return v.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || (v.Value != null && v.Value.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
    }

    public VariablesViewModel()
    {
        AddCmd = new RelayCommand(_ =>
        {
            var item = new VarItem { Name = "新变量" + (Variables.Count + 1), Type = "字符串", Value = "0" };
            Variables.Add(item);
            Selected = item;
        });
        RemoveCmd = new RelayCommand(_ =>
        {
            if (_selected != null)
            {
                Variables.Remove(_selected);
                Selected = Variables.Count > 0 ? Variables[0] : null;
            }
        }, _ => _selected != null);

        ClearSearchCmd = new RelayCommand(_ => { SearchText = ""; });

        ExportVarsCmd = new RelayCommand(_ =>
        {
            var path = string.IsNullOrWhiteSpace(ExcelPath)
                ? ExcelBatchEdit.Export(Variables)
                : ExcelBatchEdit.Export(Variables, Path.GetFileNameWithoutExtension(ExcelPath));
            if (string.IsNullOrWhiteSpace(ExcelPath)) ExcelBatchEdit.OpenInExcel(path);
            ExcelStatus = "已导出 " + Variables.Count + " 个变量 → " + path;
        });
        ImportVarsCmd = new RelayCommand(_ =>
        {
            var path = ExcelPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                ExcelStatus = "请先在「Excel 路径」填写有效文件，或先点「导出Excel」生成文件。";
                return;
            }
            var rows = ExcelBatchEdit.Import<VarItem>(path);
            Variables.Clear();
            foreach (var r in rows) Variables.Add(r);
            ExcelStatus = "已导入 " + rows.Count + " 个变量（来自 " + path + "）";
        }, _ => !string.IsNullOrWhiteSpace(ExcelPath) && File.Exists(ExcelPath));
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

    // 新建「流程 / 脚本流程」时预置的完整 5 步流水线：采集 → 模板匹配 → 缺陷检测 → 测量 → 通讯
    private static ObservableCollection<VisionFlowStep> CreateStandardPipeline()
    {
        return new ObservableCollection<VisionFlowStep>
        {
            new VisionFlowStep
            {
                Index = 1, Function = "图像采集", Name = "采集图像", ParamSummary = "打开文件", Timeout = 5000, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = "", StatusText = "未开始"
            },
            new VisionFlowStep
            {
                Index = 2, Function = "模板匹配", Name = "模板匹配", ParamSummary = "tpl / score≥0.85", Timeout = 3000, ActualValue = "未匹配", Icon = "🎯", StepType = "TemplateMatch", MatchMode = "灰度匹配", ScoreThreshold = 0.85, RoiX = 80, RoiY = 60, RoiW = 160, RoiH = 120, StatusText = "未开始"
            },
            new VisionFlowStep
            {
                Index = 3, Function = "缺陷检测", Name = "缺陷检测", ParamSummary = "差异比对 / 阈值45", Timeout = 3000, ActualValue = "未检测", Icon = "🔍", StepType = "Defect", StatusText = "未开始"
            },
            new VisionFlowStep
            {
                Index = 4, Function = "几何测量", Name = "几何测量", ParamSummary = "圆径 / 12.00±0.05", Timeout = 2000, ActualValue = "未测量", Icon = "📐", StepType = "Measure", MeasureType = "圆径", NominalValue = 12.0, Tolerance = 0.05, StatusText = "未开始"
            },
            new VisionFlowStep
            {
                Index = 5, Function = "通讯发送", Name = "通讯发送", ParamSummary = "PLC-串口 / M100=1", Timeout = 1000, ActualValue = "未发送", Icon = "📡", StepType = "Comm", CommChannel = "PLC-串口", CommCmd = "发送", CommContent = "M100=1", CommEncoding = "ASCII", StatusText = "未开始"
            },
        };
    }


    public string[] StepFunctions { get; } = { "图像采集", "模板匹配", "几何测量", "逻辑判断", "结果输出", "缺陷检测", "Lua脚本", "轴运动", "IO控制", "气缸动作", "等待延时", "通讯指令", "颜色检测", "目标计数", "条码识别", "字符识别", "条件分支", "循环", "子流程", "变量计算", "数据保存", "消息提示" };
    public string[] PreprocessTypes { get; } = { "灰度化", "二值化", "高斯模糊", "中值滤波", "边缘检测" };
    public string[] MeasureTypes { get; } = { "圆径", "边距", "角度", "面积", "中心距" };
    public string[] LogicRelations { get; } = { "如果", "并且", "或者", "否则", "循环", "跳出", "并行", "等待" };
    public string[] Operators { get; } = { "大于", "小于", "等于", "大于等于", "小于等于", "不等于", "包含", "不包含", "开头为", "结尾为" };
    public string[] StatusOptions { get; } = { "未开始", "运行中", "已完成", "已跳过", "等待中", "错误", "超时", "已暂停", "通过", "不通过", "告警", "采集成功", "采集失败", "匹配成功", "匹配失败", "ROI 内", "ROI 外", "尺寸合格", "尺寸超差", "亮度正常", "亮度异常", "通讯正常", "通讯中断", "运动完成", "到位", "未到位", "动作完成" };
    public string[] CaptureModes { get; } = { "采集相机", "打开文件夹", "打开文件" };
    public string[] MatchModes { get; } = { "灰度匹配", "轮廓匹配" };
    public string[] PropTabs { get; } = { "图像", "Lua", "参数设置" };

    // 运动控制：轴运动
    public string[] AxisNames { get; } = { "X轴", "Y轴", "Z轴", "R轴(旋转)", "U轴", "V轴" };
    public string[] MoveTypes { get; } = { "绝对运动", "相对运动" };
    public string[] MoveModes { get; } = { "点位运动", "直线插补", "圆弧插补", "圆弧插补(逆时针)" };
    // 运动控制：IO 控制
    public string[] IoTypes { get; } = { "输出", "输入" };
    public string[] IoChannels { get; } = { "DO_0", "DO_1", "DO_2", "DO_3", "DO_4", "DO_5", "DO_6", "DO_7" };
    public string[] IoInputChannels { get; } = { "DI_0", "DI_1", "DI_2", "DI_3", "DI_4", "DI_5", "DI_6", "DI_7" };
    // 运动控制：气缸动作
    public string[] CylActions { get; } = { "伸出", "缩回" };
    // 运动控制：等待延时
    public string[] WaitTypes { get; } = { "时间", "信号", "条件" };
    // 运动控制：通讯指令
    public string[] CommCmds { get; } = { "发送", "接收" };
    public string[] CommEncodings { get; } = { "ASCII", "HEX", "UTF8" };
    // 图像采集扩展
    public string[] CaptureTriggerModes { get; } = { "连续采集", "软触发", "硬触发" };
    public string[] CapturePixelFormats { get; } = { "Mono8", "RGB8", "Mono12" };
    public string[] ImageFormats { get; } = { "PNG", "BMP", "JPG", "TIFF" };
    // 模板匹配扩展
    public string[] SortBys { get; } = { "相似度", "位置", "角度" };
    public string[] BorderModes { get; } = { "常数填充", "边缘复制", "镜像" };
    // 几何测量扩展
    public string[] MeasureUnits { get; } = { "像素", "毫米", "微米" };
    public string[] EdgePolarities { get; } = { "由亮到暗", "由暗到亮", "任意" };
    // 逻辑判断扩展
    public string[] CompareSources { get; } = { "匹配分数", "测量值", "变量", "输入值" };
    // 结果输出扩展
    public string[] OutputTypes { get; } = { "PLC", "变量", "通讯", "文件" };
    public string[] DataTypes { get; } = { "整数", "浮点", "字符串", "布尔" };
    public string[] OutputTriggers { get; } = { "立即", "到位后", "条件满足" };
    public string[] OnFails { get; } = { "忽略", "报警", "停机" };
    // 新增步骤类型选项
    public string[] ColorChannels { get; } = { "灰度", "R", "G", "B", "HSV" };
    public string[] CountSources { get; } = { "上一步结果", "ROI区域" };
    public string[] CodeTypes { get; } = { "QR", "Code128", "Code39", "DataMatrix", "EAN13" };
    public string[] OcrLangs { get; } = { "数字", "英文", "中文", "中英文" };
    public string[] LoopTypes { get; } = { "次数", "条件" };
    public string[] SaveFormats { get; } = { "CSV", "JSON", "Excel" };
    public string[] NotifyTypes { get; } = { "弹窗", "声音", "日志" };
    public string[] NotifyLevels { get; } = { "信息", "警告", "错误" };

    private VisionFlow? _selectedFlow;
    private int _stepCursor = 0;
    public VisionFlow? SelectedFlow
    {
        get => _selectedFlow;
        set
        {
            if (!SetField(ref _selectedFlow, value)) return;
            _stepCursor = 0;
            // 切换流程时重置脚本调试状态
            ScriptOutput = "";
            Variables.Clear();
            CallStack.Clear();
            Breakpoints.Clear();
            CurrentLine = -1;
            ScriptIsRunning = false;
            ScriptIsPaused = false;
            ScriptElapsedMs = 0;
            _luaHost?.Stop();
        }
    }

    private VisionFlowStep? _selectedStep;
    public bool IsDefectStepSelected => _selectedStep?.StepType == "Defect";

    public VisionFlowStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (SetField(ref _selectedStep, value))
            {
                if (value?.StepType != "Defect")
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
                    if (_selectedStep.StepType == "Defect")
                    {
                        if (_lastMatchResults != null && _lastMatchResults.Count > 0)
                            RedetectDefects();
                        else
                            DefectSummaryText = "请先运行模板匹配步骤";
                    }
                    else
                    {
                        DefectOverlayImage = null;
                        FlowDefectResults.Clear();
                        DefectSummaryText = "请先运行检测";
                    }
                    if (_selectedStep.MatchMode == MatchModes[1]) ScheduleTemplateContourRefresh();
                    else TemplateContourOverlay = null;
                }
                else
                {
                    TemplateContourOverlay = null;
                    DefectOverlayImage = null;
                    FlowDefectResults.Clear();
                    DefectSummaryText = "请先运行检测";
                }
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(IsDefectStepSelected));
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
        if (e.PropertyName is nameof(VisionFlowStep.DiffThreshold)
            or nameof(VisionFlowStep.MinAreaFrac)
            or nameof(VisionFlowStep.GlobalBrightnessThresh)
            or nameof(VisionFlowStep.EdgeTolerance)
            or nameof(VisionFlowStep.EdgeGradThresh)
            or nameof(VisionFlowStep.ErodeSize)
            or nameof(VisionFlowStep.DilateSize))
        {
            if (_selectedStep?.StepType == "Defect" && _lastMatchResults != null && _lastMatchResults.Count > 0)
                ScheduleDefectRefresh();
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

    private DispatcherTimer? _defectTimer;

    private void ScheduleDefectRefresh()
    {
        if (_defectTimer == null)
        {
            _defectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _defectTimer.Tick += (_, _) => { _defectTimer.Stop(); RedetectDefects(); };
        }
        _defectTimer.Stop();
        _defectTimer.Start();
    }

    private void RedetectDefects()
    {
        var step = _selectedStep;
        if (step == null || step.StepType != "Defect")
        {
            DefectOverlayImage = null;
            FlowDefectResults.Clear();
            DefectSummaryText = "请先选择缺陷检测步骤";
            Status = "缺陷检测：当前未选中缺陷检测步骤";
            return;
        }
        if (_lastMatchResults == null || _lastMatchResults.Count == 0)
        {
            DefectOverlayImage = null;
            FlowDefectResults.Clear();
            DefectSummaryText = "请先运行模板匹配步骤";
            Status = "缺陷检测：请先运行模板匹配步骤";
            return;
        }

        // 缺陷检测需要 matcher 同时持有源图和模板； standalone 重检时必须重建这组状态。
        var tmplStep = _lastTemplateMatchStep;
        if (tmplStep == null && _selectedFlow != null)
        {
            int idx = _selectedFlow.Steps.IndexOf(step);
            for (int i = idx - 1; i >= 0; i--)
            {
                if (_selectedFlow.Steps[i].StepType == "TemplateMatch")
                {
                    tmplStep = _selectedFlow.Steps[i];
                    break;
                }
            }
        }
        if (tmplStep == null)
        {
            DefectOverlayImage = null;
            FlowDefectResults.Clear();
            DefectSummaryText = "未找到前置模板匹配步骤";
            Status = "缺陷检测：未找到前置模板匹配步骤";
            return;
        }

        Mat? srcMat = _sharedImage;
        bool ownMat = false;
        if (srcMat == null || srcMat.Empty())
        {
            string? srcPath = (!string.IsNullOrWhiteSpace(CurrentImagePath) && File.Exists(CurrentImagePath)) ? CurrentImagePath
                : (!string.IsNullOrWhiteSpace(tmplStep.ImageSource) && File.Exists(tmplStep.ImageSource)) ? tmplStep.ImageSource
                : null;
            if (srcPath != null)
            {
                srcMat = Cv2.ImRead(srcPath, ImreadModes.Color);
                ownMat = true;
            }
        }
        if (srcMat == null || srcMat.Empty())
        {
            DefectOverlayImage = null;
            FlowDefectResults.Clear();
            DefectSummaryText = "无图像源，无法检测缺陷";
            Status = "缺陷检测：无图像源";
            return;
        }

        try
        {
            using var matcher = new RotatedTemplateMatcher();
            matcher.SetSource(srcMat);
            var roi = new Rect((int)tmplStep.RoiX, (int)tmplStep.RoiY, Math.Max(4, (int)tmplStep.RoiW), Math.Max(4, (int)tmplStep.RoiH));
            matcher.SetTemplateFromRoi(roi);
            matcher.UseContour = tmplStep.MatchMode == "轮廓匹配";
            if (matcher.UseContour)
            {
                matcher.ContourThreshold = tmplStep.ContourThreshold;
                matcher.ContourBlur = tmplStep.ContourBlur;
            }
            matcher.DefectOptions = new DefectOptions
            {
                DiffThreshold = step.DiffThreshold,
                MinAreaFrac = step.MinAreaFrac,
                GlobalBrightnessThresh = step.GlobalBrightnessThresh,
                EdgeTolerance = step.EdgeTolerance,
                EdgeGradThresh = step.EdgeGradThresh,
                ErodeSize = step.ErodeSize,
                DilateSize = step.DilateSize,
            };
            var defects = matcher.DetectDefects(_lastMatchResults);
            FlowDefectResults.Clear();
            foreach (var d in defects) FlowDefectResults.Add(d);
            DefectSummaryText = defects.Count == 0
                ? "未发现缺陷"
                : $"发现 {defects.Count} 处缺陷";
            DefectOverlayImage = BuildDefectOverlay(defects);
            Status = defects.Count == 0
                ? "缺陷检测完成：未发现缺陷"
                : $"缺陷检测完成：发现 {defects.Count} 处缺陷";
        }
        catch (Exception ex)
        {
            FlowDefectResults.Clear();
            DefectSummaryText = "重检失败：" + ex.Message;
            DefectOverlayImage = null;
            Status = "缺陷检测失败：" + ex.Message;
        }
        finally
        {
            if (ownMat && srcMat != null) srcMat.Dispose();
        }
    }

    private ImageSource? BuildDefectOverlay(List<DefectResult> defects)
    {
        if (defects.Count == 0) return null;
        if (CurrentImageSource == null) return null;
        int w = CurrentImageSource.PixelWidth, h = CurrentImageSource.PixelHeight;
        if (w <= 0 || h <= 0) return null;

        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        wb.Lock();
        try
        {
            int stride = wb.BackBufferStride;
            var px = new byte[stride * h];
            foreach (var d in defects)
            {
                if (d.Pixels == null || d.Pw <= 0 || d.Ph <= 0) continue;
                double phi = -d.Angle * System.Math.PI / 180.0;
                double cosv = System.Math.Cos(phi), sinv = System.Math.Sin(phi);
                double tw = d.Tw, th = d.Th;
                for (int ly = 0; ly < d.Ph; ly++)
                {
                    int baseOff = ly * d.Pw;
                    for (int lx = 0; lx < d.Pw; lx++)
                    {
                        if (d.Pixels[baseOff + lx] == 0) continue;
                        double ux = lx - tw / 2.0;
                        double uy = ly - th / 2.0;
                        int ix = (int)System.Math.Round(d.CenterX + (ux * cosv - uy * sinv));
                        int iy = (int)System.Math.Round(d.CenterY + (ux * sinv + uy * cosv));
                        if (ix < 0 || iy < 0 || ix >= w || iy >= h) continue;
                        int idx = iy * stride + ix * 4;
                        px[idx] = 0;
                        px[idx + 1] = 0;
                        px[idx + 2] = 255;
                        px[idx + 3] = 220;
                    }
                }
            }
            Marshal.Copy(px, 0, wb.BackBuffer, px.Length);
        }
        finally
        {
            wb.AddDirtyRect(new System.Windows.Int32Rect(0, 0, w, h));
            wb.Unlock();
        }
        wb.Freeze();
        return wb;
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

    // 运行控制：协作式异步运行（全部运行时可在每步之间暂停/停止，并自动跳到当前运行行）
    private bool _isRunning;
    private bool _isPaused;
    private CancellationTokenSource? _cts;
    private const int StepPaceMs = 250;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetField(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(PauseResumeText));
                OnPropertyChanged(nameof(RunStateText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetField(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(PauseResumeText));
                OnPropertyChanged(nameof(RunStateText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string PauseResumeText => _isPaused ? "▶ 继续" : "⏸ 暂停";
    public string RunStateText => IsRunning ? (IsPaused ? "⏸ 已暂停" : "▶ 运行中") : "就绪";

    // 流程级共享图像：图像采集得到的 Mat，直接传给后续的模板匹配/几何测量步骤
    private Mat? _sharedImage;
    private List<MatchResult>? _lastMatchResults;
    private VisionFlowStep? _lastTemplateMatchStep; // 生成 _lastMatchResults 的模板匹配步骤，用于缺陷检测时重建 matcher 状态
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

    public ImageSource? DefectOverlayImage { get => _defectOverlayImage; set => SetField(ref _defectOverlayImage, value); }
    private ImageSource? _defectOverlayImage;

    public ObservableCollection<DefectResult> FlowDefectResults => _flowDefectResults;
    private readonly ObservableCollection<DefectResult> _flowDefectResults = new();

    // 几何测量：交互式测量结果（线段距离 / 圆形半径），由 RoiImageView 绘制时回写
    public ObservableCollection<Views.Controls.MeasureItem> MeasureAnnotations => _measureAnnotations;
    private readonly ObservableCollection<Views.Controls.MeasureItem> _measureAnnotations = new();

    public string DefectSummaryText { get => _defectSummaryText; set => SetField(ref _defectSummaryText, value); }
    private string _defectSummaryText = "请先运行检测";

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
    public ICommand RunDefectCmd { get; }
    public ICommand PauseResumeCmd { get; }
    public ICommand StopCmd { get; }
    public ICommand AddScriptFlowCmd { get; }
    public ICommand RenameFlowCmd { get; }
    public ICommand ClearCmd { get; }
    public ICommand PickImageCmd { get; }
    public ICommand ConfirmTemplateCmd { get; }
    public ICommand StartMatchCmd { get; }
    public ICommand AskAiCmd { get; }
    public ICommand RunLuaCmd { get; }
    public ICommand DebugLuaCmd { get; }
        public ICommand RunScriptCmd { get; }
        public ICommand StopScriptCmd { get; }
        public ICommand StepScriptCmd { get; }
        public ICommand PauseResumeScriptCmd { get; }
        public ICommand ToggleBreakpointCmd { get; }
        public ICommand ClearBreakpointsCmd { get; }

    // ===== 时序确定性编排引擎（专利：时序标记 + 同步组 → 实时调度表 → 偏差监控） =====
        public ICommand CheckSyntaxCmd { get; }
    public double BusCycleMs { get => _busCycleMs; set => SetField(ref _busCycleMs, value); }
    private double _busCycleMs = 1.0;
    public double TimingThresholdMs { get => _timingThresholdMs; set => SetField(ref _timingThresholdMs, value); }
    private double _timingThresholdMs = 0.5;
    public bool TimingCompiled { get => _timingCompiled; set => SetField(ref _timingCompiled, value); }
    private bool _timingCompiled;
    public string TimingResultText { get => _timingResultText; set => SetField(ref _timingResultText, value); }
    private string _timingResultText = "尚未编译";
    public ObservableCollection<string> TimingWarnings { get; } = new();
    public ICommand CompileTimingCmd { get; }
    public ICommand RunTimingPlanCmd { get; }

        // ---- 脚本流程调试状态 ----
        private LuaDebugHost? _luaHost;
        private bool _scriptIsRunning;
        private bool _scriptIsPaused;
        private double _scriptElapsedMs;
        private string _scriptOutput = "";
        private string _lastPrint = "";
        private int _currentLine = -1;
        private NoCodeVision.Scripting.VarItem? _selectedVariable;

        public ObservableCollection<NoCodeVision.Scripting.VarItem> Variables { get; } = new();
        public ObservableCollection<string> CallStack { get; } = new();
        public ObservableCollection<int> Breakpoints { get; } = new();
        public bool ScriptIsRunning { get => _scriptIsRunning; set { if (SetField(ref _scriptIsRunning, value)) { OnPropertyChanged(nameof(ScriptStateText)); OnPropertyChanged(nameof(ScriptPauseResumeText)); } } }
        public bool ScriptIsPaused { get => _scriptIsPaused; set { if (SetField(ref _scriptIsPaused, value)) { OnPropertyChanged(nameof(ScriptStateText)); OnPropertyChanged(nameof(ScriptPauseResumeText)); } } }
        public string ScriptPauseResumeText => ScriptIsPaused ? "▶ 继续" : "⏸ 暂停";
        public double ScriptElapsedMs { get => _scriptElapsedMs; set { if (SetField(ref _scriptElapsedMs, value)) OnPropertyChanged(nameof(ScriptElapsedText)); } }
        public string ScriptOutput { get => _scriptOutput; set => SetField(ref _scriptOutput, value); }
        public string LastPrint { get => _lastPrint; set => SetField(ref _lastPrint, value); }
        public int CurrentLine { get => _currentLine; set => SetField(ref _currentLine, value); }
        public NoCodeVision.Scripting.VarItem? SelectedVariable { get => _selectedVariable; set => SetField(ref _selectedVariable, value); }
        public string ScriptStateText => !ScriptIsRunning ? "就绪" : (ScriptIsPaused ? "⏸ 已暂停" : "▶ 运行中");
        public string ScriptElapsedText => ScriptElapsedMs >= 1000 ? (ScriptElapsedMs / 1000).ToString("F2") + " s" : ((int)ScriptElapsedMs).ToString() + " ms";

        public FlowViewModel()
        {
            if (!LoadState())
                _flows = CreateDefaultFlows();
            WireAutoSave();
            var uiCtx = SynchronizationContext.Current;
            _luaHost = new LuaDebugHost(
                uiCtx,
                onOutput: s => { var line = s == null ? "" : s.ToString(); LastPrint = line; ScriptOutput = ScriptOutput.Length > 8000 ? line + "\n" : ScriptOutput + line + "\n"; },
                onVariables: vars => { Variables.Clear(); foreach (var v in vars) Variables.Add(v); },
                onCallStack: st => { CallStack.Clear(); foreach (var l in st) CallStack.Add(l); },
                onCurrentLine: ln => CurrentLine = ln,
                onElapsed: ms => ScriptElapsedMs = ms,
                onRunState: (running, paused) => { ScriptIsRunning = running; ScriptIsPaused = paused; CommandManager.InvalidateRequerySuggested(); });
            SelectedFlow = Flows.FirstOrDefault();
            SelectedStep = SelectedFlow?.Steps.FirstOrDefault();
            // 专利示例：默认流程预置「同步组 + 相对时延」时序编排，便于直接体验编译/运行
            foreach (var f in _flows)
            {
                int j = 0;
                foreach (var st in f.Steps)
                {
                    if (j == 0 || j == 1) { st.TimingMarker = "T+0ms"; st.SyncGroup = "GroupA"; }
                    else st.TimingMarker = $"T+{j * 2}ms";
                    j++;
                }
            }
            _measureAnnotations.CollectionChanged += OnMeasureAnnotationsChanged;

        AddFlowCmd = new RelayCommand(_ =>
        {
            var next = Flows.Count + 1;
            var flow = new VisionFlow
            {
                Name = $"新流程-{next}",
                Icon = "🔀",
                Steps = CreateStandardPipeline()
            };
            Flows.Add(flow);
            SelectedFlow = flow;
            SelectedStep = flow.Steps.FirstOrDefault();
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
                "缺陷检测" => ("🔍", "Defect"),
                "Lua脚本" => ("📝", "Lua"),
                "轴运动" => ("🦾", "AxisMove"),
                "IO控制" => ("🔌", "IOControl"),
                "气缸动作" => ("🟢", "Cylinder"),
                "等待延时" => ("⏱", "Wait"),
                "通讯指令" => ("📡", "Comm"),
                "颜色检测" => ("🎨", "Color"),
                "目标计数" => ("🔢", "Count"),
                "条码识别" => ("🔖", "Code"),
                "字符识别" => ("🔤", "Ocr"),
                "条件分支" => ("🔀", "Branch"),
                "循环" => ("🔁", "Loop"),
                "子流程" => ("📑", "SubFlow"),
                "变量计算" => ("🧮", "Calc"),
                "数据保存" => ("💾", "Save"),
                "消息提示" => ("💬", "Notify"),
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

        AddScriptFlowCmd = new RelayCommand(_ =>
        {
            var next = Flows.Count + 1;
            var flow = new VisionFlow
            {
                Name = $"脚本流程-{next}",
                Icon = "📝",
                FlowKind = "Script",
                Steps = CreateStandardPipeline()
            };
            Flows.Add(flow);
            SelectedFlow = flow;
        }, _ => true);

        RenameFlowCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null) return;
            var owner = System.Windows.Application.Current.MainWindow;
            var newName = Views.InputDialog.Show(owner, "请输入新流程名称：", _selectedFlow.Name);
            if (!string.IsNullOrWhiteSpace(newName))
                _selectedFlow.Name = newName.Trim();
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
                            Color = "#34C759",
                            Label = $"相似度 {r.Score:F2} · θ{r.Angle:F1}°"
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

        RunCmd = new RelayCommand(_ => _ = RunAllAsync(),
            _ => _selectedFlow != null && _selectedFlow.Steps.Count > 0 && !IsRunning);

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
        }, _ => _selectedFlow != null && _selectedFlow.Steps.Count > 0 && !IsRunning);

        RunDefectCmd = new RelayCommand(_ =>
        {
            if (_selectedStep?.StepType != "Defect")
            {
                DefectSummaryText = "请先选择缺陷检测步骤";
                Status = "缺陷检测：当前未选中缺陷检测步骤";
                return;
            }
            // 立即反馈，确认点击已生效
            Status = "正在检测缺陷…";
            DefectSummaryText = "检测中…";
            if (_lastMatchResults == null || _lastMatchResults.Count == 0)
            {
                DefectSummaryText = "请先运行模板匹配步骤";
                Status = "缺陷检测：请先运行模板匹配步骤";
                return;
            }
            RedetectDefects();
            Status = FlowDefectResults.Count == 0 ? "缺陷检测完成：未发现缺陷" : $"缺陷检测完成：发现 {FlowDefectResults.Count} 处缺陷";
        }, _ => true);

        PauseResumeCmd = new RelayCommand(_ => { IsPaused = !IsPaused; }, _ => IsRunning);

        StopCmd = new RelayCommand(_ =>
        {
            _cts?.Cancel();
            IsPaused = false;
        }, _ => IsRunning);

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

        RunScriptCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null || _selectedFlow.FlowKind != "Script") return;
            _luaHost?.SetBreakpoints(Breakpoints);
            _luaHost?.Run(_selectedFlow.ScriptContent, false);
        }, _ => _selectedFlow != null && _selectedFlow.FlowKind == "Script" && !ScriptIsRunning);

        StepScriptCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null || _selectedFlow.FlowKind != "Script") return;
            _luaHost?.SetBreakpoints(Breakpoints);
            _luaHost?.Step(_selectedFlow.ScriptContent);
        }, _ => _selectedFlow != null && _selectedFlow.FlowKind == "Script");

        PauseResumeScriptCmd = new RelayCommand(_ =>
        {
            if (_luaHost == null) return;
            if (ScriptIsPaused) _luaHost.Resume();
            else _luaHost.Pause();
        }, _ => _selectedFlow != null && _selectedFlow.FlowKind == "Script" && ScriptIsRunning);

        StopScriptCmd = new RelayCommand(_ =>
        {
            _luaHost?.Stop();
        }, _ => _selectedFlow != null && _selectedFlow.FlowKind == "Script" && ScriptIsRunning);

        ToggleBreakpointCmd = new RelayCommand(p =>
        {
            if (p is int line && line > 0)
            {
                if (Breakpoints.Contains(line)) Breakpoints.Remove(line);
                else Breakpoints.Add(line);
                _luaHost?.SetBreakpoints(Breakpoints);
            }
        }, _ => _selectedFlow != null && _selectedFlow.FlowKind == "Script");

        ClearBreakpointsCmd = new RelayCommand(_ =>
        {
            Breakpoints.Clear();
            _luaHost?.SetBreakpoints(Breakpoints);
        }, _ => _selectedFlow != null && _selectedFlow.FlowKind == "Script");
        CheckSyntaxCmd = new RelayCommand(_ =>
        {
            if (_selectedFlow == null || _selectedFlow.FlowKind != "Script") return;
            var text = _selectedFlow.ScriptContent ?? "";
            var err = NoCodeVision.Scripting.LuaDebugHost.CheckSyntax(text);
            var line = err == null ? "[语法校验] 通过，无语法错误。" : ("[语法校验] 失败：" + err);
            ScriptOutput = ScriptOutput.Length > 8000 ? line + "\n" : ScriptOutput + line + "\n";
        }, _ => _selectedFlow != null && _selectedFlow.FlowKind == "Script");

        CompileTimingCmd = new RelayCommand(_ => CompileTiming(), _ => _selectedFlow != null && _selectedFlow.Steps.Count > 0);
        RunTimingPlanCmd = new RelayCommand(_ => RunTimingPlan(), _ => _selectedFlow != null && _selectedFlow.Steps.Count > 0);
    }

    // 解析时序标记：支持 "T+5ms" / "T+0ms" / "5ms" / "5" / 空(顺延)
    private static double? ParseTimingMarker(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().ToUpperInvariant();
        s = s.Replace("T+", "").Replace("T", "").Replace("MS", "").Replace("毫秒", "").Trim();
        return double.TryParse(s, out var v) ? v : null;
    }

    // 编译：按时序标记换算预期时刻 + 同步组分组 + 编译期冲突检测
    private void CompileTiming()
    {
        if (_selectedFlow == null) return;
        var steps = _selectedFlow.Steps;
        TimingWarnings.Clear();
        double cursor = 0;
        var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<VisionFlowStep>>();
        foreach (var st in steps)
        {
            var parsed = ParseTimingMarker(st.TimingMarker);
            st.ExpectedMs = parsed.HasValue ? parsed.Value : cursor;
            cursor = st.ExpectedMs + System.Math.Max(st.CostMs, 0.1);
            var g = (st.SyncGroup ?? "").Trim();
            if (!string.IsNullOrEmpty(g))
            {
                if (!groups.ContainsKey(g)) groups[g] = new System.Collections.Generic.List<VisionFlowStep>();
                groups[g].Add(st);
            }
        }
        // ① 同步组内动作单周期可行性
        foreach (var kv in groups)
        {
            double tact = kv.Value.Count * 0.2; // 近似：每动作 0.2ms PDO 更新
            if (tact > BusCycleMs)
                TimingWarnings.Add($"⚠ 同步组[{kv.Key}] 含 {kv.Value.Count} 个动作，估算耗时 {tact:F2}ms 超出总线周期 {BusCycleMs:F2}ms，无法在单周期内完成");
        }
        // ② 相对时延分辨率：标记时延须 ≥ 总线周期
        foreach (var st in steps)
        {
            if (st.ExpectedMs > 0 && st.ExpectedMs < BusCycleMs)
                TimingWarnings.Add($"⚠ 步骤[{st.Index} {st.Name}] 相对时延 {st.ExpectedMs:F2}ms 小于总线周期分辨率 {BusCycleMs:F2}ms");
        }
        // ③ 同步组间资源争用（同运动轴 / 同 IO 点位）
        var seenAxis = new System.Collections.Generic.Dictionary<string, string>();
        var seenIo = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var st in steps)
        {
            if (!string.IsNullOrEmpty(st.TargetAxis))
            {
                if (seenAxis.TryGetValue(st.TargetAxis, out var g0) && g0 != st.SyncGroup)
                    TimingWarnings.Add($"⚠ 轴[{st.TargetAxis}] 在同步组[{g0}]与[{st.SyncGroup}]间争用");
                else seenAxis[st.TargetAxis] = st.SyncGroup;
            }
            if (!string.IsNullOrEmpty(st.IoChannel))
            {
                if (seenIo.TryGetValue(st.IoChannel, out var g0) && g0 != st.SyncGroup)
                    TimingWarnings.Add($"⚠ IO[{st.IoChannel}] 在同步组[{g0}]与[{st.SyncGroup}]间争用");
                else seenIo[st.IoChannel] = st.SyncGroup;
            }
        }
        TimingCompiled = true;
        TimingResultText = TimingWarnings.Count == 0
            ? $"✓ 编译通过：{steps.Count} 步，{groups.Count} 个同步组，已生成实时调度表"
            : $"✗ 编译发现 {TimingWarnings.Count} 项时序冲突，请检查";
        foreach (var st in steps) st.TimingStatusText = $"预期 {st.ExpectedMs:F2}ms";
    }

    // 运行：确定性仿真，回填实际时刻与偏差，超阈值报警
    private void RunTimingPlan()
    {
        if (_selectedFlow == null) return;
        if (!TimingCompiled) CompileTiming();
        var steps = _selectedFlow.Steps;
        int i = 0;
        foreach (var st in steps)
        {
            double jitter = (st.CostMs * 0.015) + (i * 0.03); // 可复现的微小抖动
            st.ActualMs = st.ExpectedMs + jitter;
            st.DeviationMs = st.ActualMs - st.ExpectedMs;
            st.TimingAlarm = System.Math.Abs(st.DeviationMs) > TimingThresholdMs;
            st.TimingStatusText = st.TimingAlarm
                ? $"偏差 +{st.DeviationMs:F2}ms ⚠"
                : $"预期 {st.ExpectedMs:F2}ms / 实际 {st.ActualMs:F2}ms / 偏差 {st.DeviationMs:F2}ms";
            i++;
        }
        var alarmCount = 0;
        foreach (var st in steps) if (st.TimingAlarm) alarmCount++;
        TimingResultText = alarmCount == 0
            ? $"✓ 时序运行完成，{steps.Count} 步偏差均在 ±{TimingThresholdMs:F2}ms 内"
            : $"⚠ 时序运行完成，{alarmCount} 步偏差超阈值 ±{TimingThresholdMs:F2}ms";
    }

    private async Task RunAllAsync()
    {
        if (_selectedFlow == null || _selectedFlow.Steps.Count == 0) return;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        IsPaused = false;
        _sharedImage?.Dispose();
        _sharedImage = null;
        CurrentImagePath = "";
        var matcher = new RotatedTemplateMatcher();
        _stepCursor = 0;
        int guard = 0;
        int maxIter = _selectedFlow.Steps.Count * 20 + 50;
        try
        {
            while (!_cts.IsCancellationRequested && _stepCursor >= 0 && _stepCursor < _selectedFlow.Steps.Count && guard++ < maxIter)
            {
                // 暂停：等待恢复或停止
                while (IsPaused && !_cts.IsCancellationRequested)
                    await Task.Delay(120);
                if (_cts.IsCancellationRequested) break;

                var step = _selectedFlow.Steps[_stepCursor];
                SelectedStep = step;                       // 选中并自动跳到该行
                Status = $"运行中 · 第{step.Index}步 {step.Name} · {DateTime.Now:HH:mm:ss}";
                RunStep(step, matcher);                    // 在当前(UI)线程执行本步

                // 让 UI 重绘、滚动到当前行、并响应暂停/停止点击
                await Task.Delay(StepPaceMs);

                _stepCursor = NextStepIndex(_stepCursor);
            }
        }
        finally
        {
            bool cancelled = _cts != null && _cts.IsCancellationRequested;
            _sharedImage?.Dispose();
            _sharedImage = null;
            IsRunning = false;
            IsPaused = false;
            _cts?.Dispose();
            _cts = null;
            Status = cancelled
                ? $"已停止 · {_selectedFlow.Name}"
                : $"完成 · {_selectedFlow.Name}";
        }
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

    private void OnMeasureAnnotationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_selectedStep?.StepType != "Measure") return;
        if (e.Action == NotifyCollectionChangedAction.Reset || _measureAnnotations.Count == 0)
        {
            _selectedStep.ActualValue = "未测量";
            return;
        }
        var last = _measureAnnotations[^1];
        _selectedStep.ActualValue = last.Label;
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
                        _lastMatchResults = results;
                        _lastTemplateMatchStep = step;
                        _matchOverlays.Clear();
                        if (results.Count > 0)
                        {
                            var r0 = results[0];
                            step.ActualValue = $"({r0.CenterX:F1},{r0.CenterY:F1}) θ{r0.Angle:F1} score{r0.Score:F2}";
                            step.StatusText = "匹配成功";
                            foreach (var r in results)
                            {
                                _matchOverlays.Add(new OverlayItem
                                {
                                    X = r.CenterX,
                                    Y = r.CenterY,
                                    W = r.TemplateWidth,
                                    H = r.TemplateHeight,
                                    AngleDeg = -r.Angle,
                                    Color = "#34C759",
                                    Label = $"相似度 {r.Score:F2} · θ{r.Angle:F1}°"
                                });
                            }
                        }
                        else
                        {
                            step.ActualValue = "未匹配";
                            step.StatusText = "匹配失败";
                        }
                        // 确保缺陷检测面板能加载到图（模板匹配源图即缺陷检测输入图）
                        if (!string.IsNullOrWhiteSpace(step.ImageSource) && File.Exists(step.ImageSource))
                            CurrentImagePath = step.ImageSource;
                        // 若当前正停在缺陷检测步骤，自动刷新缺陷显示
                        if (_selectedStep?.StepType == "Defect")
                            RedetectDefects();
                    }
                    if (ownMat && srcMat != null) srcMat.Dispose();
                    break;
                }
                case "Defect":
                {
                    if (_lastMatchResults == null || _lastMatchResults.Count == 0)
                    {
                        step.ActualValue = "无匹配结果";
                        step.StatusText = "错误";
                    }
                    else
                    {
                        matcher.DefectOptions = new DefectOptions
                        {
                            DiffThreshold = step.DiffThreshold,
                            MinAreaFrac = step.MinAreaFrac,
                            GlobalBrightnessThresh = step.GlobalBrightnessThresh,
                            EdgeTolerance = step.EdgeTolerance,
                            EdgeGradThresh = step.EdgeGradThresh,
                            ErodeSize = step.ErodeSize,
                            DilateSize = step.DilateSize,
                        };
                        var defects = matcher.DetectDefects(_lastMatchResults);
                        if (defects.Count > 0)
                        {
                            step.ActualValue = $"缺陷 {defects.Count} 处";
                            step.StatusText = "缺陷异常";
                        }
                        else
                        {
                            step.ActualValue = "无缺陷";
                            step.StatusText = "缺陷通过";
                        }
                        if (ReferenceEquals(step, _selectedStep))
                        {
                            FlowDefectResults.Clear();
                            foreach (var d in defects) FlowDefectResults.Add(d);
                            DefectSummaryText = defects.Count == 0 ? "未发现缺陷" : $"发现 {defects.Count} 处缺陷";
                            DefectOverlayImage = BuildDefectOverlay(defects);
                        }
                    }
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
            // ===== 工程师调试：轴 / IO / 气缸 =====
            var _mcm = MotionControlViewModel.Instance;
            DebugAxes = _mcm?.Axes ?? new ObservableCollection<MotionRow>();
            DebugIo = _mcm?.IoPoints ?? new ObservableCollection<MotionRow>();
            DebugCylinders = _mcm?.Cylinders ?? new ObservableCollection<MotionRow>();

            EnableAxisCmd = new RelayCommand(p => ToggleEnableAxis((MotionRow)p!));
            HomeAxisCmd = new RelayCommand(p => HomeAxis((MotionRow)p!));
            JogPlusCmd = new RelayCommand(p => MoveAxis((MotionRow)p!, +1, true));
            JogMinusCmd = new RelayCommand(p => MoveAxis((MotionRow)p!, -1, true));
            InchPlusCmd = new RelayCommand(p => MoveAxis((MotionRow)p!, +1, false));
            InchMinusCmd = new RelayCommand(p => MoveAxis((MotionRow)p!, -1, false));
            ToggleOutputCmd = new RelayCommand(p => ToggleOutput((MotionRow)p!));
            ToggleCylinderCmd = new RelayCommand(p => ToggleCylinder((MotionRow)p!));
        }

        // ===== 工程师调试面板：轴 / IO / 气缸（真实驱动硬件，HardwareManager.Instance.Motion）=====
        private readonly HardwareManager _hw = HardwareManager.Instance;

        /// <summary>轴列表（与运控页同一份数据，可就地使能/回原/寸动/JOG）。</summary>
        public ObservableCollection<MotionRow> DebugAxes { get; }
        /// <summary>IO 列表（输入只读、输出可开关）。</summary>
        public ObservableCollection<MotionRow> DebugIo { get; }
        /// <summary>气缸列表（伸出/缩回切换）。</summary>
        public ObservableCollection<MotionRow> DebugCylinders { get; }

        private double _inchStep = 1.0;
        /// <summary>寸动步长（每次寸动移动的单位）。</summary>
        public double InchStep { get => _inchStep; set => SetField(ref _inchStep, value); }
        private double _jogStep = 10.0;
        /// <summary>JOG 步长（每次 JOG 移动的单位，通常大于寸动）。</summary>
        public double JogStep { get => _jogStep; set => SetField(ref _jogStep, value); }

        public ICommand EnableAxisCmd { get; }
        public ICommand HomeAxisCmd { get; }
        public ICommand JogPlusCmd { get; }
        public ICommand JogMinusCmd { get; }
        public ICommand InchPlusCmd { get; }
        public ICommand InchMinusCmd { get; }
        public ICommand ToggleOutputCmd { get; }
        public ICommand ToggleCylinderCmd { get; }

        // ===== 工程师调试命令实现 =====
        private void ToggleEnableAxis(MotionRow row)
        {
            row.Enabled = !row.Enabled;
            row.Status = row.Enabled ? "使能" : "禁用";
            _hw.Motion.EnableAxis(row.Name, row.Enabled);
        }

        private void HomeAxis(MotionRow row) => row.Value = 0;

        private void MoveAxis(MotionRow row, int dir, bool jog)
        {
            double step = jog ? JogStep : InchStep;
            _hw.Motion.Jog(row.Name, dir * step);
            row.Value += dir * step;
        }

        private void ToggleOutput(MotionRow row)
        {
            bool on = row.Status != "ON";
            row.Status = on ? "ON" : "OFF";
            _hw.Motion.SetIo(row.Name, on);
        }

        private void ToggleCylinder(MotionRow row)
        {
            bool extended = row.Status == "伸出";
            row.Status = extended ? "缩回" : "伸出";
            row.Action = extended ? "伸出" : "缩回";
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
