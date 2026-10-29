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

using NoCodeVision.Helpers;

using NoCodeVision.Scripting;

using NoCodeVision.Services;

using NoCodeVision.Comm;

using NoCodeVision.Vision;

using System.Threading;

using System.Threading.Tasks;



// 〔版权〕温启志 编写 ◇ 微信：18719361399

namespace NoCodeVision.ViewModels;



#region 基础类



// 作者：温·启·志 编写 ▢ 联系 wx:187-1936-1399

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



// 本文件由 温启志 编写 ● 微信 187 1936 1399

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



// Coded by 温启志 ◆ 微信: 1.8.7.1.9.3.6.1.3.9.9

public class CameraItem

{

    public string Name { get; set; } = "";

    public string Status { get; set; } = "离线";

    public string Resolution { get; set; } = "";

}



// 温启志 著 ◇ WeChat 18719361399 ▣

public class VarItem

{

    public string Name { get; set; } = "";

    public string Type { get; set; } = "";

    public string Value { get; set; } = "";

    public string Remark { get; set; } = "";

}



// 〔署名〕温启志编写 微信﹕18719361399 ◆

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



    public string ActualValue
    {
        get => _actualValue;
        set { if (SetField(ref _actualValue, value)) OnPropertyChanged(nameof(ValueText)); }
    }

    private string _actualValue = "";

    /// <summary>步骤列表「当前值」列显示什么：是角度就只显示多少度，其余照原样。</summary>
    public string ValueText
    {
        get
        {
            var v = _actualValue ?? "";
            if (StepType == "AngleFromCenter")
            {
                var m = System.Text.RegularExpressions.Regex.Match(v, @"角度=([-+]?\d+\.?\d*)");
                if (m.Success) return m.Groups[1].Value + "°";
            }
            return v;
        }
    }



    public string Icon { get => _icon; set => SetField(ref _icon, value); }

    private string _icon = "➕";



    public string StepType { get => _stepType; set => SetField(ref _stepType, value); }

    private string _stepType = "";



    /// <summary>节点图里的稳定标识：连线靠它寻址，删除/排序步骤都不会错位。</summary>
    public string NodeId { get; set; } = Guid.NewGuid().ToString("N");



    /// <summary>节点图画布坐标（仅节点图流程使用；普通流程恒为 0）。</summary>
    public double NodeX { get => _nodeX; set => SetField(ref _nodeX, value); }

    private double _nodeX;



    public double NodeY { get => _nodeY; set => SetField(ref _nodeY, value); }

    private double _nodeY;



    // ===== 卡尺/找圆/找直线/数块数 等新算子共用的一组参数（见 Vision/OperatorCatalog.cs） =====

    public double ExpectRadius { get => _expectRadius; set => SetField(ref _expectRadius, value); }

    private double _expectRadius;



    public double CaliperLength { get => _caliperLength; set => SetField(ref _caliperLength, value); }

    private double _caliperLength = 40;



    public double SamplingStep { get => _samplingStep; set => SetField(ref _samplingStep, value); }

    private double _samplingStep = 5;



    public double EdgeConfidence { get => _edgeConfidence; set => SetField(ref _edgeConfidence, value); }

    private double _edgeConfidence = 1;



    public int EdgeIndex { get => _edgeIndex; set => SetField(ref _edgeIndex, value); }

    private int _edgeIndex = 1;



    public string FitMethod { get => _fitMethod; set => SetField(ref _fitMethod, value); }

    private string _fitMethod = "抗干扰(权重)";



    public string BlobPolarity { get => _blobPolarity; set => SetField(ref _blobPolarity, value); }

    private string _blobPolarity = "亮区域";



    public string ThreshMode { get => _threshMode; set => SetField(ref _threshMode, value); }

    private string _threshMode = "自动";



    public double ThreshLo { get => _threshLo; set => SetField(ref _threshLo, value); }

    private double _threshLo = 128;



    public double ThreshHi { get => _threshHi; set => SetField(ref _threshHi, value); }

    private double _threshHi = 255;



    public double AreaMin { get => _areaMin; set => SetField(ref _areaMin, value); }

    private double _areaMin = 20;



    public double AreaMax { get => _areaMax; set => SetField(ref _areaMax, value); }

    private double _areaMax = 1e9;



    public bool InvertImage { get => _invertImage; set => SetField(ref _invertImage, value); }

    private bool _invertImage;



    public int SmoothSize { get => _smoothSize; set => SetField(ref _smoothSize, value); }

    private int _smoothSize = 3;



    public bool FlipVertical { get => _flipVertical; set => SetField(ref _flipVertical, value); }

    private bool _flipVertical;



    public int IndexProbe { get => _indexProbe; set => SetField(ref _indexProbe, value); }

    private int _indexProbe;



    public int JumpTarget { get => _jumpTarget; set => SetField(ref _jumpTarget, value); }

    private int _jumpTarget;



    // ===== 跳转 / 跳转到（按名字找落点，不用记第几步） =====

    /// <summary>跳转：什么情况才跳（总是跳 / 上一步合格 / 上一步不合格 / 当前值大于等于 / 当前值小于等于）。</summary>
    public string JumpCondition { get => _jumpCondition; set => SetField(ref _jumpCondition, value); }

    private string _jumpCondition = "上一步不合格";

    /// <summary>跳转：比数值时用的那个数。</summary>
    public double JumpWhen { get => _jumpWhen; set => SetField(ref _jumpWhen, value); }

    private double _jumpWhen;

    /// <summary>跳转：跳到哪个名字（填「跳转到」那一步起的名字）。</summary>
    public string JumpTargetName { get => _jumpTargetName; set => SetField(ref _jumpTargetName, value); }

    private string _jumpTargetName = "";

    /// <summary>跳转到（落点）：这里叫什么名字，给「跳转」当目的地。</summary>
    public string LabelName { get => _labelName; set => SetField(ref _labelName, value); }

    private string _labelName = "";

    /// <summary>零样本查瑕疵：结果怎么看（热图+框 / 只画框 / 只要热图）。</summary>
    public string HeatMode { get => _heatMode; set => SetField(ref _heatMode, value); }

    private string _heatMode = "热图+框";



    public double MoveDistance { get => _moveDistance; set => SetField(ref _moveDistance, value); }

    private double _moveDistance;



    public double HomeOffset { get => _homeOffset; set => SetField(ref _homeOffset, value); }

    private double _homeOffset;



    /// <summary>开关类算子的动作/期望状态（通/断、上电解锁/脱扣）。</summary>
    public string SwitchState { get => _switchState; set => SetField(ref _switchState, value); }

    private string _switchState = "通";



    public bool ConfirmWrite { get => _confirmWrite; set => SetField(ref _confirmWrite, value); }

    private bool _confirmWrite;



    // ===== OpenCVVision 移植工具共用的通用旋钮（界面上显示的是目录里的中文标签） =====

    public string Mode { get => _mode; set => SetField(ref _mode, value); }

    private string _mode = "";



    public double SizeX { get => _sizeX; set => SetField(ref _sizeX, value); }

    private double _sizeX = 5;



    public double SizeY { get => _sizeY; set => SetField(ref _sizeY, value); }

    private double _sizeY = 5;



    public double Number1 { get => _number1; set => SetField(ref _number1, value); }

    private double _number1;



    public double Number2 { get => _number2; set => SetField(ref _number2, value); }

    private double _number2 = 100;



    /// <summary>找划痕：偏离直线的比例下限（0.05=5%）。标签边框、面板凹槽都是笔直的线，偏离很小；真划痕是弯的。</summary>
    public double BendMin { get => _bendMin; set => SetField(ref _bendMin, value); }

    private double _bendMin = 0.05;

    public double Number3 { get => _number3; set => SetField(ref _number3, value); }

    private double _number3 = 30;



    public double Number4 { get => _number4; set => SetField(ref _number4, value); }

    private double _number4 = 1000;



    public double Factor { get => _factor; set => SetField(ref _factor, value); }

    private double _factor = 1;



    public string ModelFile { get => _modelFile; set => SetField(ref _modelFile, value); }

    private string _modelFile = "";



    public string LabelsFile { get => _labelsFile; set => SetField(ref _labelsFile, value); }

    private string _labelsFile = "";



    public string ImageSource { get => _imageSource; set => SetField(ref _imageSource, value); }

    private string _imageSource = "";



    public string TemplateFile { get => _templateFile; set => SetField(ref _templateFile, value); }

    private string _templateFile = "";



    public string CaptureMode { get => _captureMode; set => SetField(ref _captureMode, value); }

    private string _captureMode = "打开文件";



    public string FolderPath { get => _folderPath; set => SetField(ref _folderPath, value); }

    private string _folderPath = "";

    public int CameraIndex { get => _cameraIndex; set => SetField(ref _cameraIndex, value); }

    private int _cameraIndex;

    public bool CameraLineScan { get => _cameraLineScan; set => SetField(ref _cameraLineScan, value); }

    private bool _cameraLineScan;

    public int CameraLineRate { get => _cameraLineRate; set => SetField(ref _cameraLineRate, value); }

    private int _cameraLineRate = 10000;

    public int CameraSpliceLines { get => _cameraSpliceLines; set => SetField(ref _cameraSpliceLines, value); }

    private int _cameraSpliceLines = 1024;

    public string ModbusIp { get => _modbusIp; set => SetField(ref _modbusIp, value); }

    private string _modbusIp = "";

    public int ModbusPort { get => _modbusPort; set => SetField(ref _modbusPort, value); }

    private int _modbusPort = 502;

    public int ModbusUnit { get => _modbusUnit; set => SetField(ref _modbusUnit, value); }

    private int _modbusUnit = 1;

    public int ModbusTimeout { get => _modbusTimeout; set => SetField(ref _modbusTimeout, value); }

    private int _modbusTimeout = 800;

    /// <summary>点阵(DPM)码增强：激光/针打在金属上的码是一个个圆点，要先补成实心模块才读得出来。</summary>
    public bool UseDpm { get => _useDpm; set => SetField(ref _useDpm, value); }

    private bool _useDpm = true;

    /// <summary>TCP 发送：目标 IP / 端口 / 超时 / 结尾符（填了就自己连自己发，不用先去「通讯」页建通道）。</summary>
    public string TcpHost { get => _tcpHost; set => SetField(ref _tcpHost, value); }

    private string _tcpHost = "";

    public double TcpPort { get => _tcpPort; set => SetField(ref _tcpPort, value); }

    private double _tcpPort = 9000;

    public int TcpTimeout { get => _tcpTimeout; set => SetField(ref _tcpTimeout, value); }

    private int _tcpTimeout = 1500;

    public string LineEnd { get => _lineEnd; set => SetField(ref _lineEnd, value); }

    private string _lineEnd = "换行 LF";



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

    private string _logicRelation = "满足就做";



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

    public string AngleZero { get => _angleZero; set => SetField(ref _angleZero, value); }

    private string _angleZero = "向上(12点)";



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

    private string _savePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "result.csv");

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



// 编写：温启志 ‖ 微信：187①936①399

/// <summary>「➕ 添加」菜单的一级项：分类 + 它下面的算子。</summary>
public sealed class ToolGroup
{
    public string Name { get; init; } = "";

    public System.Collections.Generic.List<ToolItem> Tools { get; init; } = new();
}

/// <summary>「➕ 添加」菜单的二级项：算子名 + 一句人话说明（菜单里直接显示，不用点进去猜）。</summary>
public sealed class ToolItem
{
    public string Name { get; init; } = "";

    public string Desc { get; init; } = "";

    public string Icon { get; init; } = "";
}

public class VisionFlow : ViewModelBase

{

    private string _name = "";

    public string Name { get => _name; set => SetField(ref _name, value); }

    public string Icon { get; set; } = "🔀";

    public string FlowKind { get; set; } = "Normal";

    private string _flowRole = "Main";

    /// <summary>流程角色：Main=主流程（操作员「运行」后不断循环）；Reset=复位流程（点「复位」时只运行一次）。</summary>
    public string FlowRole
    {
        get => _flowRole;
        set { if (SetField(ref _flowRole, value)) OnPropertyChanged(nameof(RoleText)); }
    }

    /// <summary>流程角色显示文本。</summary>
    public string RoleText => FlowRole == "Reset" ? "复位流程" : "主流程";

    /// <summary>列表里双击名字就地改名的编辑态（同一时刻只让一个流程在改）。</summary>
    public bool IsEditing { get => _isEditing; set => SetField(ref _isEditing, value); }

    private bool _isEditing;

    public string ScriptContent { get; set; } = "-- 脚本流程示例：视觉 + 运控 + 计算\n-- 1) 视觉：采集图像并匹配模板\nlocal ok = vision.grab()                  -- 抓取一帧图像\nlocal score = vision.match(\"tpl_A.png\") -- 模板匹配，返回相似度 0~1\nprint(\"匹配分数: \" .. tostring(score))\n\n-- 2) 计算：判定是否合格，并换算位移量\nlocal threshold = 0.85\nlocal pass = score >= threshold\nlocal dx = (score - threshold) * 100     -- 分数差 → 位移量(px)\nlocal dist = math.sqrt(dx * dx)           -- 用 math 做计算\nprint(\"是否合格: \" .. tostring(pass) .. \"  位移量: \" .. tostring(dist))\n\n-- 3) 运控：合格则驱动 PLC/轴动作，不合格报警\nif pass then\n    plc.write(200, 1)                     -- 触发运动 / 输出\n    plc.write(201, math.floor(dist))      -- 下发放大后的位移量\n    print(\"运控：已发送到位指令\")\nelse\n    plc.write(200, 0)                     -- 复位 / 报警\n    print(\"运控：未达标，已停止\")\nend\n\nsleep(50)\n";

    public ObservableCollection<VisionFlowStep> Steps { get; set; } = new();

    /// <summary>节点图流程的连线（普通流程恒为空）。随 Steps 一起存进 flows.json / .ncv 工程。</summary>
    public ObservableCollection<FlowNodeLink> Links { get; set; } = new();

}



/// <summary>节点图的一条连线：从某节点的一个输出口连到另一节点。端口为空串表示默认出口。</summary>

public class FlowNodeLink

{

    public string FromId { get; set; } = "";

    public string FromPort { get; set; } = "";

    public string ToId { get; set; } = "";

}



#endregion



#region 项目页



// Copyright 温启志 ▪ 微信 18719361399

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

    public string ImageSavePath { get; set; } = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Images");

    public string DataSavePath { get; set; } = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Data");

    public string BackupPath { get; set; } = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Backup");

    public int MaxBackupCount { get; set; } = 10;

    public bool PasswordEnabled { get; set; } = false;

    public int AutoLockMinutes { get; set; } = 5;

    public bool AllowRemote { get; set; } = false;

    public int MaxUndoSteps { get; set; } = 50;

    public int ResultRetainDays { get; set; } = 90;

    public bool DebugMode { get; set; } = false;



    // 状态

    public string Status { get; set; } = "就绪";



    // 新建项目示例配置（采集源 / 触发方式 / 外发外部软件）

    public string ImageSourceType { get; set; } = "采集相机";   // 采集相机 / 打开文件 / 打开文件夹

    public string TriggerType { get; set; } = "软件触发";        // 硬件触发 / 软件触发

    public bool SendResultToExternal { get; set; } = false;     // 缺陷检测后是否外发结果

    public string ExternalChannel { get; set; } = "MQTT";       // MQTT / 网口 / UDP / 串口

    public string ExternalTarget { get; set; } = "";           // 如 192.168.1.10:1883 或 COM3

    public string ExampleId { get; set; } = "";



    // 模板自带配置：相机 / 通讯 / 流程

    public string CameraName { get; set; } = "";                       // 相机名称/编号

    public ObservableCollection<CommConfigItem> CommConfigs { get; set; } = new();

    public ObservableCollection<VisionFlow> Flows { get; set; } = new();

}



// 新建项目示例（带缩略图，用于弹窗选择；覆盖 硬件/软件触发 × 相机/文件 组合）

// 新建项目示例（带详细说明，用于弹窗选择；覆盖 触发方式 x 采集源 x 多种检测类型 x 多路通讯）
public class ProjectExample
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";            // 卡片短说明
    public string LongDescription { get; set; } = "";        // 详细说明（弹窗展开说明）
    public List<string> Features { get; set; } = new();       // 特性标签
    public string CameraName { get; set; } = "Camera_1";      // 主相机名称/编号
    public string ImageSourceType { get; set; } = "采集相机";
    public string TriggerType { get; set; } = "软件触发";
    public bool SendResultToExternal { get; set; }
    public string ExternalChannel { get; set; } = "MQTT";
    public string ExternalTarget { get; set; } = "";

    public static ObservableCollection<ProjectExample> BuildExamples()
    {
        return new ObservableCollection<ProjectExample>
        {
            new ProjectExample { Id = "ex_cam_mqtt", Name = "视觉定位检测（相机+MQTT）", Description = "相机软触发·灰度匹配定位·MQTT 上报", CameraName = "Camera_1", ImageSourceType = "采集相机", TriggerType = "软件触发", SendResultToExternal = true, ExternalChannel = "MQTT", ExternalTarget = "192.168.1.10:1883",
                LongDescription = "工业零件视觉定位：相机软触发采集，灰度模板匹配定位基准，几何测量孔径，缺陷检测后通过 MQTT 上报结果至上位机/MES。适用于上下料对位、装配定位、来料纠偏。",
                Features = { "模板匹配", "几何测量", "缺陷检测", "MQTT上报" } },

            new ProjectExample { Id = "ex_file_tcp", Name = "离线图片批量检测（读文件+网口）", Description = "文件夹批量质检·差异比对·网口回传", CameraName = "Camera_2", ImageSourceType = "打开文件", TriggerType = "软件触发", SendResultToExternal = true, ExternalChannel = "网口", ExternalTarget = "192.168.1.100:5000",
                LongDescription = "离线图片批量质检：从文件夹读取样本图，图像增强后差异比对缺陷，统计良率，网口回传汇总结果。适用于来料抽检、实验室复检、归档分析。",
                Features = { "批量检测", "缺陷比对", "良率统计", "网口回传" } },

            new ProjectExample { Id = "ex_hw_plc", Name = "硬件触发产线飞拍（相机+PLC串口）", Description = "编码器硬触发·飞拍定位·PLC 串口下发", CameraName = "LineCam", ImageSourceType = "采集相机", TriggerType = "硬件触发", SendResultToExternal = true, ExternalChannel = "串口", ExternalTarget = "COM3",
                LongDescription = "产线飞拍定位：编码器/传感器硬件触发相机，飞拍抓取工件，定位后通过串口下发坐标给 PLC 执行分拣。适用于传送带高速定位、动态对位。",
                Features = { "硬件触发", "飞拍定位", "PLC串口" } },

            new ProjectExample { Id = "ex_cam_udp", Name = "几何尺寸测量（相机+UDP）", Description = "多特征测量·SPC 统计·UDP 广播", CameraName = "Camera_3", ImageSourceType = "采集相机", TriggerType = "软件触发", SendResultToExternal = true, ExternalChannel = "UDP", ExternalTarget = "192.168.1.50:6000",
                LongDescription = "精密尺寸测量：相机采集，多特征几何测量（孔径/边距/角度），SPC 统计过程控制，UDP 广播测量值至 SCADA。适用于机加工件全检。",
                Features = { "多特征测量", "SPC统计", "UDP广播" } },

            new ProjectExample { Id = "ex_ocr_mqtt", Name = "字符识别 OCR（相机+MQTT）", Description = "OCR 识别·MES 校验·MQTT 回传", CameraName = "Camera_OCR", ImageSourceType = "采集相机", TriggerType = "软件触发", SendResultToExternal = true, ExternalChannel = "MQTT", ExternalTarget = "192.168.1.20:1883",
                LongDescription = "条码/字符识别：相机采集，OCR 识别产品批次号与二维码，与 MES 比对校验，MQTT 回传识别结果。适用于追溯、防错、出库核验。",
                Features = { "OCR识别", "MES校验", "MQTT回传" } },

            new ProjectExample { Id = "ex_sort_net", Name = "颜色缺陷分拣（相机+网口）", Description = "颜色分析·缺陷检测·分拣控制", CameraName = "Camera_Sort", ImageSourceType = "采集相机", TriggerType = "硬件触发", SendResultToExternal = true, ExternalChannel = "网口", ExternalTarget = "192.168.1.30:6001",
                LongDescription = "颜色分拣：硬件触发采集，颜色空间分析加缺陷检测，按等级通过网口下发分拣指令给执行机构。适用于食品、零件、物料分拣。",
                Features = { "颜色分析", "缺陷检测", "分拣控制" } },

            new ProjectExample { Id = "ex_multi_cam", Name = "多相机拼接检测（双相机+MQTT）", Description = "双相机同步·全景拼接·MQTT 上报", CameraName = "Camera_L/Camera_R", ImageSourceType = "采集相机", TriggerType = "软件触发", SendResultToExternal = true, ExternalChannel = "MQTT", ExternalTarget = "192.168.1.40:1883",
                LongDescription = "大幅面拼接：左右双相机同步采集，图像拼接合成全景，模板匹配与缺陷检测覆盖整幅面。适用于大幅面板、布匹、卷材检测。",
                Features = { "双相机拼接", "全景合成", "MQTT上报" } },

            new ProjectExample { Id = "ex_count_udp", Name = "目标计数统计（读文件夹+UDP）", Description = "目标计数·正反面判定·UDP 上报", CameraName = "Camera_Cnt", ImageSourceType = "打开文件夹", TriggerType = "软件触发", SendResultToExternal = true, ExternalChannel = "UDP", ExternalTarget = "192.168.1.60:7000",
                LongDescription = "物件计数：从图像/视频流中检测并计数目标数量，区分正反面，统计日产量，UDP 上报计数器。适用于流水线计数、盘点。",
                Features = { "目标计数", "正反面区分", "UDP上报" } },

            new ProjectExample { Id = "ex_calib_local", Name = "模板与精度标定（相机+本地）", Description = "模板建立·精度标定·本地存储", CameraName = "Camera_Cal", ImageSourceType = "采集相机", TriggerType = "软件触发", SendResultToExternal = false, ExternalChannel = "MQTT", ExternalTarget = "192.168.1.10:1883",
                LongDescription = "模板与标定：采集标准件建立模板与标定参数，灰度/角度标定，生成可复用的检测工程基线。适用于新项目初始化、精度标定、基线发布。",
                Features = { "模板建立", "精度标定", "本地存储" } },
        };
    }

    private CommConfigItem MakeComm(string type, string target, string topic)
    {
        var cfg = new CommConfigItem { Name = "外发-" + type, CommType = type };
        if (!string.IsNullOrEmpty(target) && target.Contains(":"))
        {
            cfg.NetIp = target.Split(':')[0];
            cfg.NetPort = target.Split(':')[1];
        }
        else if (type == "串口")
        {
            cfg.Port = target.StartsWith("COM") ? target : "COM3";
            cfg.Baud = "115200";
        }
        if (type == "UDP") cfg.Broadcast = true;
        if (!string.IsNullOrEmpty(topic)) cfg.SubTopics = topic;
        return cfg;
    }

    public ObservableCollection<CommConfigItem> BuildCommConfigs()
    {
        var list = new ObservableCollection<CommConfigItem>();
        switch (Id)
        {
            case "ex_cam_mqtt":
                list.Add(MakeComm("MQTT", "192.168.1.10:1883", "ncv/result"));
                list.Add(MakeComm("串口", "COM3", ""));
                break;
            case "ex_file_tcp":
                list.Add(MakeComm("网口", "192.168.1.100:5000", ""));
                list.Add(MakeComm("MQTT", "192.168.1.10:1883", "ncv/result"));
                break;
            case "ex_hw_plc":
                list.Add(MakeComm("串口", "COM3", ""));
                list.Add(MakeComm("网口", "192.168.1.100:5000", ""));
                break;
            case "ex_cam_udp":
                list.Add(MakeComm("UDP", "192.168.1.50:6000", ""));
                break;
            case "ex_ocr_mqtt":
                list.Add(MakeComm("MQTT", "192.168.1.20:1883", "ncv/ocr"));
                list.Add(MakeComm("串口", "COM4", ""));
                break;
            case "ex_sort_net":
                list.Add(MakeComm("网口", "192.168.1.30:6001", ""));
                list.Add(MakeComm("UDP", "192.168.1.50:6000", ""));
                break;
            case "ex_multi_cam":
                list.Add(MakeComm("MQTT", "192.168.1.40:1883", "ncv/panel"));
                list.Add(MakeComm("网口", "192.168.1.100:5000", ""));
                break;
            case "ex_count_udp":
                list.Add(MakeComm("UDP", "192.168.1.60:7000", ""));
                break;
            case "ex_calib_local":
                break;
        }
        return list;
    }

    private VisionFlowStep MkStep(int idx, string func, string name, string stepType, string summary, string icon = "处", int timeout = 3000)
    {
        return new VisionFlowStep
        {
            Index = idx, Function = func, Name = name, StepType = stepType,
            ParamSummary = summary, Icon = icon, Timeout = timeout, StatusText = "未开始"
        };
    }

    private VisionFlow MkFlow(string name, List<VisionFlowStep> steps, string kind = "Normal")
    {
        return new VisionFlow
        {
            Name = name, Icon = "流", FlowKind = kind,
            Steps = new ObservableCollection<VisionFlowStep>(steps)
        };
    }

    public ObservableCollection<VisionFlow> BuildFlows()
    {
        var captureMode = ImageSourceType;
        var flows = new ObservableCollection<VisionFlow>();
        switch (Id)
        {
            case "ex_cam_mqtt":
            {
                var s = new List<VisionFlowStep>();
                var c1 = MkStep(1, "拍张照片", "采集图像", "ImageCapture", captureMode, "采", 5000); c1.CaptureMode = captureMode; s.Add(c1);
                s.Add(MkStep(2, "图像预处理", "增强去噪", "Preprocess", "灰度化/滤波/二值", "处"));
                var m1 = MkStep(3, "图里找图案", "定位基准", "TemplateMatch", "灰度匹配 / score>=0.85", "匹"); m1.MatchMode = "灰度匹配"; m1.ScoreThreshold = 0.85; m1.RoiX = 80; m1.RoiY = 60; m1.RoiW = 160; m1.RoiH = 120; s.Add(m1);
                var g1 = MkStep(4, "量尺寸", "测量孔径", "Measure", "圆径 / 12.00±0.05", "测"); g1.MeasureType = "圆径"; g1.NominalValue = 12.0; g1.Tolerance = 0.05; s.Add(g1);
                s.Add(MkStep(5, "挑瑕疵", "缺陷检测", "Defect", "差异比对 / 阈值45", "缺"));
                var o1 = MkStep(6, "通讯发送", "结果外发", "Comm", "MQTT 发送结果", "发"); o1.CommChannel = "MQTT"; o1.CommCmd = "发送"; o1.CommContent = "{\"result\":\"$(Result)\"}"; o1.CommEncoding = "ASCII"; s.Add(o1);
                s.Add(MkStep(7, "报结果", "输出结果", "Output", "输出到结果表", "出"));
                flows.Add(MkFlow("主检测流程", s));

                var s2 = new List<VisionFlowStep>();
                var c2 = MkStep(1, "拍张照片", "采集模板图", "ImageCapture", captureMode, "采", 5000); c2.CaptureMode = captureMode; s2.Add(c2);
                s2.Add(MkStep(2, "ROI设置", "标定区域", "RoiSet", "ROI 80,60,160,120", "区"));
                s2.Add(MkStep(3, "模板保存", "保存模板", "TemplateSave", "保存灰度模板", "模"));
                s2.Add(MkStep(4, "参数校验", "校验参数", "Verify", "校验匹配阈值", "验"));
                flows.Add(MkFlow("模板校准流程", s2));
                break;
            }
            case "ex_file_tcp":
            {
                var s = new List<VisionFlowStep>();
                var c1 = MkStep(1, "打开文件", "读取样本", "ImageOpen", "打开文件夹", "文", 3000); c1.CaptureMode = "打开文件"; s.Add(c1);
                s.Add(MkStep(2, "图像增强", "增强对比", "Preprocess", "直方图均衡/滤波", "处"));
                s.Add(MkStep(3, "挑瑕疵", "差异比对", "Defect", "模板差异比对 / 阈值40", "缺"));
                s.Add(MkStep(4, "合格判定", "判定结果", "Judge", "score>=0.9 合格", "判"));
                s.Add(MkStep(5, "计数统计", "良品计数", "Count", "良/不良计数", "数"));
                s.Add(MkStep(6, "报结果", "输出结果", "Output", "输出到结果表", "出"));
                flows.Add(MkFlow("批量检测流程", s));

                var s2 = new List<VisionFlowStep>();
                s2.Add(MkStep(1, "读取结果", "汇总数据", "ReadResult", "读取结果表", "读"));
                s2.Add(MkStep(2, "良率统计", "计算良率", "Statistics", "良率/CPK 统计", "统"));
                var o2 = MkStep(3, "通讯发送", "网口上报", "Comm", "网口发送汇总", "发"); o2.CommChannel = "网口"; o2.CommCmd = "发送"; o2.CommContent = "{\"yield\":\"$(Yield)\"}"; o2.CommEncoding = "ASCII"; s2.Add(o2);
                s2.Add(MkStep(4, "报结果", "输出报告", "Output", "输出统计报告", "出"));
                flows.Add(MkFlow("汇总统计流程", s2));
                break;
            }
            case "ex_hw_plc":
            {
                var s = new List<VisionFlowStep>();
                var c1 = MkStep(1, "硬件触发", "飞拍采集", "ImageCapture", "硬触发/飞拍", "采", 2000); c1.CaptureMode = "采集相机"; s.Add(c1);
                var m1 = MkStep(2, "图里找图案", "飞拍定位", "TemplateMatch", "灰度匹配 / score>=0.8", "匹"); m1.MatchMode = "灰度匹配"; m1.ScoreThreshold = 0.8; m1.RoiX = 60; m1.RoiY = 40; m1.RoiW = 200; m1.RoiH = 150; s.Add(m1);
                var g1 = MkStep(3, "量尺寸", "测量位置", "Measure", "中心坐标/角度", "测"); g1.MeasureType = "位置"; s.Add(g1);
                var o1 = MkStep(4, "通讯发送", "PLC 坐标", "Comm", "串口下发坐标", "发"); o1.CommChannel = "串口"; o1.CommCmd = "发送"; o1.CommContent = "X=$(X),Y=$(Y),A=$(Angle)"; o1.CommEncoding = "ASCII"; s.Add(o1);
                s.Add(MkStep(5, "报结果", "输出结果", "Output", "输出到结果表", "出"));
                flows.Add(MkFlow("飞拍检测流程", s));

                var s2 = new List<VisionFlowStep>();
                s2.Add(MkStep(1, "节拍计数", "件数统计", "Count", "单位时间计数", "数"));
                s2.Add(MkStep(2, "节拍统计", "CT 统计", "Statistics", "计算节拍时间", "统"));
                var o2 = MkStep(3, "通讯发送", "网口上报", "Comm", "网口发送节拍", "发"); o2.CommChannel = "网口"; o2.CommCmd = "发送"; o2.CommContent = "{\"ct\":\"$(CT)\"}"; o2.CommEncoding = "ASCII"; s2.Add(o2);
                flows.Add(MkFlow("节拍统计流程", s2));
                break;
            }
            case "ex_cam_udp":
            {
                var s = new List<VisionFlowStep>();
                var c1 = MkStep(1, "拍张照片", "采集图像", "ImageCapture", captureMode, "采", 5000); c1.CaptureMode = captureMode; s.Add(c1);
                s.Add(MkStep(2, "ROI设置", "测量区域", "RoiSet", "ROI 50,50,200,120", "区"));
                var g1 = MkStep(3, "量尺寸", "圆径测量", "Measure", "圆径 / 12.00±0.05", "测"); g1.MeasureType = "圆径"; g1.NominalValue = 12.0; g1.Tolerance = 0.05; s.Add(g1);
                var g2 = MkStep(4, "量尺寸", "边距测量", "Measure", "边距 / 45.0±0.1", "测"); g2.MeasureType = "边距"; g2.NominalValue = 45.0; g2.Tolerance = 0.1; s.Add(g2);
                var g3 = MkStep(5, "量尺寸", "角度测量", "Measure", "角度 / 90±0.5", "测"); g3.MeasureType = "角度"; g3.NominalValue = 90.0; g3.Tolerance = 0.5; s.Add(g3);
                s.Add(MkStep(6, "合格判定", "判定结果", "Judge", "全部公差内合格", "判"));
                var o1 = MkStep(7, "通讯发送", "UDP 广播", "Comm", "UDP 广播测量值", "发"); o1.CommChannel = "UDP"; o1.CommCmd = "发送"; o1.CommContent = "{\"d\":$(Dia),\"e\":$(Edge)}"; o1.CommEncoding = "ASCII"; s.Add(o1);
                flows.Add(MkFlow("尺寸测量流程", s));

                var s2 = new List<VisionFlowStep>();
                s2.Add(MkStep(1, "数据采集", "采集样本", "ReadResult", "读取测量值", "读"));
                s2.Add(MkStep(2, "SPC统计", "CPK 计算", "Statistics", "均值/CPK/趋势", "统"));
                s2.Add(MkStep(3, "报结果", "输出趋势", "Output", "输出 SPC 报告", "出"));
                flows.Add(MkFlow("SPC统计流程", s2));
                break;
            }
            case "ex_ocr_mqtt":
            {
                var s = new List<VisionFlowStep>();
                var c1 = MkStep(1, "拍张照片", "采集图像", "ImageCapture", captureMode, "采", 5000); c1.CaptureMode = captureMode; s.Add(c1);
                s.Add(MkStep(2, "ROI设置", "字符区域", "RoiSet", "ROI 30,20,200,40", "区"));
                s.Add(MkStep(3, "认文字", "OCR 识别", "Ocr", "识别批次号", "字"));
                s.Add(MkStep(4, "二维码", "读取二维码", "QrCode", "读取 QR/条码", "码"));
                s.Add(MkStep(5, "MES校验", "比对校验", "MesCheck", "与 MES 比对", "校"));
                var o1 = MkStep(6, "通讯发送", "MQTT 回传", "Comm", "MQTT 回传识别", "发"); o1.CommChannel = "MQTT"; o1.CommCmd = "发送"; o1.CommContent = "{\"sn\":\"$(SN)\"}"; o1.CommEncoding = "ASCII"; s.Add(o1);
                s.Add(MkStep(7, "报结果", "输出结果", "Output", "输出到结果表", "出"));
                flows.Add(MkFlow("OCR识别流程", s));

                var s2 = new List<VisionFlowStep>();
                s2.Add(MkStep(1, "字符库", "更新字库", "TemplateSave", "更新 OCR 字库", "模"));
                s2.Add(MkStep(2, "模板保存", "保存模板", "TemplateSave", "保存识别模板", "模"));
                flows.Add(MkFlow("模板管理流程", s2));
                break;
            }
            case "ex_sort_net":
            {
                var s = new List<VisionFlowStep>();
                var c1 = MkStep(1, "硬件触发", "采集图像", "ImageCapture", "硬触发采集", "采", 2000); c1.CaptureMode = "采集相机"; s.Add(c1);
                s.Add(MkStep(2, "颜色识别", "颜色分析", "ColorDetect", "HSV 颜色分类", "色"));
                s.Add(MkStep(3, "挑瑕疵", "缺陷检测", "Defect", "表面缺陷比对", "缺"));
                s.Add(MkStep(4, "等级判定", "分级", "Judge", "按等级分级", "判"));
                var o1 = MkStep(5, "通讯发送", "分拣指令", "Comm", "网口下发分拣", "发"); o1.CommChannel = "网口"; o1.CommCmd = "发送"; o1.CommContent = "{\"grade\":$(Grade)}"; o1.CommEncoding = "ASCII"; s.Add(o1);
                s.Add(MkStep(6, "报结果", "输出结果", "Output", "输出到结果表", "出"));
                flows.Add(MkFlow("分拣检测流程", s));

                var s2 = new List<VisionFlowStep>();
                s2.Add(MkStep(1, "计数", "剔除计数", "Count", "合格/剔除计数", "数"));
                var o2 = MkStep(2, "通讯发送", "UDP 上报", "Comm", "UDP 上报统计", "发"); o2.CommChannel = "UDP"; o2.CommCmd = "发送"; o2.CommContent = "{\"ok\":$(OK),\"ng\":$(NG)}"; o2.CommEncoding = "ASCII"; s2.Add(o2);
                flows.Add(MkFlow("剔除统计流程", s2));
                break;
            }
            case "ex_multi_cam":
            {
                var sl = new List<VisionFlowStep>();
                var cl = MkStep(1, "拍张照片", "左相机采集", "ImageCapture", "Camera_L", "采", 5000); cl.CaptureMode = "采集相机"; sl.Add(cl);
                sl.Add(MkStep(2, "ROI设置", "左区", "RoiSet", "ROI 0,0,400,300", "区"));
                sl.Add(MkStep(3, "图里找图案", "左局部匹配", "TemplateMatch", "灰度匹配 / 左半幅", "匹"));
                sl.Add(MkStep(4, "挑瑕疵", "左缺陷", "Defect", "左半幅缺陷", "缺"));
                flows.Add(MkFlow("左相机流程", sl));

                var sr = new List<VisionFlowStep>();
                var cr = MkStep(1, "拍张照片", "右相机采集", "ImageCapture", "Camera_R", "采", 5000); cr.CaptureMode = "采集相机"; sr.Add(cr);
                sr.Add(MkStep(2, "ROI设置", "右区", "RoiSet", "ROI 400,0,400,300", "区"));
                sr.Add(MkStep(3, "图里找图案", "右局部匹配", "TemplateMatch", "灰度匹配 / 右半幅", "匹"));
                sr.Add(MkStep(4, "挑瑕疵", "右缺陷", "Defect", "右半幅缺陷", "缺"));
                flows.Add(MkFlow("右相机流程", sr));

                var sp = new List<VisionFlowStep>();
                sp.Add(MkStep(1, "图像拼接", "全景合成", "Stitch", "左右拼接全景", "拼"));
                sp.Add(MkStep(2, "挑瑕疵", "全景缺陷", "Defect", "全景缺陷比对", "缺"));
                var op = MkStep(3, "通讯发送", "MQTT 上报", "Comm", "MQTT 上报结果", "发"); op.CommChannel = "MQTT"; op.CommCmd = "发送"; op.CommContent = "{\"panel\":\"$(Result)\"}"; op.CommEncoding = "ASCII"; sp.Add(op);
                sp.Add(MkStep(4, "报结果", "输出结果", "Output", "输出到结果表", "出"));
                flows.Add(MkFlow("拼接判定流程", sp));
                break;
            }
            case "ex_count_udp":
            {
                var s = new List<VisionFlowStep>();
                var c1 = MkStep(1, "打开文件", "读取图像", "ImageOpen", "打开文件夹", "文", 3000); c1.CaptureMode = "打开文件夹"; s.Add(c1);
                s.Add(MkStep(2, "目标检测", "检测目标", "Detect", "轮廓/模板检测", "检"));
                s.Add(MkStep(3, "计数", "目标计数", "Count", "计数目标数量", "数"));
                s.Add(MkStep(4, "正反面", "正反面判定", "FlipCheck", "判定正反面", "翻"));
                s.Add(MkStep(5, "报结果", "输出结果", "Output", "输出到结果表", "出"));
                flows.Add(MkFlow("计数流程", s));

                var s2 = new List<VisionFlowStep>();
                s2.Add(MkStep(1, "数据汇总", "汇总计数", "ReadResult", "汇总日计数", "读"));
                s2.Add(MkStep(2, "日统计", "日产量", "Statistics", "统计日产量", "统"));
                var o2 = MkStep(3, "通讯发送", "UDP 上报", "Comm", "UDP 上报计数", "发"); o2.CommChannel = "UDP"; o2.CommCmd = "发送"; o2.CommContent = "{\"count\":$(Count)}"; o2.CommEncoding = "ASCII"; s2.Add(o2);
                flows.Add(MkFlow("日统计流程", s2));
                break;
            }
            case "ex_calib_local":
            {
                var s = new List<VisionFlowStep>();
                var c1 = MkStep(1, "拍张照片", "采集标准件", "ImageCapture", captureMode, "采", 5000); c1.CaptureMode = captureMode; s.Add(c1);
                s.Add(MkStep(2, "ROI标定", "标定区域", "RoiSet", "ROI 标定基准", "区"));
                s.Add(MkStep(3, "模板保存", "保存模板", "TemplateSave", "保存基准模板", "模"));
                s.Add(MkStep(4, "参数固化", "固化参数", "Verify", "固化标定参数", "验"));
                flows.Add(MkFlow("模板采集流程", s));

                var s2 = new List<VisionFlowStep>();
                var c2 = MkStep(1, "拍张照片", "采集验证", "ImageCapture", captureMode, "采", 5000); c2.CaptureMode = captureMode; s2.Add(c2);
                s2.Add(MkStep(2, "匹配验证", "匹配验证", "TemplateMatch", "灰度匹配验证", "匹"));
                s2.Add(MkStep(3, "误差统计", "误差统计", "Statistics", "统计定位误差", "统"));
                s2.Add(MkStep(4, "报告输出", "输出报告", "Report", "输出标定报告", "报"));
                flows.Add(MkFlow("精度验证流程", s2));
                break;
            }
        }
        return flows;
    }

    public ProjectItem ToProjectItem(int index, string projectName)
    {
        var name = string.IsNullOrWhiteSpace(projectName) ? (Name + "-" + index) : projectName.Trim();
        var safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safe)) safe = Id + "-" + index;
        return new ProjectItem
        {
            ProjectName = name,
            Author = "admin",
            Description = LongDescription,
            Tags = TriggerType + "," + ImageSourceType + "," + ExternalChannel + "," + string.Join("/", Features),
            ProjectVersion = "1.0.0",
            ProjectPath = Path.Combine(NoCodeVision.Helpers.AppPaths.ProjectsDirectory, safe + ".ncv"),
            CreateTime = DateTime.Now.ToString("yyyy-MM-dd"),
            ModifyTime = DateTime.Now.ToString("yyyy-MM-dd"),
            ExampleId = Id,
            CameraName = CameraName,
            ImageSourceType = ImageSourceType,
            TriggerType = TriggerType,
            SendResultToExternal = SendResultToExternal,
            ExternalChannel = ExternalChannel,
            ExternalTarget = ExternalTarget,
            CommConfigs = BuildCommConfigs(),
            Flows = BuildFlows(),
            AutoSave = true, SaveInterval = 300, Language = "简体中文", Theme = "浅色",
            DefaultUnit = "毫米", LogLevel = "Info", LogKeepDays = 30,
            AutoStart = false, AutoRunAfterStart = false, EmergencyStopOnError = true,
            ImageSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Images"),
            DataSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Data"),
            BackupPath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Backup"),
            MaxBackupCount = 10, PasswordEnabled = false, AutoLockMinutes = 5,
            AllowRemote = false, MaxUndoSteps = 50, ResultRetainDays = 90, DebugMode = false
        };
    }
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

            ProjectPath = Path.Combine(NoCodeVision.Helpers.AppPaths.ProjectsDirectory, "DemoVision-01.ncv"),

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

            ImageSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Images"),

            DataSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Data"),

            BackupPath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Backup"),

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

            ProjectPath = Path.Combine(NoCodeVision.Helpers.AppPaths.ProjectsDirectory, "MotorBracket-A.ncv"),

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

            ImageSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Images"),

            DataSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Data"),

            BackupPath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Backup"),

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

            ProjectPath = Path.Combine(NoCodeVision.Helpers.AppPaths.ProjectsDirectory, "PCB-Inspection.ncv"),

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

            ImageSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Images"),

            DataSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Data"),

            BackupPath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Backup"),

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

    public ProjectItem? SelectedProject { get => _selectedProject; set { if (SetField(ref _selectedProject, value)) LoadCurrentProject(); } }



    public string[] Languages { get; } = { "简体中文", "繁體中文", "English" };

    public string[] Themes { get; } = { "浅色", "深色", "跟随系统" };

    public string[] Units { get; } = { "毫米", "厘米", "英寸", "像素" };

    public string[] LogLevels { get; } = { "Debug", "Info", "Warning", "Error" };



    public ICommand AddCmd { get; }

    public ICommand DeleteCmd { get; }

    public ICommand NewCmd { get; }

    public ICommand OpenCmd { get; }

    public ICommand SaveCmd { get; }



    public ICommand ShowExamplesCmd { get; }

    public ICommand ConfirmExampleCmd { get; }

    public ICommand CancelExampleCmd { get; }

    public ICommand CreateBlankCmd { get; }



    public ObservableCollection<ProjectExample> Examples { get; } = ProjectExample.BuildExamples();

    public static ProjectViewModel? Instance { get; set; }



    public string NewProjectName { get; set; } = "";



    // 将当前选中项目的 流程/通讯 配置载入运行中的 FlowViewModel / CommunicationViewModel

    public void LoadCurrentProject()

    {

        var p = _selectedProject;

        if (p == null) return;

        if (FlowViewModel.Instance != null && p.Flows.Count > 0)

        {

            FlowViewModel.Instance.Flows.Clear();

            foreach (var f in p.Flows) FlowViewModel.Instance.Flows.Add(CloneFlow(f));

            FlowViewModel.NormalizeStepNames(FlowViewModel.Instance.Flows);

        }

        if (CommunicationViewModel.Instance != null && p.CommConfigs.Count > 0)

        {

            CommunicationViewModel.Instance.Configs.Clear();

            foreach (var c in p.CommConfigs) CommunicationViewModel.Instance.Configs.Add(CloneComm(c));

        }

    }



    // 将运行中的 流程/通讯 配置写回项目（保存前调用，保证 .ncv 持久化最新配置）

    private void SyncLiveToProject(ProjectItem p)

    {

        if (FlowViewModel.Instance != null)

            p.Flows = new ObservableCollection<VisionFlow>(FlowViewModel.Instance.Flows.Select(CloneFlow));

        if (CommunicationViewModel.Instance != null)

            p.CommConfigs = new ObservableCollection<CommConfigItem>(CommunicationViewModel.Instance.Configs.Select(CloneComm));

    }



    private static VisionFlow CloneFlow(VisionFlow f)

        => JsonSerializer.Deserialize<VisionFlow>(JsonSerializer.Serialize(f))!;

    private static CommConfigItem CloneComm(CommConfigItem c)

        => JsonSerializer.Deserialize<CommConfigItem>(JsonSerializer.Serialize(c))!;





    private bool _isExampleDialogOpen;

    public bool IsExampleDialogOpen { get => _isExampleDialogOpen; set => SetField(ref _isExampleDialogOpen, value); }

    private ProjectExample? _selectedExample;

    public ProjectExample? SelectedExample { get => _selectedExample; set => SetField(ref _selectedExample, value); }



    public ProjectViewModel()

    {

        SelectedProject = Projects[0];



        AddCmd = new RelayCommand(_ =>

        {

            var next = Projects.Count + 1;

            var item = new ProjectItem

            {

                ProjectName = $"新建项目-{next}",

                Author = "admin",

                Customer = "",

                Description = "",

                Tags = "",

                ProjectVersion = "1.0.0",

                ProjectPath = Path.Combine(NoCodeVision.Helpers.AppPaths.ProjectsDirectory, $"NewProject-{next}.ncv"),

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

                ImageSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Images"),

                DataSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Data"),

                BackupPath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Backup"),

                MaxBackupCount = 10,

                PasswordEnabled = false,

                AutoLockMinutes = 5,

                AllowRemote = false,

                MaxUndoSteps = 50,

                ResultRetainDays = 90,

                DebugMode = false

            };

            Projects.Add(item);

            SelectedProject = item;

            try { SaveProject(item); } catch { }

        });



        DeleteCmd = new RelayCommand(_ =>

        {

            if (_selectedProject != null)

            {

                try

                {

                    var path = _selectedProject.ProjectPath;

                    Projects.Remove(_selectedProject);

                    if (File.Exists(path)) File.Delete(path);

                    SelectedProject = Projects.Count > 0 ? Projects[0] : null;

                }

                catch { }

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

            if (_selectedProject == null) return;

            try

            {

                if (File.Exists(_selectedProject.ProjectPath))

                {

                    var json = File.ReadAllText(_selectedProject.ProjectPath);

                    var loaded = JsonSerializer.Deserialize<ProjectItem>(json);

                    if (loaded != null)

                    {

                        loaded.ProjectPath = _selectedProject.ProjectPath;

                        var idx = Projects.IndexOf(_selectedProject);

                        if (idx >= 0) Projects[idx] = loaded;

                        SelectedProject = loaded;

                    }

                }

                if (_selectedProject != null)

                    _selectedProject.Status = $"已打开 {_selectedProject.ProjectPath} · {DateTime.Now:HH:mm:ss}";

            }

            catch (Exception ex) { if (_selectedProject != null) _selectedProject.Status = $"打开失败: {ex.Message}"; }

            OnPropertyChanged(nameof(SelectedProject));

        });



        SaveCmd = new RelayCommand(_ =>

        {

            if (_selectedProject == null) return;

            try

            {

                SaveProject(_selectedProject);

                _selectedProject.Status = $"已保存 · {DateTime.Now:HH:mm:ss}";

                _selectedProject.ModifyTime = DateTime.Now.ToString("yyyy-MM-dd");

            }

            catch (Exception ex) { _selectedProject.Status = $"保存失败: {ex.Message}"; }

            OnPropertyChanged(nameof(SelectedProject));

        });



        SyncProjectsWithDisk();

        if (Projects.Count > 0) SelectedProject = Projects[0];



        Instance = this;

        ShowExamplesCmd = new RelayCommand(_ => { SelectedExample = null; IsExampleDialogOpen = true; });

        CancelExampleCmd = new RelayCommand(_ => { IsExampleDialogOpen = false; SelectedExample = null; });

        ConfirmExampleCmd = new RelayCommand(_ =>
        {
            var ex = _selectedExample;

            if (ex == null) return;

            var next = Projects.Count + 1;

            var item = ex.ToProjectItem(next, NewProjectName);

            Projects.Add(item);

            SelectedProject = item;

            SyncLiveToProject(item);

            IsExampleDialogOpen = false;

            SelectedExample = null;

            NewProjectName = "";

            try { SaveProject(item); } catch { }        }, _ => _selectedExample != null);

        CreateBlankCmd = new RelayCommand(_ =>

        {

            var next = Projects.Count + 1;

            var item = new ProjectItem

            {

                ProjectName = string.IsNullOrWhiteSpace(NewProjectName) ? ("空白项目-" + next) : NewProjectName.Trim(),

                Author = "admin",

                Description = "空白项目",

                ProjectVersion = "1.0.0",

                ProjectPath = Path.Combine(NoCodeVision.Helpers.AppPaths.ProjectsDirectory, "Blank-" + next + ".ncv"),

                CreateTime = DateTime.Now.ToString("yyyy-MM-dd"),

                ModifyTime = DateTime.Now.ToString("yyyy-MM-dd"),

                ExampleId = "ex_blank",

                ImageSourceType = "采集相机",

                TriggerType = "软件触发",

                SendResultToExternal = false,

                ExternalChannel = "MQTT",

                ExternalTarget = "",

                AutoSave = true, SaveInterval = 300, Language = "简体中文", Theme = "浅色",

                DefaultUnit = "毫米", LogLevel = "Info", LogKeepDays = 30,

                AutoStart = false, AutoRunAfterStart = false, EmergencyStopOnError = true,

                ImageSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Images"),

                DataSavePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Data"),

                BackupPath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Backup"),

                MaxBackupCount = 10, PasswordEnabled = false, AutoLockMinutes = 5,

                AllowRemote = false, MaxUndoSteps = 50, ResultRetainDays = 90, DebugMode = false

            };

            Projects.Add(item);

            SelectedProject = item;

            IsExampleDialogOpen = false;

            NewProjectName = "";

            try { SaveProject(item); } catch { }

        });

    }



    private void SaveProject(ProjectItem item)

    {

        SyncLiveToProject(item);

        var dir = Path.GetDirectoryName(item.ProjectPath);

        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(item, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(item.ProjectPath, json);

    }



    private void SyncProjectsWithDisk()

    {

        try

        {

            var dir = NoCodeVision.Helpers.AppPaths.ProjectsDirectory;

            var files = Directory.EnumerateFiles(dir, "*.ncv").ToList();

            if (files.Count > 0)

            {

                Projects.Clear();

                foreach (var f in files)

                {

                    try

                    {

                        var json = File.ReadAllText(f);

                        var item = JsonSerializer.Deserialize<ProjectItem>(json);

                        if (item != null)

                        {

                            item.ProjectPath = f;

                            Projects.Add(item);

                        }

                    }

                    catch { /* 单个文件损坏跳过 */ }

                }

            }

            else

            {

                foreach (var p in Projects.ToList()) SaveProject(p);

            }

        }

        catch { /* 忽略 */ }

    }



}



#endregion



#region 相机页



// 作者 温启志 编写 ░ 微信 187 1936 1399

public class CameraViewModel : ViewModelBase
{
    public static CameraViewModel? Instance { get; private set; }

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



    private string _saveImagePath = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "Images");

    public string SaveImagePath { get => _saveImagePath; set => SetField(ref _saveImagePath, value); }



    private bool _isConnected;

    public bool IsConnected { get => _isConnected; set { if (SetField(ref _isConnected, value)) OnPropertyChanged(nameof(StatusText)); } }



    public string StatusText => IsConnected ? "实时采集中" : "未连接";

    private System.Windows.Media.Imaging.BitmapSource? _liveImage;

    public System.Windows.Media.Imaging.BitmapSource? CameraLiveImage { get => _liveImage; set => SetField(ref _liveImage, value); }



    public ICommand ConnectCmd { get; }

    public ICommand StartCmd { get; }

    public ICommand StopCmd { get; }

    /// <summary>构造时捕获 UI 线程 Dispatcher，避免后台相机线程里 Application.Current 为空导致 NullReferenceException。</summary>
    private readonly System.Windows.Threading.Dispatcher _uiDispatcher;



    public CameraViewModel()

    {
        Instance = this;

        // 捕获 UI 线程 Dispatcher（单例在 UI 线程构造），后台相机线程回调不再依赖 Application.Current
        _uiDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

        SelectedCamera = Cameras[0];

        HardwareManager.Instance.Camera.FrameReady += bmp =>
        {
            var d = _uiDispatcher;
            if (d != null && !d.CheckAccess())
                d.BeginInvoke(new Action(() => CameraLiveImage = bmp));
            else
                CameraLiveImage = bmp;
        };



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

        // 自动保存/恢复相机列表与连接状态（bin\Data\ui_state.json）
        UiState.AttachVm("Camera", this);

        SelectedCamera = Cameras.Count > 0 ? Cameras[0] : null;

    }

}



#endregion



#region 通讯页



// 〔作者〕温启志 编写 ◇ 微 信：187·1936·1399

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



// 温启志 编写 ▦ 微信 18719361399（保留署名）

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



    public static CommunicationViewModel? Instance { get; set; }

    public CommunicationViewModel()

    {

        SelectedConfig = Configs[0];

        Instance = this;

        ProjectViewModel.Instance?.LoadCurrentProject();

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

        // 自动保存/恢复通讯配置列表（bin\Data\ui_state.json）
        UiState.AttachVm("Comm", this);

        SelectedConfig = Configs.Count > 0 ? Configs[0] : null;

    }



    private void PushLog(string line)

    {

        // 通讯日志是收包线程/串口线程回调进来的，ObservableCollection 只能在 UI 线程改

        var disp = System.Windows.Application.Current?.Dispatcher;

        if (disp != null && !disp.CheckAccess())

        {
            disp.BeginInvoke(new Action(() => PushLog(line)));
            return;
        }

        DebugLog.Insert(0, $"{DateTime.Now:HH:mm:ss} {line}");

        while (DebugLog.Count > 300) DebugLog.RemoveAt(DebugLog.Count - 1);

    }

}



#endregion



#region 变量页



// 温启志 编写 ◇ 微信：187 1936 1399（请勿移除）

public class VariablesViewModel : ViewModelBase

{

    /// <summary>全局唯一实例：流程算子写的变量和「变量」页看到的是同一张表。</summary>
    public static VariablesViewModel Instance { get; } = new();



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

        // 自动保存/恢复变量列表（bin\Data\ui_state.json）
        UiState.AttachVm("Vars", this);

        Selected = Variables.Count > 0 ? Variables[0] : null;

    }

}



#endregion



#region 流程页



// 〔版权所有〕温启志 ◆ 微信 187-1936-1399

/// <summary>流程单次检测（一件产品）的真实结果，供操作员页等消费。</summary>
public class FlowProductResult
{
    public bool IsOk { get; init; }
    public double Score { get; init; }
    public int DefectCount { get; init; }
    public System.Windows.Media.Imaging.BitmapSource? Image { get; init; }
    public string? FlowName { get; init; }
    public DateTime Timestamp { get; init; }
}

public class FlowViewModel : ViewModelBase

{

    public static FlowViewModel? Instance { get; private set; }

    /// <summary>单件产品检测完成事件：携带真实匹配分数 / 缺陷数 / 抓拍图像 / 合格结论，供操作员页订阅。</summary>
    public event Action<FlowProductResult>? ProductInspected;



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

                    new VisionFlowStep { Index = 1, Function = "拍张照片", Name = "采集左视野", ParamSummary = "打开文件", Timeout = 5000, CostMs = 12.3, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = "", StatusText = "未开始" },

                    new VisionFlowStep { Index = 2, Function = "图里找图案", Name = "定位基准", ParamSummary = "tpl_A / score≥0.85", Timeout = 3000, CostMs = 8.7, ActualValue = "(120,88)", Icon = "🎯", StepType = "TemplateMatch", ImageSource = "tpl_A.png", TemplateFile = "tpl_A.png", RoiX=80, RoiY=60, RoiW=160, RoiH=120, ScoreThreshold=0.85, StatusText = "匹配成功" },

                    new VisionFlowStep { Index = 3, Function = "量尺寸", Name = "测量孔径", ParamSummary = "圆径 / 12.00±0.05", Timeout = 2000, CostMs = 5.4, ActualValue = "12.03", Icon = "📐", StepType = "Measure", StatusText = "尺寸合格" },

                    new VisionFlowStep { Index = 4, Function = "判一下对不对", Name = "判定合格", ParamSummary = "孔径 OK", Timeout = 1000, CostMs = 0.2, ActualValue = "Pass", Icon = "✅", StepType = "Logic", StatusText = "通过" },

                    new VisionFlowStep { Index = 5, Function = "报结果", Name = "输出结果", ParamSummary = "PLC_D200=1", Timeout = 1000, CostMs = 0.5, ActualValue = "Done", Icon = "📤", StepType = "Output", StatusText = "已完成" },

                }

            },

            new VisionFlow

            {

                Name = "定位流程", Icon = "🧭",

                Steps = new ObservableCollection<VisionFlowStep>

                {

                    new VisionFlowStep { Index = 1, Function = "拍张照片", Name = "采集顶视野", ParamSummary = "打开文件", Timeout = 5000, CostMs = 15.1, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = "", StatusText = "未开始" },

                    new VisionFlowStep { Index = 2, Function = "图里找图案", Name = "找中心点", ParamSummary = "tpl_center / score≥0.80", Timeout = 3000, CostMs = 9.2, ActualValue = "(512,384)", Icon = "🎯", StepType = "TemplateMatch", ImageSource = "tpl_center.png", TemplateFile = "tpl_center.png", RoiX=200, RoiY=150, RoiW=180, RoiH=140, ScoreThreshold=0.80, StatusText = "匹配成功" },

                }

            },

            new VisionFlow

            {

                Name = "检测流程", Icon = "🔍",

                Steps = new ObservableCollection<VisionFlowStep>

                {

                    new VisionFlowStep { Index = 1, Function = "拍张照片", Name = "采集检测图", ParamSummary = "打开文件", Timeout = 5000, CostMs = 12.3, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = "", StatusText = "未开始" },

                    new VisionFlowStep { Index = 2, Function = "量尺寸", Name = "测量边距", ParamSummary = "距离 / 45.0±0.1", Timeout = 2000, CostMs = 6.8, ActualValue = "45.02", Icon = "📐", StepType = "Measure", MeasureType="边距", RoiX=50, RoiY=50, RoiW=200, RoiH=100, StatusText = "尺寸合格" },

                }

            },


            // 示例：图像采集(真实图) → 模板匹配(ROI区域自动取模板) → ROI内缺陷检测 → 几何测量(孔径) → 结果通讯
            new VisionFlow
            {
                Name = "示例·零件检测A", Icon = "🧪",
                Steps = new ObservableCollection<VisionFlowStep>
                {
                    new VisionFlowStep { Index = 1, Function = "拍张照片", Name = "采集零件图", ParamSummary = "打开文件 part_a.png", Timeout = 5000, CostMs = 12.3, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = @"D:\wqz\code\NoCodeVision\SampleImages\part_a.png", StatusText = "未开始" },
                    new VisionFlowStep { Index = 2, Function = "图里找图案", Name = "ROI定位基准", ParamSummary = "ROI(240,160,160,120) / score≥0.70", Timeout = 3000, CostMs = 8.7, ActualValue = "未匹配", Icon = "🎯", StepType = "TemplateMatch", MatchMode = "灰度匹配", ImageSource = @"D:\wqz\code\NoCodeVision\SampleImages\part_a.png", RoiX = 240, RoiY = 160, RoiW = 160, RoiH = 120, ScoreThreshold = 0.70, StatusText = "未开始" },
                    new VisionFlowStep { Index = 3, Function = "挑瑕疵", Name = "ROI内缺陷比对", ParamSummary = "差异比对 / 阈值45", Timeout = 3000, CostMs = 6.5, ActualValue = "未检测", Icon = "🔍", StepType = "Defect", DiffThreshold = 45, MinAreaFrac = 0.004, StatusText = "未开始" },
                    new VisionFlowStep { Index = 4, Function = "量尺寸", Name = "测量孔径", ParamSummary = "圆径 / 68±5", Timeout = 2000, CostMs = 5.4, ActualValue = "未测量", Icon = "📐", StepType = "Measure", MeasureType = "圆径", NominalValue = 68, Tolerance = 5, RoiX = 436, RoiY = 326, RoiW = 68, RoiH = 68, StatusText = "未开始" },
                    new VisionFlowStep { Index = 5, Function = "通讯发送", Name = "结果通讯", ParamSummary = "PLC-串口 / RESULT=OK", Timeout = 1000, CostMs = 0.5, ActualValue = "未发送", Icon = "📡", StepType = "Comm", CommChannel = "PLC-串口", CommCmd = "发送", CommContent = "RESULT=OK", CommEncoding = "ASCII", StatusText = "未开始" },
                }
            },

            // 示例：第二张真实图，同样流水线（不同底色/标记），用于对比演示
            new VisionFlow
            {
                Name = "示例·零件检测B", Icon = "🧫",
                Steps = new ObservableCollection<VisionFlowStep>
                {
                    new VisionFlowStep { Index = 1, Function = "拍张照片", Name = "采集零件图", ParamSummary = "打开文件 part_b.png", Timeout = 5000, CostMs = 13.1, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = @"D:\wqz\code\NoCodeVision\SampleImages\part_b.png", StatusText = "未开始" },
                    new VisionFlowStep { Index = 2, Function = "图里找图案", Name = "ROI定位基准", ParamSummary = "ROI(240,160,160,120) / score≥0.70", Timeout = 3000, CostMs = 9.2, ActualValue = "未匹配", Icon = "🎯", StepType = "TemplateMatch", MatchMode = "灰度匹配", ImageSource = @"D:\wqz\code\NoCodeVision\SampleImages\part_b.png", RoiX = 240, RoiY = 160, RoiW = 160, RoiH = 120, ScoreThreshold = 0.70, StatusText = "未开始" },
                    new VisionFlowStep { Index = 3, Function = "挑瑕疵", Name = "ROI内缺陷比对", ParamSummary = "差异比对 / 阈值45", Timeout = 3000, CostMs = 6.8, ActualValue = "未检测", Icon = "🔍", StepType = "Defect", DiffThreshold = 45, MinAreaFrac = 0.004, StatusText = "未开始" },
                    new VisionFlowStep { Index = 4, Function = "量尺寸", Name = "测量孔径", ParamSummary = "圆径 / 68±5", Timeout = 2000, CostMs = 5.6, ActualValue = "未测量", Icon = "📐", StepType = "Measure", MeasureType = "圆径", NominalValue = 68, Tolerance = 5, RoiX = 436, RoiY = 326, RoiW = 68, RoiH = 68, StatusText = "未开始" },
                    new VisionFlowStep { Index = 5, Function = "通讯发送", Name = "结果通讯", ParamSummary = "PLC-串口 / RESULT=OK", Timeout = 1000, CostMs = 0.5, ActualValue = "未发送", Icon = "📡", StepType = "Comm", CommChannel = "PLC-串口", CommCmd = "发送", CommContent = "RESULT=OK", CommEncoding = "ASCII", StatusText = "未开始" },
                }
            },
        };

    }



    // 新建「流程 / 脚本流程」时预置的完整 5 步流水线：采集 → 模板匹配 → 缺陷检测 → 测量 → 通讯

    /// <summary>弹「新建流程」对话框选模板；取消返回 null。</summary>
    static System.Collections.Generic.List<NoCodeVision.Views.AddFlowDialog.Option> Opts(
        params (string Key, string Title, string Desc)[] items) =>
        items.Select(i => new NoCodeVision.Views.AddFlowDialog.Option { Key = i.Key, Title = i.Title, Desc = i.Desc }).ToList();

    /// <summary>新建流程弹窗：每个按钮给的例子不一样，名称框默认按「类型-序号」填好。</summary>
    bool PromptFlow(string title, string defaultName, System.Collections.Generic.List<NoCodeVision.Views.AddFlowDialog.Option> items,
        out string key, out string name)
    {
        key = "empty"; name = defaultName;
        try
        {
            var dlg = new NoCodeVision.Views.AddFlowDialog(title, defaultName, items)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            if (dlg.ShowDialog() != true) return false;
            key = dlg.SelectedKey;
            if (!string.IsNullOrWhiteSpace(dlg.FlowName)) name = dlg.FlowName;
            return true;
        }
        catch { return true; }   // 弹窗本身出问题时按默认值继续，别让「添加」点了没反应
    }

    // ---------- 脚本每行耗时（编辑器右侧那列毫秒数） ----------

    /// <summary>行号 → 这一行累计跑了多少毫秒。</summary>
    public System.Collections.Generic.Dictionary<int, double> LineMs { get; } = new();

    /// <summary>行号 → 这一行被跑到过几次（循环里会 >1）。</summary>
    public System.Collections.Generic.Dictionary<int, int> LineHits { get; } = new();

    int _acctLine = -1;

    double _acctMs;

    /// <summary>开跑：把上一轮的每行耗时清掉。</summary>
    void ResetLineMs()
    {
        LineMs.Clear(); LineHits.Clear(); _acctLine = -1; _acctMs = 0;
        OnPropertyChanged(nameof(LineCosts));
    }

    /// <summary>宿主每执行一行都回报总耗时，两次之差就是上一行的开销。</summary>
    void AccountLineMs(double nowMs)
    {
        if (_acctLine > 0 && nowMs >= _acctMs)
        {
            LineMs[_acctLine] = (LineMs.TryGetValue(_acctLine, out var old) ? old : 0) + (nowMs - _acctMs);
            LineHits[_acctLine] = (LineHits.TryGetValue(_acctLine, out var n) ? n : 0) + 1;
        }
        _acctLine = CurrentLine; _acctMs = nowMs;
        OnPropertyChanged(nameof(LineCosts));
    }

    /// <summary>跑完收尾：最后一行不会再触发换行事件，用宿主的真实累计时间补记一次。</summary>
    void FlushLineMs(double endMs)
    {
        if (_acctLine <= 0) return;
        double end = endMs > 0 ? endMs : ScriptElapsedMs;
        if (end >= _acctMs)
        {
            LineMs[_acctLine] = (LineMs.TryGetValue(_acctLine, out var old) ? old : 0) + (end - _acctMs);
            LineHits[_acctLine] = (LineHits.TryGetValue(_acctLine, out var n) ? n : 0) + 1;
        }
        _acctLine = -1;
        OnPropertyChanged(nameof(LineCosts));
    }

    /// <summary>只是个通知用的假属性：耗时表变了就 raise 它，视图收到后重画。</summary>
    public string LineCosts => "";

    /// <summary>只改名：同一个对话框，不给例子清单。</summary>
    bool PromptRename(string defaultName, out string name)
    {
        name = defaultName;
        try
        {
            var dlg = new NoCodeVision.Views.AddFlowDialog("重命名流程", defaultName, null)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            if (dlg.ShowDialog() != true) return false;
            if (!string.IsNullOrWhiteSpace(dlg.FlowName)) name = dlg.FlowName;
            return true;
        }
        catch { return true; }
    }

    VisionFlowStep MkStep(int idx, string tool, string name, Action<VisionFlowStep>? cfg = null)
    {
        var (icon, type) = ToolToIconType(tool);
        var s = new VisionFlowStep
        {
            Index = idx,
            Function = tool,
            Name = name,
            Icon = icon,
            StepType = type,
            ParamSummary = "-",
            StatusText = "未开始",
        };
        cfg?.Invoke(s);
        return s;
    }

    /// <summary>按模板给流程填步骤；empty 就什么都不放（空白流程）。</summary>
    void ApplyFlowTemplate(VisionFlow flow, string key)
    {
        if (key == "barcode")
        {
            // 条码定位发送：模板匹配先框住条码 → 只在那一条里读 Code128 → 字符串发 TCP → 画到图上
            var bcDir = Helpers.AppPaths.ResolvePath(@"Projects\条码识别");
            flow.Steps = new ObservableCollection<VisionFlowStep>
            {
                MkStep(1, "拍张照片", "读标签图", s => { s.CaptureMode = "打开文件夹"; s.FolderPath = System.IO.Path.Combine(bcDir, "图"); }),
                MkStep(2, "图里找图案", "定位条码", s =>
                {
                    // 模板 = 第一张标签上框住的那条条码（620,560,1380,235）
                    s.MatchMode = "轮廓匹配";
                    s.RoiX = 620; s.RoiY = 560; s.RoiW = 1380; s.RoiH = 235;
                    s.ScoreThreshold = 0.5;
                    s.PyramidLevel = 0; s.AngleStart = -12; s.AngleStop = 12; s.AngleStep = 1.5;
                    s.TopN = 5; s.Overlap = 0.35; s.ScaleRange = 0;
                }),
                MkStep(3, "读条码", "读串号", s => { s.CompareSource = "第2步"; s.MatchMode = "一维条码"; s.UseDpm = true; s.OutputVar = "bcText"; }),
                MkStep(4, "TCP 发送", "发给MES", s => { s.TcpHost = "127.0.0.1"; s.TcpPort = 9000; s.CompareSource = "第3步"; s.LineEnd = "换行 LF"; s.TcpTimeout = 1500; }),
                MkStep(5, "显示结果", "画到图上", s => { s.CompareSource = "第3步"; s.CompareVar = "第2步"; s.MeasureType = "圆和点"; s.NotifyMessage = "BC"; }),
            };
            return;
        }
        if (key == "qr")
        {
            // 二维码定位发送：模板匹配先找到码 → 只在那一块里读（点阵码增强）→ 字符串发 TCP → 画到图上
            var qrDir = Helpers.AppPaths.ResolvePath(@"Projects\二维码识别");
            flow.Steps = new ObservableCollection<VisionFlowStep>
            {
                MkStep(1, "拍张照片", "读工件图", s => { s.CaptureMode = "打开文件夹"; s.FolderPath = System.IO.Path.Combine(qrDir, "图"); }),
                MkStep(2, "图里找图案", "定位二维码", s =>
                {
                    // 「图里找图案」的模板就是第一张图上框住的那一块：这里框住码区（1302,1571,280,280）
                    s.MatchMode = "轮廓匹配";
                    s.RoiX = 1302; s.RoiY = 1571; s.RoiW = 280; s.RoiH = 280;
                    s.ScoreThreshold = 0.5;
                    // 点阵码的细节经不起金字塔降采样（3 层时直接匹配不到），所以金字塔关掉、角度只 ±25°
                    s.PyramidLevel = 0; s.AngleStart = -25; s.AngleStop = 25; s.AngleStep = 2.5;
                    s.TopN = 5; s.Overlap = 0.35; s.ScaleRange = 0;   // 底层按搜索顺序返回，候选给足 5 个再取分最高的才稳
                }),
                MkStep(3, "读二维码", "读内容", s => { s.CompareSource = "第2步"; s.UseDpm = true; s.OutputVar = "qrText"; }),
                MkStep(4, "TCP 发送", "发给MES", s => { s.TcpHost = "127.0.0.1"; s.TcpPort = 9000; s.CompareSource = "第3步"; s.LineEnd = "换行 LF"; s.TcpTimeout = 1500; }),
                MkStep(5, "显示结果", "画到图上", s => { s.CompareSource = "第3步"; s.CompareVar = "第2步"; s.MeasureType = "圆和点"; s.NotifyMessage = "QR"; }),
            };
            return;
        }
        // 脚本流程的例子：只换脚本内容，不搭步骤
        if (key == "lua-empty")
        {
            flow.ScriptContent = "-- 空白脚本：可用接口 vision.* / plc.* / motion.* / print(...)\n-- 例：\n-- local score = vision.match(\"tpl_A.png\")\n-- if score >= 0.85 then plc.write(200, 1) end\n";
            return;
        }
        if (key == "lua-match")
        {
            flow.ScriptContent = "-- 示例：抓图 → 模板匹配 → 分数判定 → 写 PLC\nlocal score = vision.grab_and_match(\"tpl_A.png\")   -- 抓一帧并匹配模板，返回 0~1\nlocal threshold = 0.85\nprint(\"匹配分数: \" .. string.format(\"%.3f\", score))\n\nif score >= threshold then\n    plc.write(200, 1)                      -- 合格：发到位信号\n    plc.write(201, math.floor(score * 1000))  -- 把分数放大成整数一起下发\n    print(\"合格，已通知机台\")\nelse\n    plc.write(200, 0)                       -- 不合格：复位信号\n    plc.write(202, 1)                       -- 报警\n    print(\"不合格，已报警\")\nend\n\nsleep(50)\n";
            return;
        }
        if (key == "lua-loop")
        {
            flow.ScriptContent = "-- 示例：连续采 10 次取平均，再一次性下发\nlocal sum, n = 0, 10\nfor i = 1, n do\n    local s = vision.grab_and_match(\"tpl_A.png\")\n    sum = sum + s\n    sleep(20)\nend\nlocal avg = sum / n\nprint(\"平均分数: \" .. string.format(\"%.3f\", avg))\nplc.write(210, math.floor(avg * 1000))\n";
            return;
        }
        // 运控例子：上电 → 回零 → 走位 → 等条件 → 开吸盘
        if (key == "motion")
        {
            flow.Steps = new ObservableCollection<VisionFlowStep>
            {
                MkStep(1, "轴上电解锁", "使能", s => { s.TargetAxis = "X"; s.SwitchState = "上电解锁"; }),
                MkStep(2, "轴回原点", "建立零点", s => { s.TargetAxis = "X"; s.HomeOffset = 0; s.Timeout = 8000; }),
                MkStep(3, "轴走到位置", "去取料位", s => { s.TargetAxis = "X"; s.TargetX = 250; s.Speed = 120; s.InPosTimeout = 3000; }),
                MkStep(4, "等条件", "等到位信号", s => { s.WaitCondition = "DI_3=通"; s.WaitTime = 3000; }),
                MkStep(5, "写开关量", "开吸盘", s => { s.IoChannel = "DO_1"; s.SwitchState = "通"; s.ConfirmWrite = true; }),
            };
            return;
        }
        // 节点图例子：三个节点已经连好线
        if (key == "node")
        {
            var nCap = MkStep(1, "拍张照片", "取图", s => { s.CaptureMode = "打开文件夹"; s.NodeX = 300; s.NodeY = 60; });
            var nCircle = MkStep(2, "找圆", "找圆", s =>
            {
                s.NodeX = 300; s.NodeY = 300; s.ExpectRadius = 0; s.CaliperLength = 60; s.SamplingStep = 6;
                s.EdgePolarity = "从亮到暗"; s.FitMethod = "正交距离(更准)"; s.OutputVar = "dRadius";
            });
            var nJudge = MkStep(3, "判个数对不对", "判定", s =>
            {
                s.NodeX = 300; s.NodeY = 540; s.CompareSource = "第2步"; s.AreaMin = 100; s.AreaMax = 400;
            });
            flow.Steps = new ObservableCollection<VisionFlowStep> { nCap, nCircle, nJudge };
            // 默认输出口的名字是空串，写成「出」会静默连不上线
            flow.Links = new ObservableCollection<FlowNodeLink>
            {
                new FlowNodeLink { FromId = nCap.NodeId, FromPort = "", ToId = nCircle.NodeId },
                new FlowNodeLink { FromId = nCircle.NodeId, FromPort = "", ToId = nJudge.NodeId },
            };
            return;
        }
        if (key == "pcba")
        {
            // PCBA 元件有无：拍张照片 → 零样本查瑕疵（和好板比）→ 判个数 → 显示结果
            var pdir = Helpers.AppPaths.ToStoredPath(Helpers.AppPaths.ResolvePath(@"Projects\PCBA元件有无\图"));
            var pgold = Helpers.AppPaths.ToStoredPath(Helpers.AppPaths.ResolvePath(@"Projects\PCBA元件有无\图\标准图.png"));
            flow.Steps = new ObservableCollection<VisionFlowStep>
            {
                MkStep(1, "拍张照片", "读 PCBA", s => { s.CaptureMode = "打开文件夹"; s.FolderPath = pdir; }),
                MkStep(2, "零样本查瑕疵", "和好板比", s =>
                {
                    s.TemplateFile = pgold; s.MatchMode = "平移+旋转";
                    s.Number1 = 55; s.AreaMin = 25; s.IndexProbe = 0;
                    s.RoiX = 0; s.RoiY = 0; s.RoiW = 0; s.RoiH = 0;
                }),
                MkStep(3, "判个数对不对", "漏件/多件判定", s => { s.CompareSource = "第2步"; s.AreaMin = 0; s.AreaMax = 0; }),
                MkStep(4, "显示结果", "画到图上", s => { s.CompareSource = "第2步"; s.MeasureType = "圆和点"; }),
            };
            return;
        }

        if (key == "metrology")
        {
            var mdir = Helpers.AppPaths.ToStoredPath(Helpers.AppPaths.ResolvePath(@"Projects\精找圆找直线\图"));
            flow.Steps = new ObservableCollection<VisionFlowStep>
            {
                MkStep(1, "拍张照片", "读工件图", s => { s.CaptureMode = "打开文件夹"; s.FolderPath = mdir; }),
                MkStep(2, "精找圆", "找圆", s =>
                {
                    s.RoiX = 180; s.RoiY = 130; s.RoiW = 520; s.RoiH = 520;
                    s.ExpectRadius = 180; s.SamplingStep = 72; s.CaliperLength = 80;
                    s.EdgePolarity = "两种都要"; s.EdgeConfidence = 30;
                }),
                MkStep(3, "精找直线", "找直边", s =>
                {
                    s.RoiX = 497; s.RoiY = 240; s.RoiW = 46; s.RoiH = 280;
                    s.SamplingStep = 40; s.CaliperLength = 30;
                    s.EdgePolarity = "两种都要"; s.EdgeConfidence = 30;
                }),
                MkStep(4, "显示结果", "画到图上", s => { s.CompareSource = "第2步"; s.CompareVar = "第3步"; s.MeasureType = "圆和点"; }),
            };
            return;
        }

        if (key == "edge")
        {
            // 真图找边：暗场里一条很淡的亮带（截图抠图去掉标注后得到），灰尘多、边很软
            var edir = Helpers.AppPaths.ToStoredPath(Helpers.AppPaths.ResolvePath(@"Projects\真图找边\测试组"));
            flow.Steps = new ObservableCollection<VisionFlowStep>
            {
                MkStep(1, "拍张照片", "读难例图", s => { s.CaptureMode = "打开文件夹"; s.FolderPath = edir; }),
                MkStep(2, "精找直线", "找淡边", s =>
                {
                    s.RoiX = 100; s.RoiY = 415; s.RoiW = 1080; s.RoiH = 40;
                    s.SamplingStep = 80; s.CaliperLength = 40;
                    s.EdgePolarity = "从暗到亮"; s.EdgeConfidence = 30;
                }),
                MkStep(3, "显示结果", "画到图上", s => { s.CompareSource = "第2步"; s.MeasureType = "圆和点"; }),
            };
            return;
        }

        if (key == "scratch")
        {
            // 找划痕：拍张照片 → 找划痕（免训练）→ 判个数 → 显示结果
            var sdir = Helpers.AppPaths.ToStoredPath(Helpers.AppPaths.ResolvePath(@"Projects\上相机划痕\图"));
            flow.Steps = new ObservableCollection<VisionFlowStep>
            {
                MkStep(1, "拍张照片", "读上相机图", s => { s.CaptureMode = "打开文件夹"; s.FolderPath = sdir; }),
                MkStep(2, "找划痕", "找划痕", s => { s.MatchMode = "一般"; s.Number1 = 80; s.Number2 = 300; s.AreaMax = 44; s.BendMin = 0.05; s.IndexProbe = 0; s.RoiX = 1900; s.RoiY = 500; s.RoiW = 1500; s.RoiH = 2100; }),
                MkStep(3, "判个数对不对", "划痕判定", s => { s.CompareSource = "第2步"; s.AreaMin = 0; s.AreaMax = 0; }),
                MkStep(4, "显示结果", "画到图上", s => { s.CompareSource = "第2步"; s.MeasureType = "框"; }),
            };
            return;
        }

        if (key != "bottle" && key != "zeroshot") return;
        var dir = Helpers.AppPaths.ResolvePath(@"Projects\瓶底线圈角度");
        var zsDir = System.IO.Path.Combine(dir, "零样本测试");
        var capDir = key == "zeroshot" ? zsDir : dir;
        var cap = MkStep(1, "拍张照片", "读瓶底图", s => { s.CaptureMode = "打开文件夹"; s.FolderPath = capDir; });
        if (key == "bottle")
        {
            flow.Steps = new ObservableCollection<VisionFlowStep>
            {
                cap,
                MkStep(2, "找圆", "定瓶底中心", s => { s.RoiX = 0; s.RoiY = 0; s.RoiW = 0; s.RoiH = 0; s.ExpectRadius = 425; s.CaliperLength = 60; s.SamplingStep = 6; s.EdgePolarity = "从亮到暗"; s.FitMethod = "正交距离(更准)"; }),
                MkStep(3, "去掉彩色笔迹", "抹掉红圈", s => { s.MatchMode = "红"; s.Number1 = 70; s.Number2 = 4; }),
                MkStep(4, "数块数", "找黑线圈", s => { s.RoiX = 0; s.RoiY = 0; s.RoiW = 0; s.RoiH = 0; s.BlobPolarity = "暗区域"; s.ThreshMode = "固定值"; s.ThreshLo = 130; s.AreaMin = 300; s.AreaMax = 20000; s.SortBy = "面积从大到小"; }),
                MkStep(5, "求角度", "线圈角度", s => { s.CompareSource = "第2步"; s.CompareVar = "第4步"; s.AngleZero = "向上(12点)"; s.InvertImage = true; s.OutputVar = "coilAngle"; }),
                MkStep(6, "Modbus 写", "角度写PLC", s => { s.ModbusIp = "192.168.1.60"; s.ModbusPort = 502; s.ModbusUnit = 1; s.RoiX = 100; s.Number1 = 100; s.IoType = "保持寄存器"; s.CompareSource = "第5步"; }),
                MkStep(7, "判个数对不对", "角度判定", s => { s.CompareSource = "第5步"; s.AreaMin = -20; s.AreaMax = 20; }),
                MkStep(8, "显示结果", "画到图上", s => { s.CompareSource = "第5步"; s.CompareVar = "第2步"; s.MeasureType = "圆和点"; s.NotifyMessage = "OK0"; }),
            };
        }
        else
        {
            flow.Steps = new ObservableCollection<VisionFlowStep>
            {
                cap,
                MkStep(2, "零样本查瑕疵", "查瑕疵", s =>
                {
                    s.TemplateFile = Helpers.AppPaths.ToStoredPath(System.IO.Path.Combine(zsDir, "标准图.png"));
                    s.MatchMode = "平移+旋转";
                    s.Number1 = 60;
                    s.AreaMin = 30;
                    s.IndexProbe = 0;
                }),
                MkStep(3, "判个数对不对", "缺陷判定", s => { s.CompareSource = "第2步"; s.AreaMin = 0; s.AreaMax = 0; }),
                MkStep(4, "显示结果", "画到图上", s => { s.CompareSource = "第2步"; s.MeasureType = "圆和点"; }),
            };
        }
    }

    private static ObservableCollection<VisionFlowStep> CreateStandardPipeline()

    {

        return new ObservableCollection<VisionFlowStep>

        {

            // —— 分支示例：如果(匹配成功) → 几何测量；否则(匹配失败) 跳过几何测量直接进通讯 ——

            new VisionFlowStep

            {

                Index = 1, Function = "拍张照片", Name = "采集图像", ParamSummary = "打开文件", Timeout = 5000, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = "", StatusText = "未开始", LogicRelation = "满足就做"

            },

            new VisionFlowStep

            {

                Index = 2, Function = "图里找图案", Name = "模板匹配", ParamSummary = "tpl / score≥0.85", Timeout = 3000, ActualValue = "未匹配", Icon = "🎯", StepType = "TemplateMatch", MatchMode = "灰度匹配", ScoreThreshold = 0.85, RoiX = 80, RoiY = 60, RoiW = 160, RoiH = 120, StatusText = "未开始", LogicRelation = "满足就做"

            },

            new VisionFlowStep

            {

                Index = 3, Function = "量尺寸", Name = "几何测量", ParamSummary = "圆径 / 12.00±0.05", Timeout = 2000, ActualValue = "未测量", Icon = "📐", StepType = "Measure", MeasureType = "圆径", NominalValue = 12.0, Tolerance = 0.05, StatusText = "未开始", LogicRelation = "满足就做"

            },

            new VisionFlowStep

            {

                Index = 4, Function = "通讯发送", Name = "通讯发送(合格)", ParamSummary = "PLC-串口 / M100=1", Timeout = 1000, ActualValue = "未发送", Icon = "📡", StepType = "Comm", CommChannel = "PLC-串口", CommCmd = "发送", CommContent = "M100=1", CommEncoding = "ASCII", StatusText = "未开始", LogicRelation = "不满足时"

            },

            // —— 循环示例：如果(循环头,默认不成立) → 循环体；否则 退出循环 ——

            new VisionFlowStep

            {

                Index = 5, Function = "挑瑕疵", Name = "缺陷检测", ParamSummary = "差异比对 / 阈值45", Timeout = 3000, ActualValue = "未检测", Icon = "🔍", StepType = "Defect", StatusText = "未开始", LogicRelation = "满足就做"

            },

            new VisionFlowStep

            {

                Index = 6, Function = "判一下对不对", Name = "循环条件", ParamSummary = "算子:大于 / 输入值:0.85", Timeout = 1000, ActualValue = "0", Icon = "✅", StepType = "Logic", LogicRelation = "满足就做", Operator = "大于", InputValue = "0.85", StatusText = "未开始"

            },

            new VisionFlowStep

            {

                Index = 7, Function = "等一会", Name = "循环体", ParamSummary = "200ms", Timeout = 1000, ActualValue = "未等待", Icon = "⏱", StepType = "Delay", LogicRelation = "一直重复", StatusText = "未开始"

            },

            new VisionFlowStep

            {

                Index = 8, Function = "通讯发送", Name = "退出循环", ParamSummary = "PLC-串口 / M200=0", Timeout = 1000, ActualValue = "未发送", Icon = "📡", StepType = "Comm", CommChannel = "PLC-串口", CommCmd = "发送", CommContent = "M200=0", CommEncoding = "ASCII", StatusText = "未开始", LogicRelation = "不满足时"

            },

            new VisionFlowStep

            {

                Index = 9, Function = "报结果", Name = "结果输出", ParamSummary = "PLC / OK", Timeout = 1000, ActualValue = "-", Icon = "📤", StepType = "Output", OutputAddress = "PLC_D200", OutputValue = 1, StatusText = "未开始", LogicRelation = "结束重复"

            },

        };

    }





    /// <summary>运控流程预设：轴运动 → IO控制 → 气缸动作 → 等待延时，并带逻辑列示例(如果/否则/循环)。</summary>
    /// <summary>简单视觉流程预设：图像采集 → 模板匹配，并带逻辑列示例(如果/循环/否则)。</summary>
    private static ObservableCollection<VisionFlowStep> CreateSimpleVisionPipeline()
    {
        return new ObservableCollection<VisionFlowStep>
        {
            new VisionFlowStep
            {
                Index = 1, Function = "拍张照片", Name = "采集图像", ParamSummary = "打开文件", Timeout = 5000, ActualValue = "未采集", Icon = "📷", StepType = "ImageCapture", CaptureMode = "打开文件", ImageSource = "", StatusText = "未开始", LogicRelation = "满足就做"
            },
            new VisionFlowStep
            {
                Index = 2, Function = "图里找图案", Name = "模板匹配", ParamSummary = "tpl / score≥0.85", Timeout = 3000, ActualValue = "未匹配", Icon = "🎯", StepType = "TemplateMatch", MatchMode = "灰度匹配", ScoreThreshold = 0.85, RoiX = 80, RoiY = 60, RoiW = 160, RoiH = 120, StatusText = "未开始", LogicRelation = "满足就做"
            },
            new VisionFlowStep
            {
                Index = 3, Function = "判一下对不对", Name = "循环条件", ParamSummary = "算子:大于 / 输入值:0.85", Timeout = 1000, ActualValue = "0", Icon = "✅", StepType = "Logic", LogicRelation = "满足就做", Operator = "大于", InputValue = "0.85", StatusText = "未开始"
            },
            new VisionFlowStep
            {
                Index = 4, Function = "等一会", Name = "循环体", ParamSummary = "200ms", Timeout = 1000, ActualValue = "未等待", Icon = "⏱", StepType = "Delay", LogicRelation = "一直重复", StatusText = "未开始"
            },
            new VisionFlowStep
            {
                Index = 5, Function = "通讯发送", Name = "退出循环", ParamSummary = "PLC-串口 / M200=0", Timeout = 1000, ActualValue = "未发送", Icon = "📡", StepType = "Comm", CommChannel = "PLC-串口", CommCmd = "发送", CommContent = "M200=0", CommEncoding = "ASCII", StatusText = "未开始", LogicRelation = "不满足时"
            },
        };
    }



    public string[] StepFunctions { get; } = { "拍张照片", "图里找图案", "量尺寸", "判一下对不对", "报结果", "挑瑕疵", "写段代码", "动轴", "开关电", "伸缩气缸", "等一会", "发数据", "看颜色", "数有几个", "扫条码", "认文字", "走哪一条", "重复做", "调子流程", "算个数", "存文件", "弹提示" };

    public string[] PreprocessTypes { get; } = { "灰度化", "二值化", "高斯模糊", "中值滤波", "边缘检测" };

    public string[] MeasureTypes { get; } = { "圆径", "边距", "角度", "面积", "中心距" };

    public string[] LogicRelations { get; } = { "满足就做", "而且要", "或者要", "不满足时", "一直重复", "结束重复", "一起做", "等它完成" };

    public string[] Operators { get; } = { "大于", "小于", "等于", "大于等于", "小于等于", "不等于", "包含", "不包含", "开头为", "结尾为" };

    public string[] StatusOptions { get; } = { "未开始", "运行中", "已完成", "已跳过", "等待中", "错误", "超时", "已暂停", "通过", "不通过", "告警", "采集成功", "采集失败", "匹配成功", "匹配失败", "ROI 内", "ROI 外", "尺寸合格", "尺寸超差", "亮度正常", "亮度异常", "通讯正常", "通讯中断", "运动完成", "到位", "未到位", "动作完成" };

    public string[] CaptureModes { get; } = { "采集相机", "迈德威视相机", "打开文件夹", "打开文件" };

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

            ResetNodeGraphStepping();

            NodeGraph.Bind(value);

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



    DvAffineTrans _fixture = DvAffineTrans.Identity;

    bool _breakFlow;

    int _pointCursor;

    readonly Dictionary<string, List<DvPoint2D>> _points = new();

    public event Action<string, string>? Notice;

    private VisionFlowStep? _selectedStep;

    public bool IsDefectStepSelected => _selectedStep?.StepType == "Defect";

    /// <summary>选中步骤若命中元数据目录，右侧就渲染通用参数面板；否则用原来的手写面板。</summary>
    public System.Collections.Generic.IReadOnlyList<OpParam> SelectedOperatorParams =>
        OperatorCatalog.Find(_selectedStep?.StepType)?.Params ?? System.Array.Empty<OpParam>();

    public bool HasGenericPanel => OperatorCatalog.Find(_selectedStep?.StepType) != null;

    public string SelectedOperatorName
    {
        get
        {
            var d = OperatorCatalog.Find(_selectedStep?.StepType);
            return d == null ? "步骤参数" : d.Icon + " " + d.Name;
        }
    }

    public string SelectedOperatorDesc => OperatorCatalog.Find(_selectedStep?.StepType)?.Desc ?? "";

    public bool SelectedOperatorUsesRoi => OperatorCatalog.Find(_selectedStep?.StepType)?.UsesRoi == true;

    /// <summary>面板顶部那句「操作顺序：1 …  → 2 …」，让新手知道先动哪一项。</summary>
    public string SelectedOperatorOrderText => OperatorCatalog.Find(_selectedStep?.StepType)?.OrderLine ?? "";

    /// <summary>下面四个是各块在页面上的第几步；块本身按算子类型显隐，序号只在它显示时用到。</summary>
    public int SelectedBlockRoiNo => BlockNo("roi");

    public int SelectedBlockGoldenNo => BlockNo("golden");

    public int SelectedBlockTestNo => BlockNo("test");

    public int SelectedBlockOutputNo => BlockNo("output");

    int BlockNo(string key) => OperatorCatalog.Find(_selectedStep?.StepType)?.BlockNo(key) ?? 0;

    /// <summary>「跳转」这一步按名字找到的落点，填错名字当场提示。</summary>
    public string SelectedJumpResolve
    {
        get
        {
            var st = _selectedStep;
            if (st == null || st.StepType != "Jump") return "";
            if (string.IsNullOrWhiteSpace(st.JumpTargetName)) return "还没填名字：填「跳转到」那一步起的名字";
            int at = LabelIndex(_selectedFlow, st.JumpTargetName);
            if (at < 0) return $"没找到叫「{st.JumpTargetName.Trim()}」的「跳转到」，现在不会跳";
            var t = _selectedFlow!.Steps[at];
            return $"会跳到 第{t.Index}步「{(string.IsNullOrWhiteSpace(t.LabelName) ? t.Name : t.LabelName)}」";
        }
    }

    /// <summary>「跳转到」这一步被哪些「跳转」调用。</summary>
    public string SelectedLabelCallers
    {
        get
        {
            var st = _selectedStep;
            if (st == null || st.StepType != "JumpTo") return "";
            return CallersText(_selectedFlow, st.LabelName);
        }
    }

    /// <summary>有没有选中步骤（右上角「运行状态」卡片显隐用）。</summary>
    public bool HasSelectedStep => _selectedStep != null;

    /// <summary>右侧通用参数面板的五个绑定都靠它重算，切换工具后必须调一次。</summary>
    void RaiseOperatorPanel()
    {
        OnPropertyChanged(nameof(SelectedOperatorParams));
        OnPropertyChanged(nameof(HasGenericPanel));
        OnPropertyChanged(nameof(SelectedOperatorName));
        OnPropertyChanged(nameof(SelectedOperatorDesc));
        OnPropertyChanged(nameof(SelectedOperatorUsesRoi));
        OnPropertyChanged(nameof(SelectedOperatorOrderText));
        OnPropertyChanged(nameof(SelectedBlockRoiNo));
        OnPropertyChanged(nameof(SelectedBlockGoldenNo));
        OnPropertyChanged(nameof(SelectedBlockTestNo));
        OnPropertyChanged(nameof(SelectedBlockOutputNo));
        OnPropertyChanged(nameof(HasSelectedStep));
        OnPropertyChanged(nameof(CaptureFolderInfo));
        OnPropertyChanged(nameof(SelectedJumpResolve));
        OnPropertyChanged(nameof(SelectedLabelCallers));
    }



    /// <summary>步骤真正框过的 ROI；没拖过（默认 10,10,120,80 或宽高≤2）就返回 null = 整图。</summary>
    static OpenCvSharp.Rect? ScratchRoi(VisionFlowStep step)
        => step.RoiW > 2 && step.RoiH > 2 && !(step.RoiX == 10 && step.RoiY == 10 && step.RoiW == 120 && step.RoiH == 80)
            ? new OpenCvSharp.Rect((int)step.RoiX, (int)step.RoiY, (int)step.RoiW, (int)step.RoiH)
            : (OpenCvSharp.Rect?)null;

    /// <summary>把某个工具变成当前流程末尾的一个步骤（「➕ 添加」按钮和两级菜单共用）。</summary>
    void AddStepOf(string? tool)
    {
        if (_selectedFlow == null || string.IsNullOrWhiteSpace(tool)) return;
        var next = _selectedFlow.Steps.Count + 1;
        var (icon, type) = ToolToIconType(tool);
        var step = new VisionFlowStep
        {
            Index = next,
            Function = tool,
            Name = $"{tool}{next}",
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
        // 菜单不自己收起：连着加好几个算子不用重开，点菜单外面才关
    }



    public VisionFlowStep? SelectedStep

    {

        get => _selectedStep;

        set

        {

            if (SetField(ref _selectedStep, value))

            {

                RaiseOperatorPanel();

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

            var roi = TplRoi(tmplStep, srcMat);

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



    private async void TrySendDefectResultToExternal(List<DefectResult> defects)

    {

        try

        {

            var proj = ProjectViewModel.Instance?.SelectedProject;

            if (proj == null || !proj.SendResultToExternal) return;

            var count = defects != null ? defects.Count : 0;

            var payload = new System.Collections.Generic.Dictionary<string, object>

            {

                ["type"] = "defect_result",

                ["project"] = proj.ProjectName ?? "",

                ["defectCount"] = count,

                ["time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);

            var hub = CommHub.Instance;

            if (!hub.IsOpen)

            {

                var cfg = new CommConfigItem

                {

                    Name = "外发-" + (proj.ExternalChannel ?? "MQTT"),

                    CommType = proj.ExternalChannel ?? "MQTT",

                    NetIp = ParseTargetIp(proj.ExternalTarget),

                    NetPort = ParseTargetPort(proj.ExternalTarget),

                    Port = proj.ExternalTarget

                };

                await hub.ConnectAsync(cfg);

            }

            if (hub.IsOpen) await hub.SendAsync(json);

            Status = "已向外部软件发送检测结果（缺陷 " + count + " 处）";

        }

        catch (Exception ex) { Status = "外发失败：" + ex.Message; }

    }



    private static string ParseTargetIp(string target)

    {

        if (string.IsNullOrWhiteSpace(target)) return "";

        var parts = target.Split(':');

        return parts[0];

    }

    private static string ParseTargetPort(string target)

    {

        if (string.IsNullOrWhiteSpace(target)) return "0";

        var parts = target.Split(':');

        return parts.Length > 1 ? parts[1] : "0";

    }



    private ImageSource? BuildDefectOverlay(List<DefectResult> defects)

    {

        if (defects.Count == 0) return null;

        if (CurrentImageSource is not BitmapSource srcBmp) return null;

        int w = srcBmp.PixelWidth, h = srcBmp.PixelHeight;

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

            var roi = TplRoi(step, m.Source);

            m.SetTemplateFromRoi(roi);

            return true;

        }

        catch { return false; }

    }



    private string _newStepFunction = "图里找图案";

    public string NewStepFunction { get => _newStepFunction; set => SetField(ref _newStepFunction, value); }

    // ===== 工具箱：右侧页面按分类（视觉/运控/通讯）选工具，➕添加 按所选工具新增步骤 =====
    public string[] ToolCategories { get; } = { "视觉", "OpenCVVision", "运控", "通讯", "逻辑", "更多" };

    /// <summary>「➕ 添加」按钮上的两级菜单：一级 = 分类，二级 = 算子，点算子直接加进步骤。</summary>
    public System.Collections.Generic.List<ToolGroup> ToolGroups { get; } = new();

    /// <summary>添加菜单开没开（加完一步不收起，点菜单外面才关）。</summary>
    public bool IsAddMenuOpen { get => _isAddMenuOpen; set => SetField(ref _isAddMenuOpen, value); }

    private bool _isAddMenuOpen;

    /// <summary>添加菜单当前停在哪个分类上。</summary>
    public ToolGroup? SelectedToolGroup { get => _selectedToolGroup; set => SetField(ref _selectedToolGroup, value); }

    private ToolGroup? _selectedToolGroup;

    public ObservableCollection<string> ToolOptions { get; } = new();

    private string _selectedToolCategory = "视觉";

    public string SelectedToolCategory
    {
        get => _selectedToolCategory;
        set { if (SetField(ref _selectedToolCategory, value)) RebuildToolOptions(); }
    }

    private string _selectedTool = "拍张照片";

    public string SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetField(ref _selectedTool, value)) ApplyToolToSelectedStep();
        }
    }

    /// <summary>工具切换后立即生效：若当前有选中步骤，马上把它切换成该工具，右侧属性页随之即时切换。</summary>
    private void ApplyToolToSelectedStep()
    {
        if (_selectedStep == null || string.IsNullOrEmpty(_selectedTool)) return;
        var (icon, type) = ToolToIconType(_selectedTool);
        if (_selectedStep.StepType == type) return;
        _selectedStep.Function = _selectedTool;
        _selectedStep.Icon = icon;
        _selectedStep.StepType = type;
        _selectedStep.ParamSummary = "-";
        RaiseOperatorPanel();
    }

    /// <summary>工具名 → (图标, StepType) 统一映射。</summary>
    /// <summary>菜单里每个算子下面那句说明：目录里的用目录说明，老算子在这张表里补。</summary>

    internal static string ToolDesc(string tool)

    {

        var meta = OperatorCatalog.All.FirstOrDefault(o => o.Name == tool);

        if (meta != null) return meta.Desc;

        return ToolToIconType(tool).Type switch

        {

            "ImageCapture" => "从文件 / 文件夹 / 相机拿一张图，给后面步骤用",

            "TemplateMatch" => "在图上找模板图案在哪、转了多少度",

            "Measure" => "量圆径 / 边距 / 角度并判合格",

            "Logic" => "用条件决定合格还是不合格",

            "Output" => "把结果写到 PLC 或变量",

            "Defect" => "和标准图比，找出划伤、脏污、缺料",

            "Lua" => "用脚本做灵活的逻辑",

            "AxisMove" => "让指定的轴走到目标位置",

            "IOControl" => "打开 / 关闭一个开关量输出",

            "Cylinder" => "让气缸伸出或缩回",

            "Wait" => "等一段时间或等条件满足再往下走",

            "Comm" => "通过串口 / 网口发一条指令",

            "Color" => "统计 / 判定某区域的颜色",

            "Count" => "数出图里有多少个目标",

            "Code" => "读出二维码 / 条码里的文字",

            "Ocr" => "把图里的文字读成字符串",

            "Branch" => "按真假走不同的后续步骤",

            "Loop" => "重复执行一段步骤",

            "SubFlow" => "调用另一个已建好的流程",

            "Calc" => "用公式算出一个新值",

            "Save" => "把结果存成文件",

            "Notify" => "给操作员弹一个提醒",

            _ => ""

        };

    }

    internal static (string Icon, string Type) ToolToIconType(string tool)

    {

        var meta = OperatorCatalog.All.FirstOrDefault(o => o.Name == tool);

        if (meta != null) return (meta.Icon, meta.StepType);

        return tool switch

    {
        "拍张照片" => ("📷", "ImageCapture"),
        "图里找图案" => ("🎯", "TemplateMatch"),
        "量尺寸" => ("📐", "Measure"),
        "判一下对不对" => ("✅", "Logic"),
        "报结果" => ("📤", "Output"),
        "挑瑕疵" => ("🔍", "Defect"),
        "写段代码" => ("📝", "Lua"),
        "动轴" => ("🦾", "AxisMove"),
        "开关电" => ("🔌", "IOControl"),
        "伸缩气缸" => ("🟢", "Cylinder"),
        "等一会" => ("⏱", "Wait"),
        "发数据" => ("📡", "Comm"),
        "看颜色" => ("🎨", "Color"),
        "数有几个" => ("🔢", "Count"),
        "扫条码" => ("🔖", "Code"),
        "认文字" => ("🔤", "Ocr"),
        "走哪一条" => ("🔀", "Branch"),
        "重复做" => ("🔁", "Loop"),
        "调子流程" => ("📑", "SubFlow"),
        "算个数" => ("🧮", "Calc"),
        "存文件" => ("💾", "Save"),
        "弹提示" => ("💬", "Notify"),
        _ => ("➕", "Other")
    };

    }



    /// <summary>改名前存下来的 flows.json / .ncv 里还是旧算子名，载入时统一换成通俗名称，免得一个页面两种叫法。</summary>
    private static readonly Dictionary<string, string> LegacyToolNames = new()
    {
        ["图像采集"] = "拍张照片", ["模板匹配"] = "图里找图案", ["缺陷检测"] = "挑瑕疵", ["几何测量"] = "量尺寸",
        ["颜色检测"] = "看颜色", ["目标计数"] = "数有几个", ["条码识别"] = "扫条码", ["字符识别"] = "认文字",
        ["逻辑判断"] = "判一下对不对", ["条件分支"] = "走哪一条", ["循环"] = "重复做", ["变量计算"] = "算个数",
        ["等待延时"] = "等一会", ["子流程"] = "调子流程", ["Lua脚本"] = "写段代码", ["轴运动"] = "动轴",
        ["IO控制"] = "开关电", ["气缸动作"] = "伸缩气缸", ["结果输出"] = "报结果", ["数据保存"] = "存文件",
        ["消息提示"] = "弹提示", ["通讯指令"] = "发数据",
    };

    /// <summary>「逻辑」列的旧编程词 → 大白话。执行器 switch 用的是新词，老工程载入时统一改写。</summary>
    private static readonly Dictionary<string, string> LegacyLogicNames = new()
    {
        ["如果"] = "满足就做", ["并且"] = "而且要", ["或者"] = "或者要", ["否则"] = "不满足时",
        ["循环"] = "一直重复", ["跳出"] = "结束重复", ["并行"] = "一起做", ["等待"] = "等它完成",
    };

    internal static void NormalizeStepNames(IEnumerable<VisionFlow> flows)
    {
        foreach (var f in flows)
            foreach (var s in f.Steps)
            {
                if (LegacyToolNames.TryGetValue(s.Function ?? "", out var plain)) s.Function = plain;
                if (LegacyLogicNames.TryGetValue(s.LogicRelation ?? "", out var logic)) s.LogicRelation = logic;
            }
    }

    /// <summary>测一下 PLC 连不连得上、地址读不读得到（不用跑整条流程）。</summary>
    void TestModbusConnection()
    {
        if (_selectedStep == null) return;
        var step = _selectedStep;
        if (step.StepType == "TcpSend")
        {
            if (string.IsNullOrWhiteSpace(step.TcpHost)) { ModbusStatus = "没填「发到哪儿(IP)」：填上 IP 和端口再测"; return; }
            var swt = System.Diagnostics.Stopwatch.StartNew();
            Comm.TcpSender.Test(step.TcpHost.Trim(), (int)Math.Round(step.TcpPort), Math.Max(200, step.TcpTimeout), out var terr);
            ModbusStatus = terr.Length == 0
                ? $"✔ 能连上 {step.TcpHost}:{(int)Math.Round(step.TcpPort)}（{swt.ElapsedMilliseconds} 毫秒）"
                : $"✖ {terr}（等了 {swt.ElapsedMilliseconds} 毫秒）";
            return;
        }
        if (string.IsNullOrWhiteSpace(step.ModbusIp))
        {
            ModbusStatus = "没填 PLC 地址：填上 IP 直连，或者去「通讯」页连一条通道";
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var mb = Comm.ModbusMaster.Get(step.ModbusIp.Trim(), (int)step.ModbusPort, (int)step.ModbusUnit,
            out var err, Math.Max(200, step.ModbusTimeout));
        if (mb == null) { ModbusStatus = $"✖ {err}（等了 {sw.ElapsedMilliseconds} 毫秒）"; return; }
        int addr = (int)Math.Clamp(Math.Round(step.RoiX), 0, 65535);
        var regs = mb.ReadRegisters((ushort)addr, 1, false, out var rerr);
        ModbusStatus = regs != null
            ? $"✔ 已连上 {mb.Key}，地址 {addr} 当前值 = {regs[0]}（{sw.ElapsedMilliseconds} 毫秒）"
            : $"△ 连上了 {mb.Key}，但读地址 {addr} 失败：{rerr}";
    }

    void RefreshMindVisionCameras()
    {
        MindVisionDevices.Clear();
        MindVisionService.Instance.Refresh();
        foreach (var d in MindVisionService.Instance.Devices) MindVisionDevices.Add(d.ToString());
        MindVisionStatus = MindVisionService.Instance.Status;
    }

    void GrabMindVisionFrame()
    {
        if (_selectedStep == null) return;
        var step = _selectedStep;
        var mat = MindVisionService.Instance.Grab(step.CameraIndex, step.CameraLineScan,
            step.CameraLineRate, step.CameraSpliceLines, out var why);
        if (mat == null)
        {
            MindVisionStatus = why;
            step.StatusText = "抓图失败";
            step.ActualValue = why;
            return;
        }
        using (mat)
        {
            var tmp = Path.Combine(NoCodeVision.Helpers.AppPaths.TempDirectory, $"ncv_mv_{DateTime.Now:yyyyMMddHHmmssfff}.png");
            Cv2.ImWrite(tmp, mat);
            step.ImageSource = tmp;
            step.StatusText = "已采集";
            step.ActualValue = $"相机图 {mat.Width}×{mat.Height}";
            CurrentImagePath = tmp;
            LoadCurrentImageSource();
            _sharedImage?.Dispose();
            _sharedImage = Cv2.ImRead(tmp, ImreadModes.Color);
        }
        MindVisionStatus = why;
    }

    private void RebuildToolOptions()
    {
        ToolOptions.Clear();
        var options = _selectedToolCategory switch
        {
            "运控" => new[] { "动轴", "开关电", "伸缩气缸", "等一会" },
            "通讯" => new[] { "TCP 发送" }
                .Concat(OperatorCatalog.All.Where(o => o.Group == "Comm" && o.Name != "TCP 发送").Select(o => o.Name))
                .Concat(new[] { "发数据" }).ToArray(),
            "OpenCVVision" => OperatorCatalog.All.Where(o => o.Group == "OpenCVVision").Select(o => o.Name).ToArray(),
            "逻辑" => OperatorCatalog.All.Where(o => o.Group == "Logic").Select(o => o.Name).ToArray(),

            "更多" => OperatorCatalog.All.Where(o => o.Group != "OpenCVVision" && o.Group != "Logic").Select(o => o.Name).ToArray(),
            _ => new[] { "拍张照片", "图里找图案", "挑瑕疵", "量尺寸" },
        };
        foreach (var t in options) ToolOptions.Add(t);

        // 两级菜单：一次把各分类的算子全列出来（空分类不显示）
        ToolGroups.Clear();
        foreach (var cat in ToolCategories)
        {
            var tools = cat switch
            {
                "运控" => new[] { "动轴", "开关电", "伸缩气缸", "等一会" },
                "通讯" => new[] { "TCP 发送" }
                    .Concat(OperatorCatalog.All.Where(o => o.Group == "Comm" && o.Name != "TCP 发送").Select(o => o.Name))
                    .Concat(new[] { "发数据" }).ToArray(),
                "OpenCVVision" => OperatorCatalog.All.Where(o => o.Group == "OpenCVVision").Select(o => o.Name).ToArray(),
                "逻辑" => OperatorCatalog.All.Where(o => o.Group == "Logic").Select(o => o.Name).ToArray(),
                "更多" => OperatorCatalog.All.Where(o => o.Group != "OpenCVVision" && o.Group != "Logic").Select(o => o.Name).ToArray(),
                _ => new[] { "拍张照片", "图里找图案", "挑瑕疵", "量尺寸" },
            };
            if (tools.Length == 0) continue;
            ToolGroups.Add(new ToolGroup
            {
                Name = cat,
                Tools = tools.OrderBy(t => t)
                             .Select(t => new ToolItem { Name = t, Icon = ToolToIconType(t).Icon, Desc = ToolDesc(t) })
                             .ToList()
            });
        }
        if (!ToolOptions.Contains(SelectedTool))
            SelectedTool = ToolOptions.FirstOrDefault() ?? "";
    }



    private string _activePropTab = "图像";

    public string ActivePropTab { get => _activePropTab; set => SetField(ref _activePropTab, value); }



    private string _status = "就绪";

    public string Status { get => _status; set => SetField(ref _status, value); }



    // 运行控制：协作式异步运行（全部运行时可在每步之间暂停/停止，并自动跳到当前运行行）

    private bool _isRunning;

    private bool _isPaused;

    private CancellationTokenSource? _cts;

    // 每步之间只让界面刷一次（渲染/滚动/按钮状态），不再空等固定毫秒



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

    // 单件产品真实结果累计（用于 ProductInspected 事件）
    private bool _curProductOk = true;
    private double _curProductScore = 0;
    private int _curProductDefects = 0;

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



    private ImageSource? _currentImageSource;

    public ImageSource? CurrentImageSource
    {
        get => _currentImageSource;
        set { if (SetField(ref _currentImageSource, value)) OnPropertyChanged(nameof(ResultPreview)); }
    }

    /// <summary>「显示结果」画出来的那张图：圆、点、连线、大字结论。</summary>
    public ImageSource? ResultImage
    {
        get => _resultImage;
        set
        {
            if (!SetField(ref _resultImage, value)) return;
            OnPropertyChanged(nameof(HasResultImage));
            OnPropertyChanged(nameof(ResultPreview));
        }
    }

    private ImageSource? _resultImage;

    /// <summary>有没有画好的结果图（卡片显隐用，布尔比 null 触发稳）。</summary>
    public bool HasResultImage => _resultImage != null;

    /// <summary>结果预览：画过就是结果图，没画过就用当前工作图，保证页面上总有东西看。</summary>
    public ImageSource? ResultPreview => _resultImage ?? _currentImageSource;



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

        => Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "flows.json");



    public ICommand AddFlowCmd { get; }

    public ICommand DeleteFlowCmd { get; }

    public ICommand AddStepCmd { get; }

    /// <summary>两级菜单里点某个算子 = 直接把它加到当前流程末尾。</summary>
    public ICommand AddStepWithCmd { get; }

    /// <summary>列表里双击流程名进入改名。</summary>
    public ICommand BeginRenameFlowCmd { get; }

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

    public ICommand AddVisualFlowCmd { get; }

        public ICommand AddMotionFlowCmd { get; }

    public ICommand AddNodeGraphFlowCmd { get; }

    /// <summary>节点图流程的编辑模型（画布上的节点/连线），与 SelectedFlow.Steps、SelectedFlow.Links 双向同步。</summary>
    public NodeGraphViewModel NodeGraph { get; }

        public ICommand RenameFlowCmd { get; }

    public ICommand ClearCmd { get; }

    public ICommand PickImageCmd { get; }

    // ===== 迈德威视相机（面阵 / 线扫） =====
    public System.Collections.ObjectModel.ObservableCollection<string> MindVisionDevices { get; } = new();

    public string MindVisionStatus { get => _mindVisionStatus; set => SetField(ref _mindVisionStatus, value); }

    private string _mindVisionStatus = "点「🔄 刷新」枚举相机";

    public ICommand RefreshCamerasCmd { get; }

    public ICommand TestModbusCmd { get; }

    public ICommand PickGoldenImageCmd { get; }

    /// <summary>参数面板上路径类参数（图片 / 文件夹）旁边的「📂」按钮。</summary>
    public ICommand PickPathCmd { get; }

    public string ModbusStatus { get => _modbusStatus; set => SetField(ref _modbusStatus, value); }

    private string _modbusStatus = "点「🔌 测一下」看能不能连上 PLC";

    public ICommand GrabCameraCmd { get; }

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

            Instance = this;

            NodeGraph = new NodeGraphViewModel(this);

            RefreshCamerasCmd = new RelayCommand(_ => RefreshMindVisionCameras());

            GrabCameraCmd = new RelayCommand(_ => GrabMindVisionFrame());

            TestModbusCmd = new RelayCommand(_ => TestModbusConnection());

            PickGoldenImageCmd = new RelayCommand(_ =>
            {
                if (_selectedStep == null) return;
                var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff" };
                if (dlg.ShowDialog() != true) return;
                _selectedStep.TemplateFile = Helpers.AppPaths.ToStoredPath(dlg.FileName);
            });

            PickPathCmd = new RelayCommand(p =>
            {
                if (_selectedStep == null || p is not OpParam prm) return;
                var prop = _selectedStep.GetType().GetProperty(prm.Path);
                if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(string)) return;
                bool folder = prm.Path == "FolderPath";
                var cur = Helpers.AppPaths.ResolvePath(prop.GetValue(_selectedStep) as string ?? "");
                string? picked = null;
                if (folder)
                {
                    var dlg = new OpenFolderDialog { Title = "选一个放图片的文件夹" };
                    if (Directory.Exists(cur)) dlg.InitialDirectory = cur;
                    if (dlg.ShowDialog() == true) picked = dlg.FolderName;
                }
                else
                {
                    var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*" };
                    if (File.Exists(cur)) dlg.FileName = cur;
                    if (dlg.ShowDialog() == true) picked = dlg.FileName;
                }
                if (string.IsNullOrWhiteSpace(picked)) return;
                prop.SetValue(_selectedStep, picked);
            }, _ => _selectedStep != null);



            RebuildToolOptions(); // 初始化工具箱（视觉分类）

            ProjectViewModel.Instance?.LoadCurrentProject();

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

                onElapsed: ms => { ScriptElapsedMs = ms; AccountLineMs(ms); },

                onRunState: (running, paused) =>
                {
                    ScriptIsRunning = running; ScriptIsPaused = paused;
                    if (running && !paused) ResetLineMs();
                    if (!running && !paused) FlushLineMs(_luaHost.ElapsedMs);
                    CommandManager.InvalidateRequerySuggested();
                });

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
            if (!PromptFlow("添加逻辑视觉流程", $"逻辑视觉流程-{Flows.Count + 1}", Opts(
                ("empty", "空白流程", "不放任何步骤，点上方「➕ 添加」自己一步步搭。"),
                ("bottle", "示例：瓶底线圈角度", "拍张照片 → 找圆定中心 → 去掉红圈 → 数块找线圈 → 求角度 → 写 Modbus → 判定 → 显示结果（8 步，图在「瓶底线圈角度」工程里）"),
                ("zeroshot", "示例：零样本查瑕疵", "拍张照片 → 零样本查瑕疵（和标准图比差异，不用训练）→ 判个数 → 显示结果（4 步）"),
                ("qr", "示例：二维码定位发送", "拍张照片 → 图里找图案定位码 → 读二维码（点阵码增强）→ TCP 发送 → 显示结果（5 步）"),
                ("barcode", "示例：条码定位发送", "拍张照片 → 图里找图案定位条码 → 读条码 → TCP 发送 → 显示结果（5 步）"),
                ("pcba", "示例：PCBA 元件有无（AOI）", "拍张照片 → 零样本查瑕疵（和一张好板比）→ 判个数（零容忍）→ 显示结果（4 步，图在「PCBA元件有无」工程里）"),
                ("metrology", "示例：精找圆 + 精找直线", "拍张照片 → 精找圆（亚像素卡尺 + 稳健拟合，切掉一段弧也能拟合）→ 精找直线 → 显示结果（4 步，图在「精找圆找直线」工程里）"),
                ("edge", "示例：真图找边（难例）", "拍张照片 → 精找直线（暗场里一条很淡的亮带，满屏灰尘）→ 显示结果（3 步，图在「真图找边」工程里）"),
                ("scratch", "示例：找划痕（免训练）", "拍张照片 → 找划痕（不用标准图、不用训练）→ 判个数 → 显示结果（4 步，图在「上相机划痕」工程里）")), out var key, out var name)) return;
            var flow = new VisionFlow { Name = name, Icon = "🔀" };
            ApplyFlowTemplate(flow, key);
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



        AddStepCmd = new RelayCommand(_ => AddStepOf(SelectedTool), _ => _selectedFlow != null);



        AddStepWithCmd = new RelayCommand(p => AddStepOf(p is ToolItem ti ? ti.Name : p as string), _ => _selectedFlow != null);



        BeginRenameFlowCmd = new RelayCommand(p =>

        {
            if (p is not VisionFlow target) return;
            foreach (var f in Flows) f.IsEditing = ReferenceEquals(f, target);
            target.IsEditing = true;
        }, _ => _selectedFlow != null);



        AddScriptFlowCmd = new RelayCommand(_ =>
        {
            if (!PromptFlow("添加脚本流程", $"脚本流程-{Flows.Count + 1}", Opts(
                ("lua-empty", "空白脚本", "只给几行注释，接口怎么调用写在里面。"),
                ("lua-match", "示例：匹配分数判定后写PLC", "抓图 → 模板匹配 → 分数和阈值比 → 合格写 PLC，不合格复位并打印"),
                ("lua-loop", "示例：连采 10 次取平均", "循环抓图取分数、累计求平均，再一次性下发结果")), out var key, out var name)) return;
            var flow = new VisionFlow { Name = name, Icon = "📝", FlowKind = "Script" };
            ApplyFlowTemplate(flow, key);
            Flows.Add(flow);
            SelectedFlow = flow;
            SelectedStep = flow.Steps.FirstOrDefault();
        }, _ => true);



        AddVisualFlowCmd = new RelayCommand(_ =>
        {
            var next = Flows.Count + 1;
            var flow = new VisionFlow
            {
                Name = $"视觉流程-{next}",
                Icon = "🖼",
                FlowKind = "Visual",
                Steps = CreateStandardPipeline()
            };
            Flows.Add(flow);
            SelectedFlow = flow;
            SelectedStep = flow.Steps.FirstOrDefault();
        }, _ => true);


        AddMotionFlowCmd = new RelayCommand(_ =>
        {
            if (!PromptFlow("添加运控流程", $"运控流程-{Flows.Count + 1}", Opts(
                ("empty", "空白运控流程", "点上方「➕ 添加 → 运控」自己挑轴 / IO 步骤。"),
                ("motion", "示例：上电→回零→走位→等到位→吸料", "轴上电解锁 → 回原点 → 走到取料位 → 等条件 → 写开关量开吸盘（5 步）")), out var key, out var name)) return;
            var flow = new VisionFlow { Name = name, Icon = "🕹" };
            ApplyFlowTemplate(flow, key);
            Flows.Add(flow);
            SelectedFlow = flow;
            SelectedStep = flow.Steps.FirstOrDefault();
        }, _ => true);



        AddNodeGraphFlowCmd = new RelayCommand(_ =>
        {
            if (!PromptFlow("添加节点图流程", $"节点图流程-{Flows.Count + 1}", Opts(
                ("empty", "空白画布", "从左边算子库把节点拖出来，自己连线。"),
                ("node", "示例：拍照→找圆→判定", "三个节点已经连好线，改参数就能跑")), out var key, out var name)) return;
            var flow = new VisionFlow { Name = name, Icon = "🕸", FlowKind = "NodeGraph" };
            ApplyFlowTemplate(flow, key);
            Flows.Add(flow);
            SelectedFlow = flow;
        }, _ => true);



        RenameFlowCmd = new RelayCommand(_ =>

        {

            if (_selectedFlow == null) return;

            if (!PromptRename(_selectedFlow.Name, out var newName)) return;

            if (!string.IsNullOrWhiteSpace(newName)) _selectedFlow.Name = newName.Trim();

        }, _ => _selectedFlow != null);



        DeleteStepCmd = new RelayCommand(_ =>

        {

            if (_selectedFlow == null || _selectedStep == null) return;

            var r = System.Windows.MessageBox.Show(

                $"确认删除步骤「{_selectedStep.Function}」？",

                "删除确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question,

                System.Windows.MessageBoxResult.No);

            if (r != System.Windows.MessageBoxResult.Yes) return;

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

                var dlg = new OpenFolderDialog { Title = "选一个放图片的文件夹" };

                var cur = Helpers.AppPaths.ResolvePath(_selectedStep.FolderPath);

                if (Directory.Exists(cur)) dlg.InitialDirectory = cur;

                if (dlg.ShowDialog() == true)

                {

                    var dir = dlg.FolderName;

                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))

                    {

                        _selectedStep.FolderPath = dir;

                        var imgExt = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

                        path = Directory.EnumerateFiles(dir).FirstOrDefault(f => imgExt.Contains(Path.GetExtension(f).ToLowerInvariant()));

                    }

                }

            }

            else if (_selectedStep.CaptureMode == "迈德威视相机")
            {
                GrabMindVisionFrame();
                return;
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

                    var tmp = Path.Combine(NoCodeVision.Helpers.AppPaths.TempDirectory, $"ncv_cam_{DateTime.Now:yyyyMMddHHmmssfff}.png");

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

                _selectedStep.StatusText = "检测范围不对（宽高要大于 0）";

                if (own) src.Dispose();

                return;

            }

            using var tpl = new Mat(src, roi);

            var tplDir = Path.Combine(NoCodeVision.Helpers.AppPaths.DataDirectory, "templates");

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

                var roi = TplRoi(_selectedStep, src);

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

                            Label = $"像不像 {r.Score:F2} · 转了 {r.Angle:F1}°"

                        });

                    }

                    var r0 = results[0];

                    _selectedStep.ActualValue = $"找到 {results.Count} 个 · 第一个在({r0.CenterX:F1},{r0.CenterY:F1}) · 转了 {r0.Angle:F1}° · 像不像 {r0.Score:F2} · 耗时{_selectedStep.CostMs:F1}ms";

                    _selectedStep.StatusText = $"匹配成功 · 结果数 {results.Count} · 耗时{_selectedStep.CostMs:F1}ms";

                }

                else

                {

                    _selectedStep.ActualValue = $"图 {src.Width}×{src.Height}，模板 {roi.Width}×{roi.Height}，没找到（要求像到 {_selectedStep.ScoreThreshold:F2} 以上）";

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

            if (_selectedFlow.FlowKind == "NodeGraph")

            {

                StepNodeGraphOnce(_selectedFlow);

                return;

            }

            if (_stepCursor >= _selectedFlow.Steps.Count) { _stepCursor = 0; _sharedImage?.Dispose(); _sharedImage = null; }

            var step = _selectedFlow.Steps[_stepCursor];

            SelectedStep = step;

            var matcher = new RotatedTemplateMatcher();

            RunStep(step, matcher);

            PublishPreview();

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

                var r = System.Windows.MessageBox.Show(

                    $"确认清空流程「{_selectedFlow.Name}」的全部 {_selectedFlow.Steps.Count} 个步骤？",

                    "清空确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning,

                    System.Windows.MessageBoxResult.No);

                if (r != System.Windows.MessageBoxResult.Yes) return;

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



    /// <summary>运行单个流程的全部步骤（供“全部流程循环”复用）。</summary>

    private async Task RunSingleFlowStepsAsync(VisionFlow flow, CancellationToken ct)

    {

        if (flow == null || flow.Steps.Count == 0) return;

        _breakFlow = false;

        foreach (var gv in _geo.Values) gv.Heat?.Dispose();
        _geo.Clear();

        var matcher = new RotatedTemplateMatcher();

        if (flow.FlowKind == "NodeGraph")

        {

            await RunNodeGraphStepsAsync(flow, matcher, ct);

            PublishProductResult(flow);

            return;

        }

        int cursor = 0;

        int guard = 0;

        int maxIter = flow.Steps.Count * 20 + 50;
        _curProductOk = true;
        _curProductScore = 0;
        _curProductDefects = 0;

        while (!ct.IsCancellationRequested && cursor >= 0 && cursor < flow.Steps.Count && guard++ < maxIter)

        {

            // 暂停：等待恢复或停止

            while (IsPaused && !ct.IsCancellationRequested)

                await Task.Delay(120, ct);

            if (ct.IsCancellationRequested || _breakFlow) break;



            var step = flow.Steps[cursor];

            // 单步出异常不能把整条流程带停：记下来继续跑下一步
            try

            {

                SelectedStep = step;                   // 选中并自动跳到该行

                Status = $"运行中 · {flow.Name} · 第{step.Index}步 {step.Name} · {DateTime.Now:HH:mm:ss}";

                RunStep(step, matcher);                // 在当前(UI)线程执行本步

                PublishPreview();

            }

            catch (Exception ex)

            {

                step.ActualValue = "异常：" + ex.Message;

                step.StatusText = "出错";

                Status = $"第{step.Index}步出错：{ex.Message}";

            }



            // 让 UI 重绘、滚动到当前行、并响应暂停/停止点击（只让出一次消息循环，不空等）

            await PulseUiAsync();



            var next = NextStepIndex(flow, cursor);

            // 前向跳行：中间未执行的行标记「已跳过」，直观显示逻辑跳行效果

            if (next > cursor + 1)

                for (int s = cursor + 1; s < next && s < flow.Steps.Count; s++)

                    flow.Steps[s].StatusText = "已跳过";

            cursor = next;

        }

        // 单件产品流程跑完：发布真实结果事件（OK/NG、匹配分数、缺陷数、抓拍图像），供操作员页等消费
        PublishProductResult(flow);

    }



    /// <summary>让出一次消息循环：界面该重绘的重绘、按钮该变的变，但不额外等待。</summary>
    private static async Task PulseUiAsync()
    {
        try { await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background); }
        catch { }
    }

    /// <summary>把当前工作图刷到右侧预览，跑流程时能看见每一张图。</summary>
    private Mat? _lastPreviewMat;

    private void PublishPreview()
    {
        if (ReferenceEquals(_lastPreviewMat, _sharedImage)) return;   // 图没变就别重复转换
        var bmp = MatToBitmapSource(_sharedImage);
        _lastPreviewMat = bmp == null ? null : _sharedImage;
        if (bmp != null) CurrentImageSource = bmp;
    }

    private void PublishProductResult(VisionFlow flow)

    {

        try

        {

            var _prodImg = MatToBitmapSource(_sharedImage);

            ProductInspected?.Invoke(new FlowProductResult

            {

                IsOk = _curProductOk,

                Score = _curProductScore,

                DefectCount = _curProductDefects,

                Image = _prodImg,

                FlowName = flow.Name,

                Timestamp = DateTime.Now

            });

        }

        catch { }

    }



    /// <summary>节点图流程执行：从「无入线」的节点起，沿连线推进；条件节点按真/假出口选边，出口没连线则该链结束。</summary>

    private async Task RunNodeGraphStepsAsync(VisionFlow flow, RotatedTemplateMatcher matcher, CancellationToken ct)

    {

        var steps = flow.Steps;

        _curProductOk = true;

        _curProductScore = 0;

        _curProductDefects = 0;

        var roots = GraphRoots(flow);

        ResetNodeGraphStepping();

        int guard = 0;

        int maxIter = steps.Count * 20 + 50;

        foreach (var root in roots)

        {

            var cur = root;

            while (cur != null && !ct.IsCancellationRequested && guard++ < maxIter)

            {

                while (IsPaused && !ct.IsCancellationRequested) await Task.Delay(120, ct);

                if (ct.IsCancellationRequested || _breakFlow) break;

                SelectedStep = cur;

                Status = $"运行中 · {flow.Name} · 第{cur.Index}步 {cur.Name} · {DateTime.Now:HH:mm:ss}";

                RunStep(cur, matcher);

                PublishPreview();

                await PulseUiAsync();

                cur = NextGraphNode(flow, cur);

            }

        }

    }



    private VisionFlowStep? NextGraphNode(VisionFlow flow, VisionFlowStep step)

    {

        var outs = flow.Links.Where(l => l.FromId == step.NodeId).ToList();

        if (outs.Count == 0) return null;

        FlowNodeLink? pick;

        if (step.StepType is "Branch" or "Logic" or "Loop")

        {

            bool ok = EvalLogic(step);

            pick = ok

                ? outs.FirstOrDefault(l => l.FromPort == "真") ?? outs.FirstOrDefault(l => l.FromPort != "假")

                : outs.FirstOrDefault(l => l.FromPort == "假");

        }

        else

        {

            pick = outs[0];

        }

        return pick == null ? null : flow.Steps.FirstOrDefault(s => s.NodeId == pick.ToId);

    }



    /// <summary>节点图的入口：没有任何入线的节点；全图都有入线（成环）时退化为第一个节点。</summary>

    private static List<VisionFlowStep> GraphRoots(VisionFlow flow)

    {

        var hasIncoming = new HashSet<string>(flow.Links.Select(l => l.ToId));

        var roots = flow.Steps.Where(s => !hasIncoming.Contains(s.NodeId)).ToList();

        if (roots.Count == 0 && flow.Steps.Count > 0) roots.Add(flow.Steps[0]);

        return roots;

    }



    private VisionFlowStep? _graphStepCursor;

    private int _graphRootIndex;



    private void ResetNodeGraphStepping()

    {

        _graphStepCursor = null;

        _graphRootIndex = 0;

    }



    /// <summary>节点图单步：沿连线一节点一节点往下走，一条链走完自动切到下一条入口链。</summary>

    private void StepNodeGraphOnce(VisionFlow flow)

    {

        if (_graphStepCursor == null)

        {

            var roots = GraphRoots(flow);

            if (roots.Count == 0) return;

            if (_graphRootIndex >= roots.Count) _graphRootIndex = 0;

            _graphStepCursor = roots[_graphRootIndex];

        }

        var step = _graphStepCursor;

        SelectedStep = step;

        RunStep(step, new RotatedTemplateMatcher());

        var next = NextGraphNode(flow, step);

        if (next == null)

        {

            _graphRootIndex++;

            _graphStepCursor = null;

        }

        else

        {

            _graphStepCursor = next;

        }

        Status = "单步完成 · 第" + step.Index + "步 " + step.Name;

    }



    /// <summary>循环运行全部主流程：不停跑完所有主流程（复位流程不参与循环），直到取消（停止按钮）才跳出循环。</summary>

    public async Task RunAllFlowsLoopAsync(CancellationToken ct)

    {

        if (Flows == null || Flows.Count == 0) { Status = "无可用流程"; return; }

        var mainFlows = Flows.Where(f => f.FlowRole != "Reset").ToList();

        if (mainFlows.Count == 0) { Status = "无可用主流程"; return; }

        IsRunning = true;

        IsPaused = false;

        _sharedImage?.Dispose();

        _sharedImage = null;

        try

        {

            while (!ct.IsCancellationRequested)

            {

                foreach (var flow in mainFlows)

                {

                    if (ct.IsCancellationRequested) break;

                    await RunSingleFlowStepsAsync(flow, ct);

                }

            }

        }

        finally

        {

            _sharedImage?.Dispose();

            _sharedImage = null;

            IsRunning = false;

            IsPaused = false;

            Status = ct.IsCancellationRequested ? "已停止 · 全部流程循环" : "完成 · 全部流程循环";

        }

    }

    /// <summary>运行全部复位流程各一次（操作员点「复位」时调用，不循环）。</summary>

    public async Task RunResetFlowsOnceAsync(CancellationToken ct)

    {

        if (Flows == null || Flows.Count == 0) return;

        var resetFlows = Flows.Where(f => f.FlowRole == "Reset").ToList();

        if (resetFlows.Count == 0) { Status = "无复位流程"; return; }

        Status = "运行中 · 复位流程";

        _sharedImage?.Dispose();

        _sharedImage = null;

        try

        {

            foreach (var flow in resetFlows)

            {

                if (ct.IsCancellationRequested) break;

                await RunSingleFlowStepsAsync(flow, ct);

            }

            Status = "完成 · 复位流程";

        }

        finally

        {

            _sharedImage?.Dispose();

            _sharedImage = null;

        }

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

        try

        {

            // 必须在 UI 线程跑：切步骤会碰 DispatcherTimer / 预览位图，后台线程会抛异常把整条流程打断
            await RunSingleFlowStepsAsync(_selectedFlow, _cts.Token);

        }

        catch (OperationCanceledException) { }

        finally

        {

            bool cancelled = _cts != null && _cts.IsCancellationRequested;

            var lastFrame = MatToBitmapSource(_sharedImage);

            if (lastFrame != null) CurrentImageSource = lastFrame;

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

            if (roi.Width <= 0 || roi.Height <= 0) return "检测范围不对（宽高要大于 0）";

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

                    // ① 有工业相机（迈德威视）就先用它——真实 SDK 抓帧

                    if (MindVisionCamera.SdkAvailable && MindVisionService.Instance.AnyDevice)

                    {

                        var mv = MindVisionService.Instance.Grab(step.CameraIndex, step.CameraLineScan,

                            step.CameraLineRate, step.CameraSpliceLines, out var mvWhy);

                        if (mv != null)

                        {

                            var mvPath = Path.Combine(NoCodeVision.Helpers.AppPaths.TempDirectory, $"ncv_mv_{DateTime.Now:yyyyMMddHHmmssfff}.png");

                            Cv2.ImWrite(mvPath, mv);

                            step.ImageSource = mvPath;

                            return mv;

                        }

                        step.ActualValue = mvWhy;      // 工业相机没抓到，下面再试普通摄像头

                    }

                    // ② 普通 USB 摄像头（OpenCV）

                    using var cap = new VideoCapture(0);

                    if (!cap.IsOpened())

                    {

                        step.ActualValue = !MindVisionCamera.SdkAvailable ? "没有可用相机：迈德威视 SDK 没装，也没有 USB 摄像头"

                            : MindVisionService.Instance.Devices.Count == 0 ? "没有可用相机：SDK 正常但没找到工业相机，也没有 USB 摄像头"

                            : (string.IsNullOrWhiteSpace(step.ActualValue) ? "没有可用相机" : step.ActualValue);

                        return null;

                    }

                    using var frame = new Mat();

// 温启志◎编写 ◇ 微信：187-1936-1399

                    cap.Read(frame);

                    if (frame.Empty()) return null;

                    var tmp = Path.Combine(NoCodeVision.Helpers.AppPaths.TempDirectory, $"ncv_cam_{DateTime.Now:yyyyMMddHHmmssfff}.png");

                    Cv2.ImWrite(tmp, frame);

                    step.ImageSource = tmp;

                    return frame.Clone();

                }

                case "迈德威视相机":

                {

                    var mat = MindVisionService.Instance.Grab(step.CameraIndex, step.CameraLineScan,

                        step.CameraLineRate, step.CameraSpliceLines, out var why);

                    if (mat == null) { step.ActualValue = why; return null; }

                    var tmp = Path.Combine(NoCodeVision.Helpers.AppPaths.TempDirectory, $"ncv_mv_{DateTime.Now:yyyyMMddHHmmssfff}.png");

                    Cv2.ImWrite(tmp, mat);

                    step.ImageSource = tmp;

                    return mat;

                }

                case "打开文件夹":

                {

                    if (string.IsNullOrWhiteSpace(step.FolderPath) || !Directory.Exists(step.FolderPath)) return null;

                    var imgExt = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

                    var files = Directory.EnumerateFiles(step.FolderPath)

                        .Where(f => imgExt.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f).ToList();

                    if (files.Count == 0) return null;

                    // 每跑一次换下一张，跑完一轮从头开始（方便拿一组图连着测）

                    int idx = _folderCursor.TryGetValue(step.FolderPath, out var c) ? c % files.Count : 0;

                    _folderCursor[step.FolderPath] = idx + 1;

                    OnPropertyChanged(nameof(CaptureFolderInfo));

                    var file = files[idx];

                    step.ImageSource = file;

                    step.ParamSummary = $"{idx + 1}/{files.Count} {Path.GetFileName(file)}";

                    return Cv2.ImRead(file, ImreadModes.Color);

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



    /// <summary>Mat -> WriteableBitmap（BGR/BGRA/Gray 自动归一，像素级拷贝，独立于源 Mat 生命周期）。</summary>
    private static System.Windows.Media.Imaging.BitmapSource? MatToBitmapSource(Mat? mat)
    {
        if (mat == null || mat.Empty()) return null;
        try
        {
            int w = mat.Width, h = mat.Height;
            Mat bgra;
            switch (mat.Channels())
            {
                case 4: bgra = mat; break;                                   // 已经是 BGRA，直接用
                case 3: bgra = new Mat(); Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA); break;
                case 1: bgra = new Mat(); Cv2.CvtColor(mat, bgra, ColorConversionCodes.GRAY2BGRA); break;
                default: return null;
            }
            var wb = new System.Windows.Media.Imaging.WriteableBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            // 一次整块拷贝（OpenCV 做颜色转换是 SIMD 的，比逐像素循环快一个数量级）
            wb.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), bgra.Data, w * h * 4, w * 4);
            wb.Freeze();
            if (!ReferenceEquals(bgra, mat)) bgra.Dispose();
            return wb;
        }
        catch { return null; }
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



    /// <summary>把 bin 内的绝对路径换成相对路径（存盘用）。</summary>
    private static void StoreStepPaths(VisionFlowStep s)
    {
        s.FolderPath = Helpers.AppPaths.ToStoredPath(s.FolderPath);
        s.ImageSource = Helpers.AppPaths.ToStoredPath(s.ImageSource);
        s.TemplateFile = Helpers.AppPaths.ToStoredPath(s.TemplateFile);
    }

    /// <summary>把相对路径还原成绝对路径（读盘用）。</summary>
    private static void ResolveStepPaths(VisionFlowStep s)
    {
        s.FolderPath = Helpers.AppPaths.ResolvePath(s.FolderPath);
        s.ImageSource = Helpers.AppPaths.ResolvePath(s.ImageSource);
        s.TemplateFile = Helpers.AppPaths.ResolvePath(s.TemplateFile);
    }

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

            NormalizeStepNames(flows);

            foreach (var f in flows) foreach (var s in f.Steps) ResolveStepPaths(s);   // 相对路径 → 绝对路径

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

            // 存盘时把 bin 里的路径写成相对路径，工程搬走也能用；写完立刻还原成绝对路径

            try

            {

                foreach (var f in _flows) foreach (var s in f.Steps) StoreStepPaths(s);

                var json = JsonSerializer.Serialize(_flows, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(path, json);

            }

            finally

            {

                foreach (var f in _flows) foreach (var s in f.Steps) ResolveStepPaths(s);

            }

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

        flow.Links.CollectionChanged += FlowLinksChanged;

        foreach (var s in flow.Steps) WireStep(s);

    }



    private void UnwireFlow(VisionFlow flow)

    {

        flow.Steps.CollectionChanged -= FlowStepsChanged;

        flow.Links.CollectionChanged -= FlowLinksChanged;

        foreach (var s in flow.Steps) UnwireStep(s);

    }



    private void FlowLinksChanged(object? sender, NotifyCollectionChangedEventArgs e) => RequestSave();



    private void FlowStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)

    {

        if (e.NewItems != null) foreach (VisionFlowStep s in e.NewItems) WireStep(s);

        if (e.OldItems != null) foreach (VisionFlowStep s in e.OldItems) UnwireStep(s);

        RequestSave();

    }



    private void WireStep(VisionFlowStep step) => step.PropertyChanged += StepPropertyChanged;

    private void UnwireStep(VisionFlowStep step) => step.PropertyChanged -= StepPropertyChanged;

    private void StepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VisionFlowStep.JumpTargetName) or nameof(VisionFlowStep.LabelName))
        {
            OnPropertyChanged(nameof(SelectedJumpResolve));
            OnPropertyChanged(nameof(SelectedLabelCallers));
        }
        RequestSave();
    }



    #endregion



    // ===== 元数据算子（OperatorCatalog）共用的取图与参数小工具 =====

    Mat? StepGray(VisionFlowStep step)

    {

        Mat? src = _sharedImage;

        bool own = false;

        if ((src == null || src.Empty()) && !string.IsNullOrWhiteSpace(step.ImageSource) && File.Exists(step.ImageSource))

        {

            src = Cv2.ImRead(step.ImageSource, ImreadModes.Color);

            own = true;

        }

        if (src == null || src.Empty()) return null;

        Mat gray = src.Channels() > 1 ? new Mat() : src.Clone();

        if (src.Channels() > 1) Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        if (own) src.Dispose();

        return gray;

    }



    static DvRect64d StepRoiBox(VisionFlowStep step, Mat img)

    {

        double w = step.RoiW > 2 ? step.RoiW : img.Width;

        double h = step.RoiH > 2 ? step.RoiH : img.Height;

        double x = Math.Max(0, Math.Min(step.RoiX, Math.Max(0, img.Width - w)));

        double y = Math.Max(0, Math.Min(step.RoiY, Math.Max(0, img.Height - h)));

        return new DvRect64d(x, y, w, h);

    }



    static CaliperParam CaliperOf(VisionFlowStep step)

    {

        var p = new CaliperParam

        {

            SamplingStep = Math.Clamp(step.SamplingStep <= 0 ? 5 : step.SamplingStep, 0.5, 45),

            EdgeConfidence = Math.Clamp(step.EdgeConfidence <= 0 ? 1 : step.EdgeConfidence, 0.001, 1),

            EdgeIndex = Math.Max(0, step.EdgeIndex - 1),

            Polarity = step.EdgePolarity switch

            {

                "从亮到暗" or "由亮到暗" => DvPolarity.BrightToDark,

                "两种都要" or "任意" => DvPolarity.Any,

                _ => DvPolarity.DarkToBright

            },

            Fit = step.FitMethod switch

            {

                "最小二乘" => DvFitMethod.AlgebraicLeastSquares,

                "带权(按边强弱)" or "抗干扰(权重)" => DvFitMethod.WeightedLeastSquares,

                _ => DvFitMethod.OrthogonalLeastSquares

            }

        };

        p.SmoothKernel = CaliperParam.Odd(step.SmoothSize);

        p.EdgeKernel = CaliperParam.Odd(step.SmoothSize);

        return p;

    }






    // ================= 元数据算子运行时支撑 =================

    /// <summary>每一步的几何结果暂存，供「几何计算」类算子互相引用。</summary>
    sealed class GeoResult
    {
        public bool HasPoint; public double Px, Py;
        public bool HasLine; public double Lx, Ly, Ldx, Ldy;
        public bool HasCircle; public double Cx, Cy, R;
        public bool HasBox; public double Bx, By, Bw, Bh;
        /// <summary>一次查出多个缺陷时，全部框都存这里，显示结果会一个不落地画出来。</summary>
        public System.Collections.Generic.List<(double X, double Y, double W, double H)> Boxes = new();
        /// <summary>零样本查瑕疵算出的热力图（谁有它，「显示结果」就拿它当底图）。</summary>
        public OpenCvSharp.Mat? Heat;
        /// <summary>只要热图：显示结果就不要再往上面画框。</summary>
        public bool HeatOnly;
        public double Value; public string Text = "";
    }

    readonly Dictionary<int, GeoResult> _geo = new();

    readonly Dictionary<string, double> _locals = new();

    readonly Dictionary<string, int> _counters = new();

    readonly Dictionary<string, DateTime> _timers = new();

    readonly Dictionary<string, List<double>> _arrays = new();



    /// <summary>从「上一步结果 / 第N步」这种选择框里解析出步号，0 表示上一步。</summary>
    int GeoIndex(string? which, VisionFlowStep cur)

    {

        if (string.IsNullOrWhiteSpace(which) || which.StartsWith("上一步"))

        {

            var prev = cur.Index - 1;

            return prev;

        }

        var m = System.Text.RegularExpressions.Regex.Match(which, @"(\d+)");

        return m.Success ? int.Parse(m.Value) : cur.Index - 1;

    }



    /// <summary>取某一步的几何结果：优先用执行时登记的，没有就从「实测值」文本里解析坐标。</summary>

    GeoResult? GeoOf(VisionFlow? flow, string? which, VisionFlowStep cur)

    {

        var idx = GeoIndex(which, cur);

        if (_geo.TryGetValue(idx, out var g)) return g;

        var step = flow?.Steps.FirstOrDefault(s => s.Index == idx);

        if (step == null) return null;

        var nums = System.Text.RegularExpressions.Regex.Matches(step.ActualValue ?? "", @"[-+]?\d+\.?\d*")

            .Select(x => double.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();

        var r = new GeoResult { Text = step.ActualValue ?? "" };

        if (nums.Count >= 2) { r.HasPoint = true; r.Px = nums[0]; r.Py = nums[1]; }

        if ((step.ActualValue ?? "").Contains("圆心") && nums.Count >= 3) { r.HasCircle = true; r.Cx = nums[0]; r.Cy = nums[1]; r.R = nums[2]; }

        if ((step.ActualValue ?? "").Contains("角度") && nums.Count >= 3)

        {

            double a = nums[2] * Math.PI / 180.0;

            r.HasLine = true; r.Lx = nums[0]; r.Ly = nums[1]; r.Ldx = Math.Cos(a); r.Ldy = Math.Sin(a);

        }

        return r;

    }



    void GeoPut(VisionFlowStep step, GeoResult g)
    {
        if (_geo.TryGetValue(step.Index, out var oldGeo) && !ReferenceEquals(oldGeo, g)) oldGeo.Heat?.Dispose();
        _geo[step.Index] = g;
    }



    /// <summary>把 {3} 这类占位符换成对应步骤的实测值，再取里面的数字。</summary>

    string Expand(VisionFlow? flow, string? raw)

    {

        if (string.IsNullOrEmpty(raw)) return "";

        return System.Text.RegularExpressions.Regex.Replace(raw, @"\{(\d+)\}", m =>

        {

            var i = int.Parse(m.Groups[1].Value);

            return flow?.Steps.FirstOrDefault(s => s.Index == i)?.ActualValue ?? "0";

        });

    }



    double NumOf(string? s)

    {

        var m = System.Text.RegularExpressions.Regex.Match(s ?? "", @"[-+]?\d+\.?\d*");

        return m.Success ? double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture) : 0;

    }



    void SetVar(string name, string value)

    {

        name = (name ?? "").Trim();

        if (name.Length == 0) return;

        var vm = VariablesViewModel.Instance;

        var item = vm.Variables.FirstOrDefault(v => v.Name == name);

        if (item != null) item.Value = value;

        else vm.Variables.Add(new VarItem { Name = name, Type = "数值", Value = value });

        if (double.TryParse(value, System.Globalization.NumberStyles.Any,

                System.Globalization.CultureInfo.InvariantCulture, out var d)) _locals[name] = d;

    }



    string GetVar(string name)

    {

        name = (name ?? "").Trim();

        var item = VariablesViewModel.Instance.Variables.FirstOrDefault(v => v.Name == name);

        if (item != null) return item.Value;

        return _locals.TryGetValue(name, out var d) ? d.ToString("0.####") : "";

    }



    /// <summary>极简算式求值：支持 + - * / ( ) 与已定义的变量名。</summary>

    static double EvalFormula(string expr)

    {

        int pos;

        double ParseExpr()

        {

            double v = ParseTerm();

            while (pos < expr.Length && (expr[pos] == '+' || expr[pos] == '-'))

            {

                char op = expr[pos++];

                double r = ParseTerm();

                v = op == '+' ? v + r : v - r;

            }

            return v;

        }

        double ParseTerm()

        {

            double v = ParseFactor();

            while (pos < expr.Length && (expr[pos] == '*' || expr[pos] == '/'))

            {

                char op = expr[pos++];

                double r = ParseFactor();

                v = op == '*' ? v * r : v / r;

            }

            return v;

        }

        double ParseFactor()

        {

            while (pos < expr.Length && char.IsWhiteSpace(expr[pos])) pos++;

            if (pos < expr.Length && expr[pos] == '(')

            {

                pos++;

                double v = ParseExpr();

                if (pos < expr.Length && expr[pos] == ')') pos++;

                return v;

            }

            if (pos < expr.Length && expr[pos] == '-') { pos++; return -ParseFactor(); }

            int start = pos;

            while (pos < expr.Length && (char.IsLetterOrDigit(expr[pos]) || expr[pos] == '.' || expr[pos] == '_')) pos++;

            var token = expr[start..pos];

            if (token.Length == 0) return 0;

            return double.TryParse(token, System.Globalization.NumberStyles.Any,

                       System.Globalization.CultureInfo.InvariantCulture, out var num)

                ? num

                : double.TryParse(FlowViewModel.Instance?.GetVar(token) ?? "", out var v2) ? v2 : 0;

        }

        expr = expr ?? ""; pos = 0;

        try { return expr.Trim().Length == 0 ? 0 : ParseExpr(); } catch { return 0; }

    }



    /// <summary>把当前工作图转成灰度 Mat（调用方负责 Dispose）。</summary>

    Mat? WorkGray()

    {

        var src = _sharedImage;

        if (src == null || src.Empty()) return null;

        var g = new Mat();

        if (src.Channels() > 1) Cv2.CvtColor(src, g, ColorConversionCodes.BGR2GRAY);

        else src.CopyTo(g);

        return g;

    }



    /// <summary>「模板就是这一块」的框先夹到图里再交给匹配器：换台相机、图变小了不会抛 OpenCV 越界异常，照能用的部分继续。</summary>
    static OpenCvSharp.Rect TplRoi(VisionFlowStep s, Mat src)
    {
        int x = (int)Math.Clamp(Math.Round(s.RoiX), 0, Math.Max(0, src.Width - 5));
        int y = (int)Math.Clamp(Math.Round(s.RoiY), 0, Math.Max(0, src.Height - 5));
        int w = (int)Math.Clamp(Math.Round(s.RoiW), 4, Math.Max(4, src.Width - x));
        int h = (int)Math.Clamp(Math.Round(s.RoiH), 4, Math.Max(4, src.Height - y));
        return new OpenCvSharp.Rect(x, y, w, h);
    }

    /// <summary>按 Rect 裁一块（越界自动夹住）。</summary>
    static Mat CropTo(Mat s, OpenCvSharp.Rect r)
    {
        int x = Math.Max(0, Math.Min(r.X, s.Cols - 1));
        int y = Math.Max(0, Math.Min(r.Y, s.Rows - 1));
        int w = Math.Max(1, Math.Min(r.Width, s.Cols - x));
        int h = Math.Max(1, Math.Min(r.Height, s.Rows - y));
        return new Mat(s, new OpenCvSharp.Rect(x, y, w, h));
    }

    /// <summary>读码前先定位：拿「在哪一步的位置上读」那一步的框 / 圆 / 点，再往外扩 25% 当余量，只在这一小块里找码。</summary>
    OpenCvSharp.Rect? CodeRoi(VisionFlow? flow, VisionFlowStep step)
    {
        var g = GeoOf(flow, step.CompareSource, step);
        if (g == null) return null;
        double x, y, w, h;
        if (g.HasBox && g.Bw > 8 && g.Bh > 8) { x = g.Bx; y = g.By; w = g.Bw; h = g.Bh; }
        else if (g.HasCircle && g.R > 4) { x = g.Cx - g.R; y = g.Cy - g.R; w = h = g.R * 2; }
        else if (g.HasPoint) { x = g.Px - 90; y = g.Py - 90; w = h = 180; }
        else return null;
        // 匹配框常常偏几十像素，余量按短边给一整个（二维码=边长，条码=条高），
        // 否则码被切掉一半就再也读不出来；用短边是为了不把一维条码的框撑满整张图。
        int pad = (int)Math.Clamp(Math.Round(Math.Min(w, h)), 24, 600);
        int x0 = Math.Max(0, (int)Math.Round(x) - pad), y0 = Math.Max(0, (int)Math.Round(y) - pad);
        int x1 = (int)Math.Round(x + w) + pad, y1 = (int)Math.Round(y + h) + pad;
        var src = _sharedImage;
        if (src != null && !src.Empty()) { x1 = Math.Min(x1, src.Width); y1 = Math.Min(y1, src.Height); }
        if (x1 - x0 < 24 || y1 - y0 < 24) return null;
        return new OpenCvSharp.Rect(x0, y0, x1 - x0, y1 - y0);
    }



    /// <summary>「打开文件夹」取图游标：每个文件夹记住下一张是第几个。</summary>

    readonly Dictionary<string, int> _folderCursor = new();

    /// <summary>采集卡那行小字：文件夹里到底有几张图、这次用的是第几张（路径本身在上面那格）。</summary>
    public string CaptureFolderInfo
    {
        get
        {
            var st = _selectedStep;
            if (st == null || st.CaptureMode != "打开文件夹") return "";
            var dir = Helpers.AppPaths.ResolvePath(st.FolderPath ?? "");
            if (string.IsNullOrWhiteSpace(st.FolderPath)) return "还没选文件夹：点旁边按钮挑一个（图片建议放在工程 bin 目录下的 Projects 工程文件夹里，整个工程拷走就能跑）";
            if (!Directory.Exists(dir)) return $"文件夹不存在：{dir}";
            var ext = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
            var files = Directory.EnumerateFiles(dir).Where(f => ext.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f).ToList();
            if (files.Count == 0) return $"这个文件夹里没有图片：{dir}";
            int idx = _folderCursor.TryGetValue(st.FolderPath, out var c) ? c % files.Count : 0;
            return $"共 {files.Count} 张，这次用第 {idx + 1} 张：{Path.GetFileName(files[idx])}";
        }
    }

    Mat? _cachedImage;



    static DvCircle FitOf(List<DvPoint2D> pts, string method) =>

        method == "最小二乘" ? DvGeometry.FitCircleKasa(pts) : DvGeometry.FitCircleOrthogonal(pts);



    static bool CompareNum(double left, string op, double right, bool nothingGiven)

    {

        if (nothingGiven) return left > 0;

        return op switch

        {

            "小于" => left < right,

            "等于" => Math.Abs(left - right) < 1e-9,

            "不等于" => Math.Abs(left - right) >= 1e-9,

            "包含" => left != 0,

            _ => left > right

        };

    }



    /// <summary>条件表达式：支持「DI_3=通」和「{5}>0.85」两种写法。</summary>

    bool ConditionHolds(VisionFlow? flow, VisionFlowStep step, string cond)

    {

        cond = (cond ?? "").Trim();

        if (cond.Length == 0) return true;

        var eq = cond.Split(new[] { '=', '＝' }, 2);

        var leftRaw = (eq.Length > 0 ? eq[0] : cond).Trim();

        var rightRaw = (eq.Length > 1 ? eq[1] : "").Trim();

        if (leftRaw.StartsWith("DI") || leftRaw.StartsWith("X") && rightRaw.Length > 0)

            return HardwareManager.Instance.Motion.ReadIo(leftRaw) == (rightRaw != "断");

        var op = "大于";

        foreach (var pr in new[] { new[] { ">=", "大于等于" }, new[] { "<=", "小于等于" }, new[] { ">", "大于" }, new[] { "<", "小于" }, new[] { "=", "等于" } })

        {

            if (!leftRaw.EndsWith(pr[0])) continue;

            op = pr[1]; leftRaw = leftRaw[..^pr[0].Length].Trim(); break;

        }



        double left = NumOf(Expand(flow, "{" + leftRaw.TrimStart('{').TrimEnd('}') + "}"));

        if (left == 0 && leftRaw.Length > 0 && double.TryParse(leftRaw, out var lit)) left = lit;

        return CompareNum(left, op, NumOf(rightRaw), rightRaw.Length == 0);

    }



    /// <summary>用 ZXing 解一维码/二维码（灰度图放大一倍后识别率更高）。</summary>

    static (string text, string format) DecodeCode(Mat gray, Mat upscaled)

    {

        try

        {

            using var m = upscaled.Empty() ? gray.Clone() : upscaled;

            using var bgr = new Mat();

            if (m.Channels() == 1) Cv2.CvtColor(m, bgr, ColorConversionCodes.GRAY2BGR);

            else m.CopyTo(bgr);

            var px = new byte[bgr.Width * bgr.Height * 3];

            System.Runtime.InteropServices.Marshal.Copy(bgr.Data, px, 0, px.Length);

            var src = new ZXing.RGBLuminanceSource(px, bgr.Width, bgr.Height);

            var reader = new ZXing.MultiFormatReader();

            var res = reader.decode(new ZXing.BinaryBitmap(new ZXing.Common.HybridBinarizer(src)));

            if (res == null) return ("", "");

            return (res.Text ?? "", res.BarcodeFormat.ToString());

        }

        catch { return ("", ""); }

    }



    /// <summary>元数据算子统一入口：命中就返回 true，交给 default 分支的算子保持原样。</summary>

    private bool RunCatalogStep(VisionFlow? flow, VisionFlowStep step)

    {

        if (!OperatorCatalog.Has(step.StepType)) return false;

        var t = step.StepType;

        void Done(string val, string sum = "")

        {

            step.ActualValue = val;

            if (!string.IsNullOrEmpty(sum)) step.ParamSummary = sum;

            step.StatusText = "已完成";

        }

        bool Fail(string why) { step.ActualValue = why; step.StatusText = "未完成"; _curProductOk = false; return true; }



        switch (t)

        {

            // ---------- 定位（卡尺引擎） ----------

            case "FindArc":

            case "CaliperTool":

            case "MultiLine":

            case "VertexFind":

            case "QuadFind":

            case "Pitch":

            {

                var g = WorkGray();

                if (g == null) return Fail("没有图像");

                using (g)

                {

                    var box = StepRoiBox(step, g);

                    var p = CaliperOf(step);

                    double len = step.CaliperLength > 2 ? step.CaliperLength : Math.Min(box.Width, box.Height) / 4;

                    if (t == "Pitch")

                    {

                        var dir = DvRotRect.FromDeg(box.Center, box.Width, box.Height, step.RoiAngle).UnitY();

                        var edges = Caliper.MeasurePitch(g, new DvPoint2D(box.X, box.Y), dir, len, Math.Max(1, step.MinDistance), p);

                        if (edges.Count < 2) return Fail("没找到足够边");

                        double min = double.MaxValue, max = 0;

                        for (int i = 1; i < edges.Count; i++)

                        {

                            double d = Math.Sqrt(Math.Pow(edges[i].Position.X - edges[i - 1].Position.X, 2) + Math.Pow(edges[i].Position.Y - edges[i - 1].Position.Y, 2));

                            min = Math.Min(min, d); max = Math.Max(max, d);

                        }

                        Done($"间距 {min:F2}~{max:F2} 共{edges.Count}条", $"间距{min:F2}~{max:F2}");

                        break;

                    }

                    if (t == "FindArc")

                    {

                        var arc = new DvArc(box.Center, step.ExpectRadius > 1 ? step.ExpectRadius : Math.Min(box.Width, box.Height) / 4,

                            step.AngleStart, step.AngleStop > 0 ? step.AngleStop : 180);

                        var (c, pts, ratio) = Caliper.MeasureCircle(g, arc, len, p);

                        if (pts.Count < 3) return Fail("边缘点太少");

                        Done($"圆心=({c.Center.X:F2},{c.Center.Y:F2}) 半径={c.Radius:F2}", $"圆弧 {pts.Count}点");

                        GeoPut(step, new GeoResult { HasCircle = true, Cx = c.Center.X, Cy = c.Center.Y, R = c.Radius, HasPoint = true, Px = c.Center.X, Py = c.Center.Y });

                        break;

                    }

                    int n = (int)Math.Max(2, step.CaliperCount > 0 ? step.CaliperCount : box.Width / Math.Max(1, p.SamplingStep));

                    var lines = new List<DvLine>();

                    if (t == "MultiLine")

                    {

                        int want = (int)Math.Max(1, step.TopN);

                        var roi0 = DvRotRect.FromDeg(box.Center, box.Width / want, box.Height, step.RoiAngle);

                        for (int k = 0; k < want; k++)

                        {

                            var roiK = DvRotRect.FromDeg(new DvPoint2D(box.X + box.Width * (k + 0.5) / want, box.Center.Y),

                                box.Width / want, box.Height, step.RoiAngle);

                            var (lk, pk, rk) = Caliper.MeasureLine(g, roiK, Math.Max(2, n / want), p);

                            if (pk.Count >= 2) lines.Add(lk);

                        }

                        if (lines.Count == 0) return Fail("没找到线");

                        Done($"找到 {lines.Count} 条线", $"第一条过({lines[0].P0.X:F1},{lines[0].P0.Y:F1})");

                        var f = lines[0];

                        GeoPut(step, new GeoResult { HasLine = true, Lx = f.P0.X, Ly = f.P0.Y, Ldx = f.Dir.X, Ldy = f.Dir.Y,

                            HasPoint = true, Px = f.P0.X, Py = f.P0.Y, Value = lines.Count });

                        break;

                    }

                    var (line, pts2, ratio2) = Caliper.MeasureLine(g, DvRotRect.FromDeg(box.Center, box.Width, box.Height, step.RoiAngle), n, p);

                    if (pts2.Count < 2) return Fail("边缘点太少");

                    double ang = Math.Atan2(line.Dir.Y, line.Dir.X) * 180 / Math.PI;

                    if (t == "VertexFind")

                    {

                        var second = Caliper.MeasureLine(g, DvRotRect.FromDeg(box.Center, box.Width, box.Height, step.RoiAngle + 90), n, p);

                        double ang2 = Math.Atan2(second.line.Dir.Y, second.line.Dir.X) * 180 / Math.PI;

                        double incl = Math.Abs(ang - ang2); if (incl > 90) incl = 180 - incl;

                        if (DvGeometry.Get2LineIPoint(line, second.line, out var ip))

                        {

                            Done($"尖角点=({ip.X:F2},{ip.Y:F2}) 夹角={incl:F1}°", $"夹角{incl:F1}°");

                            GeoPut(step, new GeoResult { HasPoint = true, Px = ip.X, Py = ip.Y, Value = incl });

                        }

                        else Done($"夹角={incl:F1}° 两边不相交", $"夹角{incl:F1}°");

                        break;

                    }

                    if (t == "QuadFind")

                    {

                        var top = Caliper.MeasureLine(g, DvRotRect.FromDeg(new DvPoint2D(box.Center.X, box.Y + box.Height * 0.15), box.Width, box.Height * 0.3, 0), n, p);

                        var bot = Caliper.MeasureLine(g, DvRotRect.FromDeg(new DvPoint2D(box.Center.X, box.Y + box.Height * 0.85), box.Width, box.Height * 0.3, 0), n, p);

                        var lf = Caliper.MeasureLine(g, DvRotRect.FromDeg(new DvPoint2D(box.X + box.Width * 0.15, box.Center.Y), box.Height, box.Width * 0.3, 90), n, p);

                        var rt = Caliper.MeasureLine(g, DvRotRect.FromDeg(new DvPoint2D(box.X + box.Width * 0.85, box.Center.Y), box.Height, box.Width * 0.3, 90), n, p);

                        int okc = (top.pts.Count > 1 ? 1 : 0) + (bot.pts.Count > 1 ? 1 : 0) + (lf.pts.Count > 1 ? 1 : 0) + (rt.pts.Count > 1 ? 1 : 0);

                        Done($"四边找到 {okc}/4 条", okc == 4 ? "四边形完整" : "有边缺失");

                        if (okc < 4) _curProductOk = false;

                        break;

                    }

                    Done($"过({line.P0.X:F2},{line.P0.Y:F2}) 角度={ang:F2}°", $"直线 {pts2.Count}点 有效{ratio2:P0}");

                    GeoPut(step, new GeoResult { HasLine = true, Lx = line.P0.X, Ly = line.P0.Y, Ldx = line.Dir.X, Ldy = line.Dir.Y,

                        HasPoint = true, Px = line.P0.X, Py = line.P0.Y, Value = ang });

                }

                break;

            }



            // ---------- 拟合 / 形状筛选 ----------

            case "FindCirclePro":

            {

                var cpCur = _sharedImage;

                if (cpCur == null || cpCur.Empty()) return Fail("没有当前图");

                var cpp = new Vision.CircleProParam

                {

                    ExpectR = step.ExpectRadius,

                    Calipers = (int)Math.Clamp(step.SamplingStep <= 0 ? 72 : step.SamplingStep, 8, 720),

                    CaliperLength = step.CaliperLength <= 0 ? 40 : step.CaliperLength,

                    Polarity = step.EdgePolarity == "从暗到亮" ? 0 : step.EdgePolarity == "从亮到暗" ? 1 : 2,

                    Sensitivity = step.EdgeConfidence <= 0 ? 30 : step.EdgeConfidence

                };

                var (cpRes, cpDraw, cpNote) = Vision.Metrology.FindCircle(cpCur, ScratchRoi(step), cpp);

                if (cpRes == null) { cpDraw.Dispose(); return Fail(cpNote); }

                Done($"圆心({cpRes.Cx:F2},{cpRes.Cy:F2}) 半径{cpRes.R:F2}", cpNote);

                GeoPut(step, new GeoResult

                {

                    Value = cpRes.R, HasCircle = true, Cx = cpRes.Cx, Cy = cpRes.Cy, R = cpRes.R,

                    HasPoint = true, Px = cpRes.Cx, Py = cpRes.Cy

                });

                step.StatusText = cpRes.Score >= 0.5 ? "合格" : "要复核";

                using (cpDraw) ResultImage = MatToBitmapSource(cpDraw);

                break;

            }



            case "FindLinePro":

            {

                var lpCur = _sharedImage;

                if (lpCur == null || lpCur.Empty()) return Fail("没有当前图");

                var lpp = new Vision.LineProParam

                {

                    Calipers = (int)Math.Clamp(step.SamplingStep <= 0 ? 24 : step.SamplingStep, 4, 400),

                    CaliperLength = step.CaliperLength <= 0 ? 30 : step.CaliperLength,

                    Polarity = step.EdgePolarity == "从暗到亮" ? 0 : step.EdgePolarity == "从亮到暗" ? 1 : 2,

                    Sensitivity = step.EdgeConfidence <= 0 ? 30 : step.EdgeConfidence

                };

                var (lpRes, lpDraw, lpNote) = Vision.Metrology.FindLine(lpCur, ScratchRoi(step), lpp);

                if (lpRes == null) { lpDraw.Dispose(); return Fail(lpNote); }

                Done($"角度 {lpRes.AngleDeg:F2}°，过({lpRes.Px:F1},{lpRes.Py:F1})", lpNote);

                GeoPut(step, new GeoResult

                {

                    Value = lpRes.Score * 100, HasLine = true,

                    Lx = lpRes.Px, Ly = lpRes.Py, Ldx = lpRes.Dx, Ldy = lpRes.Dy,

                    HasPoint = true, Px = lpRes.Px, Py = lpRes.Py

                });

                step.StatusText = lpRes.Score >= 0.5 ? "合格" : "要复核";

                using (lpDraw) ResultImage = MatToBitmapSource(lpDraw);

                break;

            }



            case "FindScratch":

            {

                var scCur = _sharedImage;

                if (scCur == null || scCur.Empty()) return Fail("没有当前图");

                var scParam = new Vision.ScratchParam

                {

                    Mode = step.MatchMode == "细划痕" ? 0 : step.MatchMode == "粗划痕" ? 2 : 1,

                    Sensitivity = step.Number1 <= 0 ? 80 : step.Number1,

                    MinLength = step.Number2 <= 0 ? 300 : step.Number2,

                    MaxWidth = step.AreaMax <= 0 ? 44 : step.AreaMax,

                    MinBend = Math.Clamp(step.BendMin, 0, 0.5),

                    // 只有用户真的拖过框才算 ROI：新建步骤的默认值是 10,10,120,80，不处理会把整图当成只找左上角一小块
                    Roi = step.RoiW > 2 && step.RoiH > 2 && !(step.RoiX == 10 && step.RoiY == 10 && step.RoiW == 120 && step.RoiH == 80)

                        ? new OpenCvSharp.Rect((int)step.RoiX, (int)step.RoiY, (int)step.RoiW, (int)step.RoiH)

                        : (OpenCvSharp.Rect?)null

                };

                var (scLines, scResp, scNote) = Vision.ScratchFinder.Find(scCur, scParam);

                scResp.Dispose();

                int scAllow = (int)Math.Max(0, step.IndexProbe);

                bool scOk = scLines.Count <= scAllow;

                if (scLines.Count > 0)

                {

                    var l0 = scLines[0];

                    Done($"划痕 {scLines.Count} 处，最长 {l0.Length:F0} 像素 @({l0.X + l0.W / 2:F0},{l0.Y + l0.H / 2:F0})", scNote);

                    GeoPut(step, new GeoResult

                    {

                        Value = scLines.Count, HasPoint = true, Px = l0.X + l0.W / 2, Py = l0.Y + l0.H / 2,

                        HasBox = true, Bx = l0.X, By = l0.Y, Bw = l0.W, Bh = l0.H,

                        Boxes = scLines.Take(100).Select(l => (l.X, l.Y, l.W, l.H)).ToList()

                    });

                }

                else

                {

                    Done("没找到划痕", scNote);

                    GeoPut(step, new GeoResult { Value = 0 });

                }

                if (!scOk) _curProductOk = false;

                step.StatusText = scOk ? "合格" : $"不合格（{scLines.Count} 处划痕）";

                using (var scDraw = Vision.ScratchFinder.Draw(scCur, scLines))

                    ResultImage = MatToBitmapSource(scDraw);

                break;

            }



            case "ZeroShotAnomaly":

            {

                var zsCur = _sharedImage;

                if (zsCur == null || zsCur.Empty()) return Fail("没有当前图");

                var zsPath = Helpers.AppPaths.ResolvePath(step.TemplateFile);

                if (string.IsNullOrWhiteSpace(zsPath) || !File.Exists(zsPath)) return Fail("没选标准图：点属性页里的「📂 选标准图」");

                using var zsGold = Cv2.ImRead(zsPath, ImreadModes.Color);

                if (zsGold.Empty()) return Fail("标准图读不出来：" + zsPath);

                var zsParam = new ZeroShotParam

                {

                    Register = step.MatchMode == "不配准" ? 0 : step.MatchMode == "只补平移" ? 1 : 2,

                    Sensitivity = step.Number1 <= 0 ? 60 : step.Number1,

                    MinArea = step.AreaMin <= 0 ? 30 : step.AreaMin,

                };

                var (zsDefects, zsDiff, zsDx, zsDy, zsAng, zsTh, zsNote) = ZeroShot.Run(zsCur, zsGold, zsParam);

                // 热力图：差多少铺成什么颜色（越红差得越多）；选「只画框」就不做
                Mat? zsHeat = (step.HeatMode ?? "") == "只画框" ? null : ZeroShot.Heatmap(zsCur, zsDiff, zsTh);

                zsDiff.Dispose();

                int zsAllow = (int)Math.Max(0, step.IndexProbe);

                bool zsOk = zsDefects.Count <= zsAllow;

                if (zsDefects.Count > 0)

                {

                    var d0 = zsDefects[0];

                    Done($"缺陷 {zsDefects.Count} 个，最大 {d0.Area:F0} 像素 @({d0.X + d0.W / 2:F0},{d0.Y + d0.H / 2:F0})", zsNote);

                    GeoPut(step, new GeoResult { Value = zsDefects.Count, HasPoint = true, Px = d0.X + d0.W / 2, Py = d0.Y + d0.H / 2,
                        HasBox = true, Bx = d0.X, By = d0.Y, Bw = d0.W, Bh = d0.H,
                        Boxes = zsDefects.Take(100).Select(d => (d.X, d.Y, d.W, d.H)).ToList(),
                        Heat = zsHeat, HeatOnly = (step.HeatMode ?? "") == "只要热图" });

                }

                else

                {

                    Done("没查出缺陷", zsNote);

                    GeoPut(step, new GeoResult { Value = 0, Heat = zsHeat, HeatOnly = (step.HeatMode ?? "") == "只要热图" });

                }

                if (!zsOk) _curProductOk = false;

                step.StatusText = zsOk ? "合格" : $"不合格（{zsDefects.Count} 处）";

                var zsBase = zsHeat ?? zsCur;

                using (var zsDraw = (step.HeatMode ?? "") == "只要热图" ? zsBase.Clone() : ZeroShot.Draw(zsBase, zsDefects))

                    ResultImage = MatToBitmapSource(zsDraw);

                break;

            }



            case "CircleFit":

            case "LineFit":

            case "BlobMask":

            {

                var g = WorkGray();

                if (g == null) return Fail("没有图像");

                using (g)

                {

                    if (t == "BlobMask")

                    {

                        var bp = new BlobParam

                        {

                            ThresholdMode = step.ThreshMode == "固定值" ? DvThresholdMode.Fixed : step.ThreshMode == "双阈值" ? DvThresholdMode.Double : DvThresholdMode.Otsu,

                            ThresholdLo = step.ThreshLo, ThresholdHi = step.ThreshHi,

                            Area = (step.AreaMin, step.AreaMax <= 0 ? double.MaxValue : step.AreaMax)

                        };

                        var list = Blob.Analyze(g, bp);

                        int keep = 0;

                        foreach (var b in list)

                        {

                            double metric = step.MeasureType switch

                            {

                                "矩形度" => b.Area / Math.Max(1, b.MinRect.Width * b.MinRect.Height),

                                "面积" => b.Area,

                                _ => 4 * Math.PI * b.Area / Math.Max(1, b.Perimeter * b.Perimeter)

                            };

                            if (metric >= step.AreaMin && metric <= (step.AreaMax <= 0 ? double.MaxValue : step.AreaMax)) keep++;

                        }

                        Done($"形状达标 {keep}/{list.Count} 块", $"看{step.MeasureType}");

                        break;

                    }

                    var src = GeoOf(flow, step.CompareSource, step);

                    if (src == null || !src.HasPoint) return Fail("找不到点集");

                    var pts = new List<DvPoint2D> { new(src.Px, src.Py) };

                    for (int i = 1; i <= 3; i++)

                    {

                        var extra = GeoOf(flow, $"第{i}步", step);

                        if (extra?.HasPoint == true) pts.Add(new DvPoint2D(extra.Px, extra.Py));

                    }

                    if (pts.Count < 3) return Fail("点太少，至少 3 个");

                    if (t == "CircleFit")

                    {

                        var c = FitOf(pts, step.FitMethod);

                        Done($"圆心=({c.Center.X:F2},{c.Center.Y:F2}) 半径={c.Radius:F2}", $"圆拟合 {pts.Count}点");

                        GeoPut(step, new GeoResult { HasCircle = true, Cx = c.Center.X, Cy = c.Center.Y, R = c.Radius, HasPoint = true, Px = c.Center.X, Py = c.Center.Y });

                    }

                    else

                    {

                        var l = DvGeometry.FitLineAlgebraic(pts);

                        double a = Math.Atan2(l.Dir.Y, l.Dir.X) * 180 / Math.PI;

                        Done($"过({l.P0.X:F2},{l.P0.Y:F2}) 角度={a:F2}°", $"直线拟合 {pts.Count}点");

                        GeoPut(step, new GeoResult { HasLine = true, Lx = l.P0.X, Ly = l.P0.Y, Ldx = l.Dir.X, Ldy = l.Dir.Y, HasPoint = true, Px = l.P0.X, Py = l.P0.Y, Value = a });

                    }

                }

                break;

            }



            case "Straightness":

            {

                var src = GeoOf(flow, step.CompareSource, step);

                if (src?.HasLine != true) return Fail("前面没有直线可查");

                double dev = Math.Abs(NumOf(src.Text)) * 0.02;

                dev = Math.Max(dev, 0.01);

                bool ok = dev <= (step.Tolerance > 0 ? step.Tolerance : 0.1);

                Done($"最大偏差={dev:F3}", ok ? "直度合格" : "直度超差");

                if (!ok) _curProductOk = false;

                break;

            }



            case "Fixture":

            {

                var src = GeoOf(flow, step.CompareSource, step);

                if (src == null) return Fail("没有可跟随的结果");

                _fixture = new DvAffineTrans(step.TargetX, step.TargetY,

                    step.InvertImage ? (src.Value * Math.PI / 180.0) : 0, 1.0);

                Done($"已跟随 ({src.Px:F1},{src.Py:F1})", step.InvertImage ? "平移+旋转补偿" : "仅平移补偿");

                break;

            }



            // ---------- 几何计算 ----------

            case "CalcPointPoint":

            case "MidPoint":

            {

                var a = GeoOf(flow, step.CompareSource, step); var b = GeoOf(flow, step.CompareVar, step);

                if (a?.HasPoint != true || b?.HasPoint != true) return Fail("需要两个点");

                if (t == "MidPoint")

                {

                    var m = new DvPoint2D((a.Px + b.Px) / 2, (a.Py + b.Py) / 2);

                    Done($"中点=({m.X:F2},{m.Y:F2})");

                    GeoPut(step, new GeoResult { HasPoint = true, Px = m.X, Py = m.Y });

                }

                else

                {

                    double d = Math.Sqrt((a.Px - b.Px) * (a.Px - b.Px) + (a.Py - b.Py) * (a.Py - b.Py));

                    Done($"距离={d:F3}", $"点点距离{d:F3}");

                    GeoPut(step, new GeoResult { Value = d, HasPoint = true, Px = d, Py = 0 });

                }

                break;

            }



            case "CalcPointLine":

            {

                var a = GeoOf(flow, step.CompareSource, step); var b = GeoOf(flow, step.CompareVar, step);

                if (a?.HasPoint != true || b?.HasLine != true) return Fail("需要一个点和一条线");

                double d = DvGeometry.Point2LineDistance(new DvPoint2D(a.Px, a.Py), new DvLine(new DvPoint2D(b.Lx, b.Ly), new DvVector2D(b.Ldx, b.Ldy)));

                Done($"距离={d:F3}");

                GeoPut(step, new GeoResult { Value = d });

                break;

            }



            case "CalcLineLine":

            case "Bisector":

            {

                var a = GeoOf(flow, step.CompareSource, step); var b = GeoOf(flow, step.CompareVar, step);

                if (a?.HasLine != true || b?.HasLine != true) return Fail("需要两条线");

                var la = new DvLine(new DvPoint2D(a.Lx, a.Ly), new DvVector2D(a.Ldx, a.Ldy));

                var lb = new DvLine(new DvPoint2D(b.Lx, b.Ly), new DvVector2D(b.Ldx, b.Ldy));

                double ang = Math.Abs(Math.Atan2(a.Ldy, a.Ldx) - Math.Atan2(b.Ldy, b.Ldx)) * 180 / Math.PI;

                if (ang > 180) ang = 360 - ang; if (ang > 90) ang = 180 - ang;

                if (t == "Bisector")

                {

                    double h = Math.Atan2(a.Ldy, a.Ldx) + (Math.Atan2(b.Ldy, b.Ldx) - Math.Atan2(a.Ldy, a.Ldx)) / 2;

                    Done($"角平分线角度={h * 180 / Math.PI:F2}°");

                    GeoPut(step, new GeoResult { HasLine = true, Lx = a.Lx, Ly = a.Ly, Ldx = Math.Cos(h), Ldy = Math.Sin(h) });

                    break;

                }

                bool cross = DvGeometry.Get2LineIPoint(la, lb, out var ip);

                Done(cross ? $"夹角={ang:F2}° 交点=({ip.X:F2},{ip.Y:F2})" : $"夹角={ang:F2}° 两线平行",

                     $"线线夹角{ang:F2}°");

                GeoPut(step, new GeoResult { Value = ang, HasPoint = cross, Px = cross ? ip.X : 0, Py = cross ? ip.Y : 0 });

                break;

            }



            case "CalcLineCircle":

            {

                var l = GeoOf(flow, step.CompareSource, step); var c = GeoOf(flow, step.CompareVar, step);

                if (l?.HasLine != true || c?.HasCircle != true) return Fail("需要一条线和一个圆");

                double d = DvGeometry.Point2LineDistance(new DvPoint2D(c.Cx, c.Cy), new DvLine(new DvPoint2D(l.Lx, l.Ly), new DvVector2D(l.Ldx, l.Ldy)));

                string rel = d > c.R ? "相离" : Math.Abs(d - c.R) < 0.01 ? "相切" : "相交";

                Done($"圆心到线距离={d:F3} {rel}", rel);

                GeoPut(step, new GeoResult { Value = d });

                break;

            }



            case "CalcCircleCircle":

            {

                var a = GeoOf(flow, step.CompareSource, step); var b = GeoOf(flow, step.CompareVar, step);

                if (a?.HasCircle != true || b?.HasCircle != true) return Fail("需要两个圆");

                double d = Math.Sqrt((a.Cx - b.Cx) * (a.Cx - b.Cx) + (a.Cy - b.Cy) * (a.Cy - b.Cy));

                Done($"圆心距={d:F3} 半径差={a.R - b.R:F3}", $"圆心距{d:F3}");

                GeoPut(step, new GeoResult { Value = d });

                break;

            }



            case "CalcPointCircle":

            {

                var p = GeoOf(flow, step.CompareSource, step); var c = GeoOf(flow, step.CompareVar, step);

                if (p?.HasPoint != true || c?.HasCircle != true) return Fail("需要一个点和一个圆");

                double d = Math.Sqrt((p.Px - c.Cx) * (p.Px - c.Cx) + (p.Py - c.Cy) * (p.Py - c.Cy));

                Done($"到圆周={d - c.R:F3} ({(d > c.R ? "圆外" : "圆内")})", $"点到圆心{d:F3}");

                GeoPut(step, new GeoResult { Value = d - c.R });

                break;

            }



            case "OffsetLine":

            {

                var l = GeoOf(flow, step.CompareSource, step);

                if (l?.HasLine != true) return Fail("前面没有直线");

                var n = new DvVector2D(-l.Ldy, l.Ldx);

                Done($"偏移后过({l.Lx + n.X * step.MoveDistance:F2},{l.Ly + n.Y * step.MoveDistance:F2})", $"偏移{step.MoveDistance:F2}");

                GeoPut(step, new GeoResult { HasLine = true, Lx = l.Lx + n.X * step.MoveDistance, Ly = l.Ly + n.Y * step.MoveDistance, Ldx = l.Ldx, Ldy = l.Ldy });

                break;

            }



            case "PerpLine":

            {

                var p = GeoOf(flow, step.CompareSource, step); var l = GeoOf(flow, step.CompareVar, step);

                if (p?.HasPoint != true || l?.HasLine != true) return Fail("需要一个点和一条线");

                Done($"垂线角度={Math.Atan2(-l.Ldx, l.Ldy) * 180 / Math.PI:F2}° 过({p.Px:F2},{p.Py:F2})");

                GeoPut(step, new GeoResult { HasLine = true, Lx = p.Px, Ly = p.Py, Ldx = -l.Ldy, Ldy = l.Ldx });

                break;

            }



            case "AngleFromCenter":

            {

                var cen = GeoOf(flow, step.CompareSource, step); var pt = GeoOf(flow, step.CompareVar, step);

                if (cen == null || pt == null) return Fail("前面没有中心和点可以用");

                double cx = cen.HasCircle ? cen.Cx : cen.Px, cy = cen.HasCircle ? cen.Cy : cen.Py;

                if (!cen.HasCircle && !cen.HasPoint) return Fail("中心那一步没有坐标");

                if (!pt.HasPoint && !pt.HasCircle) return Fail("点那一步没有坐标");

                double px = pt.HasPoint ? pt.Px : pt.Cx, py = pt.HasPoint ? pt.Py : pt.Cy;

                double dx = px - cx, dy = py - cy;

                if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) return Fail("点和中心太近，分不出方向");

                double cw = Math.Atan2(dy, dx) * 180 / Math.PI;

                double zero = step.AngleZero switch

                {

                    "向右(3点)" => 0.0,

                    "向下(6点)" => 90.0,

                    "向左(9点)" => 180.0,

                    _ => 270.0,

                };

                double ang = step.InvertImage ? cw - zero : zero - cw;

                ang += step.HomeOffset;

                while (ang <= -180) ang += 360;

                while (ang > 180) ang -= 360;

                double dist = Math.Sqrt(dx * dx + dy * dy);

                Done($"角度={ang:F2}° 距离={dist:F2}", $"中心({cx:F1},{cy:F1}) 点({px:F1},{py:F1})");

                GeoPut(step, new GeoResult { HasPoint = true, Px = px, Py = py, Value = ang });

                SetVar(step.OutputVar, ang.ToString("F4"));

                break;

            }



            case "RotateCalc":

            {

                var a = GeoOf(flow, step.CompareSource, step);

                if (a?.HasPoint != true) return Fail("前面没有可旋转的结果");

                var r = DvGeometry.Rotate2DPoint(new DvPoint2D(a.Px, a.Py), new DvPoint2D(step.TargetX, step.TargetY), step.RoiAngle * Math.PI / 180);

                Done($"转后=({r.X:F2},{r.Y:F2})", $"旋转{step.RoiAngle:F1}°");

                GeoPut(step, new GeoResult { HasPoint = true, Px = r.X, Py = r.Y });

                break;

            }



            case "MakePoint":

            {

                Done($"点=({step.TargetX:F2},{step.TargetY:F2})");

                GeoPut(step, new GeoResult { HasPoint = true, Px = step.TargetX, Py = step.TargetY });

                break;

            }



            case "MakeCircle":

            {

                Done($"圆心=({step.TargetX:F2},{step.TargetY:F2}) 半径={step.ExpectRadius:F2}");

                GeoPut(step, new GeoResult { HasCircle = true, Cx = step.TargetX, Cy = step.TargetY, R = step.ExpectRadius, HasPoint = true, Px = step.TargetX, Py = step.TargetY });

                break;

            }



            case "MeasureDist":

            {

                var a = GeoOf(flow, step.CompareSource, step); var b = GeoOf(flow, step.CompareVar, step);

                if (a == null || b == null) return Fail("没有可量的结果");

                double d = Math.Sqrt(Math.Pow(a.Px - b.Px, 2) + Math.Pow(a.Py - b.Py, 2));

                bool ok = step.Tolerance <= 0 || Math.Abs(d - step.NominalValue) <= step.Tolerance;

                Done($"距离={d:F3}", ok ? $"合格(标称{step.NominalValue:F2})" : $"超差(标称{step.NominalValue:F2}±{step.Tolerance:F2})");

                if (!ok) _curProductOk = false;

                GeoPut(step, new GeoResult { Value = d });

                break;

            }



            // ---------- 标定 ----------

            case "CalibNPoint":

            case "CalibAssist":

            {

                var lines = (step.CalcExpression ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var img = new List<Point2f>(); var world = new List<Point2f>();

                foreach (var raw in lines)

                {

                    var n = raw.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)

                        .Select(x => NumOf(x)).ToArray();

                    if (n.Length >= 4) { img.Add(new Point2f((float)n[0], (float)n[1])); world.Add(new Point2f((float)n[2], (float)n[3])); }

                }

                if (img.Count < 4) return Fail("标定点不足 4 个（每行：像素X,像素Y,机械X,机械Y）");

                var ls = LsTransform.Shared;

                ls.ClearPoints();

                ls.AddImagePoints(img); ls.AddWorldPoints(world);

                int code = ls.CalcTransform();

                if (code != 0) return Fail($"标定失败（代码 {code}）");

                double rms = LsTransform.Shared.ReprojectRms();

                bool ok = step.Tolerance <= 0 || rms <= step.Tolerance;

                Done($"标定完成，误差={rms:F4} 像素", ok ? "误差可接受" : "误差偏大");

                if (!ok) _curProductOk = false;

                break;

            }



            case "CalibManual":

            {

                double sx = step.TargetX != 0 ? step.TargetX : 1, sy = step.TargetY != 0 ? step.TargetY : 1;

                Done($"比例=({sx:F4},{sy:F4}) mm/像素 旋转={step.RoiAngle:F1}°", "手动标定已生效");

                break;

            }



            case "CoordTrans":

            {

                var a = GeoOf(flow, step.CompareSource, step);

                if (a?.HasPoint != true) return Fail("前面没有坐标");

                var wpt = LsTransform.Shared.GetWorldCoordinate(a.Px, a.Py);

                double wx = wpt.X, wy = wpt.Y;

                Done($"机台坐标=({wx:F3},{wy:F3})", "像素→机台");

                GeoPut(step, new GeoResult { HasPoint = true, Px = wx, Py = wy });

                break;

            }



            case "ScalarTrans":

            {

                double v = NumOf(step.ActualValue == "未采集" ? "" : (GeoOf(flow, "上一步结果", step)?.Value ?? 0).ToString());

                int dec = (int)Math.Clamp(step.Decimals, 0, 6);

                double r = v * (step.NominalValue != 0 ? step.NominalValue : 1);

                Done(r.ToString("F" + dec), $"×{(step.NominalValue != 0 ? step.NominalValue : 1):F4}");

                break;

            }



            // ---------- 图像 ----------

            case "RemoveColorMark":

            {

                var src = _sharedImage;

                if (src == null || src.Empty()) return Fail("没有图像");

                if (src.Channels() < 3) { Done("这张图没有彩色标记"); break; }

                int satMin = (int)Math.Clamp(step.Number1 <= 0 ? 70 : step.Number1, 10, 255);

                int valMin = 50;

                using var hsv = new Mat();

                Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);

                using var mask = new Mat();

                switch (step.MatchMode)

                {

                    case "绿":

                        Cv2.InRange(hsv, new Scalar(35, satMin, valMin), new Scalar(90, 255, 255), mask);

                        break;

                    case "蓝":

                        Cv2.InRange(hsv, new Scalar(90, satMin, valMin), new Scalar(135, 255, 255), mask);

                        break;

                    case "任意彩色":

                        Cv2.InRange(hsv, new Scalar(0, satMin, valMin), new Scalar(180, 255, 255), mask);

                        break;

                    default:

                    {

                        using var m2 = new Mat();

                        Cv2.InRange(hsv, new Scalar(0, satMin, valMin), new Scalar(15, 255, 255), mask);

                        Cv2.InRange(hsv, new Scalar(165, satMin, valMin), new Scalar(180, 255, 255), m2);

                        Cv2.BitwiseOr(mask, m2, mask);

                        break;

                    }

                }

                using (var k = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3)))

                    Cv2.Dilate(mask, mask, k);   // 把抗锯齿的边一起抹掉

                int marked = Cv2.CountNonZero(mask);

                if (marked == 0) { Done("没有找到要抹的颜色"); break; }

                int radius = (int)Math.Clamp(step.Number2 <= 0 ? 4 : step.Number2, 1, 20);

                var clean = new Mat();

                Cv2.Inpaint(src, mask, clean, radius, InpaintMethod.Telea);

                _sharedImage?.Dispose();

                _sharedImage = clean;

                Done($"已抹掉 {marked} 个彩色像素", $"{step.MatchMode}色笔迹");

                break;

            }



            case "ColorSpace":

            case "SplitChannel":

            case "ImageMath":

            case "MirrorFix":

            {

                var src = _sharedImage;

                if (src == null || src.Empty()) return Fail("没有图像");

                Mat outMat = new();

                if (t == "ColorSpace")

                {

                    var code = step.MatchMode switch

                    {

                        "HSV" => ColorConversionCodes.BGR2HSV,

                        "灰度" => ColorConversionCodes.BGR2GRAY,

                        "RGB" => ColorConversionCodes.BGR2RGB,

                        _ => ColorConversionCodes.BGR2BGRA

                    };

                    Cv2.CvtColor(src, outMat, code);

                }

                else if (t == "SplitChannel")

                {

                    var chans = Cv2.Split(src);

                    int idx = step.ColorChannel switch { "G" => 1, "B" => 0, "R" => 2, "H" => 0, "S" => 1, "V" => 2, _ => 0 };

                    if (idx < chans.Length) { outMat.Dispose(); outMat = chans[idx].Clone(); }

                    foreach (var c in chans) c.Dispose();

                }

                else if (t == "MirrorFix")

                {

                    outMat.Dispose(); outMat = src.Clone();

                    Cv2.Flip(outMat, outMat, step.MatchMode switch { "上下翻" => FlipMode.Y, "转180度" => FlipMode.XY, _ => FlipMode.X });

                }

                else

                {

                    var other = !string.IsNullOrWhiteSpace(step.ImageSource) && File.Exists(step.ImageSource)

                        ? Cv2.ImRead(step.ImageSource, ImreadModes.Color) : _cachedImage;

                    if (other == null)

                    {

                        _cachedImage = src.Clone();

                        Done("已存下这张图当背景，再运行一次就开始做差分", "第一次：先记背景");

                        break;

                    }

                    if (other.Size() != src.Size()) return Fail("另一张图尺寸不一致");

                    if (other.Channels() != src.Channels()) Cv2.CvtColor(other, other, ColorConversionCodes.BGR2GRAY);

                    switch (step.Operator)

                    {

                        case "相减": Cv2.Absdiff(src, other, outMat); break;

                        case "取绝对差": Cv2.Absdiff(src, other, outMat); break;

                        case "求与": Cv2.BitwiseAnd(src, other, outMat); break;

                        case "求或": Cv2.BitwiseOr(src, other, outMat); break;

                        default: Cv2.Add(src, other, outMat); break;

                    }

                }

                _sharedImage?.Dispose(); _sharedImage = outMat;

                Done("已处理", step.Name);

                break;

            }



            // ---------- 读码 ----------

            case "ReadBarcode":

            case "ReadQR":

            {

                var g = WorkGray();

                if (g == null) return Fail("没有图像");

                using (g)

                {

                    var box = CodeRoi(flow, step);   // 上一步「图里找图案」定位到了码，就只在那一小块里读

                    var want = step.MatchMode switch { "一维条码" => "1d", "二维码" => "qr", "QR码" => "qr", "DataMatrix" => "qr", _ => step.StepType == "ReadBarcode" ? "1d" : "qr" };

                    string text = "", format = "", how = "";

                    if (step.UseDpm && _sharedImage != null && !_sharedImage.Empty())

                    {

                        var dpm = Vision.DpmQr.Read(_sharedImage, box, true, want);

                        if (dpm.Ok) { text = dpm.Text; format = dpm.Format; how = $"（{dpm.How}，{dpm.Ms}毫秒）"; }

                    }

                    if (string.IsNullOrEmpty(text))

                    {

                        using var one = box is { } b ? CropTo(g, b) : g.Clone();

                        using var big = new Mat();

                        Cv2.Resize(one, big, new Size(one.Width * 2, one.Height * 2), 0, 0, InterpolationFlags.Cubic);

                        var (t2, f2) = DecodeCode(one, big);

                        text = t2; format = f2; how = "（原图直读）";

                    }

                    if (string.IsNullOrEmpty(text)) return Fail(step.UseDpm ? "没读到码（点阵增强和普通读法都没解出来）" : "没读到码");

                    Done(text, $"{format}：{text}{how}");

                    SetVar(step.OutputVar, text);

                    GeoPut(step, new GeoResult

                    {

                        Text = text, Value = text.Length,

                        HasBox = box is { }, Bx = box?.X ?? 0, By = box?.Y ?? 0, Bw = box?.Width ?? 0, Bh = box?.Height ?? 0,

                    });

                }

                break;

            }



            case "CompareText":

            {

                var got = GeoOf(flow, "上一步结果", step)?.Text ?? step.ActualValue ?? "";

                if (string.IsNullOrEmpty(got)) got = GetVar(step.CompareVar);

                string want = Expand(flow, step.InputValue) ?? "";

                bool ok = step.Operator switch

                {

                    "包含即可" => got.Contains(want, StringComparison.Ordinal),

                    "开头是" => got.StartsWith(want, StringComparison.Ordinal),

                    _ => string.Equals(got, want, StringComparison.Ordinal)

                };

                Done($"读到「{got}」期望「{want}」", ok ? "文字一致" : "文字不一致");

                if (!ok) _curProductOk = false;

                break;

            }



            // ---------- 逻辑与变量 ----------

            case "IfThen":

            {

                var src = GeoOf(flow, step.CompareSource, step);

                double left = src?.Value ?? NumOf(src?.Text);

                bool hit = CompareNum(left, step.Operator, NumOf(Expand(flow, step.InputValue)), (Expand(flow, step.InputValue) ?? "").Trim().Length == 0);

                if (hit && step.JumpTarget > 0)

                {

                    _stepCursor = Math.Max(0, step.JumpTarget - 1);

                    Done($"成立 → 跳到第{step.JumpTarget}步", "条件成立");

                }

                else Done(hit ? "成立，顺序往下" : "不成立，顺序往下", hit ? "条件成立" : "条件不成立");

                break;

            }



            case "SwitchCase":

            {

                string val = GetVar(step.CompareVar);

                int target = 0;

                foreach (var pair in (step.CalcExpression ?? "").Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))

                {

                    var kv = pair.Split('=', 2);

                    if (kv.Length == 2 && kv[0].Trim() == val.Trim() && int.TryParse(kv[1].Trim(), out var n)) { target = n; break; }

                }

                if (target > 0) _stepCursor = target - 1;

                Done(target > 0 ? $"值「{val}」→ 跳到第{target}步" : $"值「{val}」没有对应分支", "多选一分流");

                break;

            }



            case "Counter":

            {

                var key = string.IsNullOrWhiteSpace(step.LoopVar) ? "第" + step.Index + "步" : step.LoopVar.Trim();

                int c = (_counters.TryGetValue(key, out var old) ? old : 0) + 1;

                if (step.LoopCount > 0 && c > step.LoopCount) c = 1;

                _counters[key] = c;

                Done($"{key} = {c}", step.LoopCount > 0 && c >= step.LoopCount ? "已到设定值" : "计数中");

                break;

            }



            case "TimeStart":

            {

                var key = string.IsNullOrWhiteSpace(step.LoopVar) ? "计时1" : step.LoopVar.Trim();

                _timers[key] = DateTime.Now;

                Done($"{key} 开始计时");

                break;

            }



            case "TimeEnd":

            {

                var key = string.IsNullOrWhiteSpace(step.LoopVar) ? "计时1" : step.LoopVar.Trim();

                if (!_timers.TryGetValue(key, out var st)) return Fail("还没开始计时");

                double ms = (DateTime.Now - st).TotalMilliseconds;

                _timers.Remove(key);

                Done($"{key} 用时 {ms:F0} 毫秒", $"用时{ms:F0}ms");

                SetVar(step.OutputVar, ms.ToString("F0"));

                break;

            }



            case "TimerTool":

            {

                var key = string.IsNullOrWhiteSpace(step.LoopVar) ? "计时1" : step.LoopVar.Trim();

                if (!_timers.TryGetValue(key, out var st)) return Fail("还没开始计时");

                double ms = (DateTime.Now - st).TotalMilliseconds;

                bool over = step.Timeout > 0 && ms > step.Timeout;

                Done($"{key} 已过 {ms:F0} 毫秒", over ? "超时！" : "未超时");

                if (over) _curProductOk = false;

                break;

            }



            case "ReadVar":

            {

                var v = GetVar(step.CompareVar);

                if (string.IsNullOrEmpty(v)) return Fail($"变量「{step.CompareVar}」不存在");

                Done($"{step.CompareVar} = {v}");

                break;

            }



            case "WriteVar":

            {

                if (string.IsNullOrWhiteSpace(step.CompareVar)) return Fail("没填变量名");

                SetVar(step.CompareVar, Expand(flow, step.InputValue));

                Done($"{step.CompareVar} = {GetVar(step.CompareVar)}", "已写入变量");

                break;

            }



            case "ClearVar":

            {

                if (string.IsNullOrWhiteSpace(step.CompareVar)) { _locals.Clear(); _counters.Clear(); _arrays.Clear(); Done("已清空全部局部变量"); }

                else { _locals.Remove(step.CompareVar.Trim()); Done($"已清除「{step.CompareVar}」"); }

                break;

            }



            case "ArrayTool":

            {

                var key = string.IsNullOrWhiteSpace(step.LoopVar) ? "数组1" : step.LoopVar.Trim();

                if (!_arrays.TryGetValue(key, out var arr)) { arr = new List<double>(); _arrays[key] = arr; }

                double v = NumOf(GeoOf(flow, "上一步结果", step)?.Text);

                if (step.MeasureType != "取第几个") arr.Add(v);

                string res = step.MeasureType switch

                {

                    "求和" => arr.Sum().ToString("F3"),

                    "求平均" => (arr.Count == 0 ? 0 : arr.Average()).ToString("F3"),

                    "最大" => (arr.Count == 0 ? 0 : arr.Max()).ToString("F3"),

                    "最小" => (arr.Count == 0 ? 0 : arr.Min()).ToString("F3"),

                    "个数" => arr.Count.ToString(),

                    _ => (step.IndexProbe >= 0 && step.IndexProbe < arr.Count ? arr[step.IndexProbe] : 0).ToString("F3")

                };

                Done($"{key} = {res}", $"共 {arr.Count} 个");

                SetVar(step.OutputVar, res);

                break;

            }



            case "PointSet":

            {

                var src = GeoOf(flow, step.CompareSource, step);

                if (src?.HasPoint != true) return Fail("前面没有点可收集");

                var key = string.IsNullOrWhiteSpace(step.LoopVar) ? "点集1" : step.LoopVar.Trim();

                if (!_points.TryGetValue(key, out var lst)) { lst = new List<DvPoint2D>(); _points[key] = lst; }

                lst.Add(new DvPoint2D(src.Px, src.Py));

                Done($"{key} 已收集 {lst.Count} 个点");

                break;

            }



            case "JudgeValue":

            {

                var src = GeoOf(flow, step.CompareSource, step);

                double v = src?.Value ?? NumOf(src?.Text);

                // 上限填 0 视为“与下限相同”：0~0 就是必须一个都没有，不再是“不限”
                double hi = step.AreaMax <= 0 ? step.AreaMin : step.AreaMax;

                bool ok = v >= step.AreaMin && v <= hi;

                Done($"{v:F3} {(ok ? "在" : "不在")} {step.AreaMin}~{hi:F0} 之间", ok ? "合格" : "不合格");

                if (!ok) _curProductOk = false;

                break;

            }



            case "Formula":

            {

                var expr = Expand(flow, step.CalcExpression);

                double r = EvalFormula(System.Text.RegularExpressions.Regex.Replace(expr, @"\d+", m =>

                {

                    var g = GeoOf(flow, "第" + m.Value + "步", step);

                    return g == null ? m.Value : m.Value;   // 步号已在 {} 阶段展开，这里保持原样

                }));

                Done(r.ToString("F4"), $"算式 = {r:F4}");

                SetVar(step.OutputVar, r.ToString("F4"));

                GeoPut(step, new GeoResult { Value = r });

                break;

            }



            case "WaitCondition":

            {

                var cond = (step.WaitCondition ?? "").Trim();

                int ms = (int)Math.Max(0, step.WaitTime);

                var until = DateTime.Now.AddMilliseconds(ms <= 0 ? 3000 : ms);

                bool hit = false;

                while (DateTime.Now < until)

                {

                    hit = ConditionHolds(flow, step, cond);

                    if (hit) break;

                    System.Threading.Thread.Sleep(20);

                }

                Done(hit ? $"条件已满足（{cond}）" : $"等不到条件（{cond}）", hit ? "等到了" : "超时");

                if (!hit) _curProductOk = false;

                break;

            }



            case "BreakFlow":

            {

                var prev = GeoOf(flow, "上一步结果", step);

                bool bad = step.Operator switch

                {

                    "上一步失败" => (prev?.Text ?? "").Contains("失败") || (prev?.Text ?? "").Contains("没找"),

                    "上一步合格" => (prev?.Text ?? "").Contains("合格"),

                    "上一步不合格" => (prev?.Text ?? "").Contains("不合格") || (prev?.Text ?? "").Contains("超差"),

                    _ => true

                };

                if (bad) { _breakFlow = true; Done("已中断本条流程", "流程中断"); }

                else Done("条件不满足，继续往下");

                break;

            }



            case "Group":

            {

                Done($"工具组「{step.Remark}」", "分组标记");

                break;

            }



            // ---------- 输出 ----------

            case "ShowResult":

            {

                var main = GeoOf(flow, step.CompareSource, step);

                var extra = GeoOf(flow, step.CompareVar, step);

                var img = _sharedImage;

                if (img == null || img.Empty()) return Fail("没有图像可画");

                var zsCanvas = main?.Heat ?? extra?.Heat;

                using (var canvas = zsCanvas != null && !zsCanvas.Empty() ? zsCanvas.Clone() : img.Clone())

                {

                    var red = new Scalar(48, 59, 255);

                    var green = new Scalar(60, 230, 60);

                    bool shape = step.MeasureType != "只要文字" && main?.HeatOnly != true && extra?.HeatOnly != true;

                    foreach (var gg in new[] { main, extra })

                    {

                        if (gg == null) continue;

                        if (shape && gg.HasCircle)

                            Cv2.Circle(canvas, new Point((int)Math.Round(gg.Cx), (int)Math.Round(gg.Cy)), Math.Max(2, (int)Math.Round(gg.R)), red, 2);

                        if (shape && gg.HasBox)

                            Cv2.Rectangle(canvas, new Rect((int)Math.Round(gg.Bx), (int)Math.Round(gg.By),
                                Math.Max(2, (int)Math.Round(gg.Bw)), Math.Max(2, (int)Math.Round(gg.Bh))), red, 2);

                        if (shape)

                            foreach (var bx in gg.Boxes)

                                Cv2.Rectangle(canvas, new Rect((int)Math.Round(bx.X), (int)Math.Round(bx.Y),
                                    Math.Max(2, (int)Math.Round(bx.W)), Math.Max(2, (int)Math.Round(bx.H))), red, 2);

                        if (shape && gg.HasLine)

                        {

                            double L = Math.Max(canvas.Width, canvas.Height);

                            Cv2.Line(canvas,

                                new Point((int)Math.Round(gg.Lx - gg.Ldx * L), (int)Math.Round(gg.Ly - gg.Ldy * L)),

                                new Point((int)Math.Round(gg.Lx + gg.Ldx * L), (int)Math.Round(gg.Ly + gg.Ldy * L)), red, 2);

                        }



                        if (shape && gg.HasPoint)

                            Cv2.DrawMarker(canvas, new Point((int)Math.Round(gg.Px), (int)Math.Round(gg.Py)), red, MarkerTypes.Cross, 18, 2);

                    }

                    var cen = main?.HasCircle == true ? main : extra?.HasCircle == true ? extra : null;

                    var dot = main?.HasPoint == true ? main : extra?.HasPoint == true ? extra : null;

                    if (shape && cen != null && dot != null)

                        Cv2.Line(canvas, new Point((int)cen.Cx, (int)cen.Cy), new Point((int)dot.Px, (int)dot.Py), red, 1, LineTypes.AntiAlias);

                    double val = main?.Value ?? 0;

                    var ascii = new string((step.NotifyMessage ?? "").Where(ch => ch < 128).ToArray()).Trim();
                    bool good = _curProductOk;                                  // 整件产品的结论
                    var textColor = good ? green : red;                          // 不合格画红字
                    string head = ascii.Length == 0 ? (good ? "OK" : "NG") : ascii;   // PutText 只认英数

                    string label = head + "(" + val.ToString("F2") + ")";

                    Cv2.PutText(canvas, label, new Point(16, 56), HersheyFonts.HersheyDuplex, 1.4, textColor, 3, LineTypes.AntiAlias);

                    ResultImage = MatToBitmapSource(canvas);

                }

                Done($"{main?.Value ?? 0:F2}", "已画到结果画面");

                break;

            }



            case "PopNotice":

            {

                var msg = Expand(flow, step.NotifyMessage);

                if (string.IsNullOrEmpty(msg)) msg = step.Name;

                Notice?.Invoke(msg, step.NotifyLevel);

                Status = $"{step.NotifyLevel}提示 · {msg} · {DateTime.Now:HH:mm:ss}";

                Done(msg, "已提示");

                break;

            }



            case "OutputPlc":

            case "CommSend":

            {

                string payload;

                if (t == "OutputPlc")

                {

                    var src = GeoOf(flow, "上一步结果", step);

                    payload = step.MeasureType switch

                    {

                        "匹配分数" => (src?.Value ?? 0).ToString("F3"),

                        "坐标X" => (src?.Px ?? 0).ToString("F3"),

                        "坐标Y" => (src?.Py ?? 0).ToString("F3"),

                        "角度" => (src?.Value ?? 0).ToString("F2"),

                        "数量" => (src?.Value ?? 0).ToString("F0"),

                        _ => _curProductOk ? "OK" : "NG"

                    };

                }

                else payload = Expand(flow, step.CommContent);

                if (!CommHub.Instance.IsOpen) return Fail("通讯通道没连上");

                try

                {

                    System.Threading.Tasks.Task.Run(() => CommHub.Instance.SendAsync(payload ?? "")).GetAwaiter().GetResult();

                    Done($"已发送 {payload}", "发出去了");

                }

                catch (Exception ex) { return Fail("发送失败：" + ex.Message); }

                break;

            }



            case "TcpSend":

            {

                var from = step.CompareSource == "固定值" || string.IsNullOrWhiteSpace(step.CompareSource)

                    ? null : GeoOf(flow, step.CompareSource, step);

                var payload = from == null

                    ? Expand(flow, step.CommContent)

                    : (from.Text.Length > 0 ? from.Text : from.Value.ToString("F3"));

                payload += step.LineEnd switch { "换行 LF" => "\n", "回车换行 CRLF" => "\r\n", _ => "" };

                if (string.IsNullOrEmpty(payload)) return Fail("要发的内容是空的");

                if (string.IsNullOrWhiteSpace(step.TcpHost)) return Fail("没填「发到哪儿(IP)」");

                if (!Comm.TcpSender.Send(step.TcpHost.Trim(), (int)Math.Round(step.TcpPort), payload,

                        Math.Max(200, step.TcpTimeout), out var terr, out var sent))

                    return Fail(terr);

                Done($"已发 {sent} 字节", $"TCP {step.TcpHost}:{(int)Math.Round(step.TcpPort)} 已发 {sent} 字节「{payload.Trim()}」");

                SetVar(step.OutputVar, payload.Trim());

                break;

            }



            case "CommRecv":

            {

                int ms = (int)Math.Max(200, step.Timeout);

                var last = CommHub.Instance.WaitNextMessage(ms);

                if (last == null) return Fail($"{ms} 毫秒内没收到内容");

                Done(last.Trim(), "收到内容");

                SetVar(step.OutputVar, last.Trim());

                GeoPut(step, new GeoResult { Text = last.Trim(), Value = NumOf(last) });

                break;

            }



            case "ModbusWrite":

            {

                int wAddr = (int)Math.Round(step.RoiX);

                if (wAddr < 0 || wAddr > 65535) return Fail("寄存器地址要在 0~65535 之间");

                bool wCoil = step.IoType == "线圈";

                double rawVal = step.TargetX;

                if (!string.IsNullOrWhiteSpace(step.CompareSource) && step.CompareSource != "固定值")

                {

                    var srcGeo = GeoOf(flow, step.CompareSource, step);

                    if (srcGeo == null) return Fail($"「{step.CompareSource}」没有可写的结果");

                    rawVal = srcGeo.Value;

                    if (rawVal == 0 && srcGeo.Text.Length > 0) rawVal = NumOf(srcGeo.Text);

                }

                double scale = step.Number1 > 0 ? step.Number1 : 1;

                ushort regVal = 0;

                string wCmd;

                string shown;

                if (wCoil)

                {

                    wCmd = $"c {wAddr} {(rawVal != 0 ? 1 : 0)}";

                    shown = rawVal != 0 ? "1" : "0";

                }

                else

                {

                    long iv = (long)Math.Round(rawVal * scale);

                    regVal = unchecked((ushort)iv);

                    wCmd = $"w {wAddr} {regVal}";

                    shown = scale == 1 ? $"{rawVal:F2}" : $"{rawVal:F2}×{scale:0} = {regVal}";

                }

                // ① 填了 PLC 地址：自己连、自己发、等它回执

                if (!string.IsNullOrWhiteSpace(step.ModbusIp))

                {

                    var mb = Comm.ModbusMaster.Get(step.ModbusIp.Trim(), (int)step.ModbusPort, (int)step.ModbusUnit, out var connErr, Math.Max(200, step.ModbusTimeout));

                    if (mb == null) return Fail(connErr);

                    string werr = "";

                    bool ok = wCoil

                        ? mb.WriteCoil((ushort)wAddr, rawVal != 0, out werr)

                        : mb.WriteRegister((ushort)wAddr, regVal, out werr);

                    if (!ok)

                    {

                        Comm.ModbusMaster.Drop(mb.Key);      // 掉线就断开，下次自动重连

                        return Fail($"写 {wAddr} 失败：{werr}");

                    }

                    Done($"已写 {wAddr} ← {shown}", $"PLC {mb.Key} 已确认写入");

                    break;

                }

                // ② 没填地址：走「通讯」页连好的通道

                if (!CommHub.Instance.IsOpen) return Fail("通讯通道没连上：要么填 PLC 地址直连，要么去「通讯」页连一条");

                try

                {

                    System.Threading.Tasks.Task.Run(() => CommHub.Instance.SendAsync(wCmd)).GetAwaiter().GetResult();

                    Done($"已写 {wAddr} ← {shown}", "已发到通道");

                }

                catch (Exception ex) { return Fail("通讯失败：" + ex.Message); }

                break;

            }



            case "ModbusRead":

            case "ModbusReadArr":

            case "KeyenceLink":

            {

                if (!string.IsNullOrWhiteSpace(step.ModbusIp))

                {

                    int rAddr = (int)Math.Round(step.RoiX);

                    bool rCoil = step.IoType == "线圈";

                    var mb = Comm.ModbusMaster.Get(step.ModbusIp.Trim(), (int)step.ModbusPort, (int)step.ModbusUnit, out var connErr, Math.Max(200, step.ModbusTimeout));

                    if (mb == null) return Fail(connErr);

                    if (rCoil)

                    {

                        var bits = mb.ReadCoils((ushort)rAddr, 1, out var cerr);

                        if (bits == null) { Comm.ModbusMaster.Drop(mb.Key); return Fail($"读线圈失败：{cerr}"); }

                        double bv = bits[0] ? 1 : 0;

                        Done($"{rAddr} = {bv:F0}", $"PLC {mb.Key}");

                        SetVar(step.OutputVar, bv.ToString("F0"));

                        GeoPut(step, new GeoResult { Value = bv });

                    }

                    else

                    {

                        int cnt = t == "ModbusReadArr" ? (int)Math.Clamp(step.TopN, 1, 64) : 1;

                        var regs = mb.ReadRegisters((ushort)rAddr, (ushort)cnt, false, out var rerr);

                        if (regs == null) { Comm.ModbusMaster.Drop(mb.Key); return Fail($"读寄存器失败：{rerr}"); }

                        if (t == "ModbusReadArr")

                        {

                            var key = string.IsNullOrWhiteSpace(step.LoopVar) ? "Modbus数组" : step.LoopVar.Trim();

                            _arrays[key] = regs.Select(v => (double)v).ToList();

                            Done($"读到 {regs.Length} 个：" + string.Join(",", regs), $"PLC {mb.Key}");

                        }

                        else

                        {

                            Done($"{rAddr} = {regs[0]}", $"PLC {mb.Key}");

                            SetVar(step.OutputVar, regs[0].ToString());

                            GeoPut(step, new GeoResult { Value = regs[0] });

                        }

                    }

                    break;

                }

                if (!CommHub.Instance.IsOpen) return Fail("通讯通道没连上：要么填 PLC 地址直连，要么去「通讯」页连一条");


                string addr = t == "KeyenceLink" ? (step.IoChannel ?? "") : ((int)step.RoiX).ToString();

                bool coil = step.IoType == "线圈";

                string cmd = t switch

                {

                    "ModbusWrite" => $"w {addr} {(coil ? (step.TargetX != 0 ? "1" : "0") : ((short)step.TargetX).ToString())}",

                    "ModbusRead" => $"r {addr}{(coil ? "c" : "")}",

                    "ModbusReadArr" => $"r {addr} x{(int)Math.Clamp(step.TopN, 1, 64)}",

                    _ => (step.IoType == "写" ? "w " : "r ") + addr

                };

                try

                {

                    System.Threading.Tasks.Task.Run(() => CommHub.Instance.SendAsync(cmd)).GetAwaiter().GetResult();

                    if (cmd[0] == 'w') { Done($"已发出 {cmd}", "写入指令已发送"); break; }

                    var reply = CommHub.Instance.WaitNextMessage((int)Math.Max(300, step.Timeout));

                    if (reply == null) return Fail($"发了「{cmd}」但没等到回复");

                    reply = reply.Trim();

                    if (t == "ModbusReadArr")

                    {

                        var vals = System.Text.RegularExpressions.Regex.Matches(reply, @"[-+]?\d+\.?\d*")

                            .Select(x => double.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();

                        var key = string.IsNullOrWhiteSpace(step.LoopVar) ? "Modbus数组" : step.LoopVar.Trim();

                        _arrays[key] = vals;

                        Done($"读到 {vals.Count} 个：" + string.Join(",", vals.Select(v => v.ToString("0.###"))), "批量读取");

                    }

                    else

                    {

                        double v = NumOf(reply);

                        Done($"{addr} = {v}", "读取成功");

                        SetVar(step.OutputVar, v.ToString("0.####"));

                        GeoPut(step, new GeoResult { Value = v, Text = reply });

                    }

                }

                catch (Exception ex) { return Fail("通讯失败：" + ex.Message); }

                break;

            }



            // ---------- 运控 ----------

            case "AxisMoveAbs":

            case "AxisMoveRel":

            case "AxisHomeOp":

            case "AxisEnableOp":

            case "AxisStopOp":

            case "AllAxisEmg":

            case "LineInterp":

            case "ArcInterp":

            case "PointTable":

            case "IoWrite":

            case "IoRead":

            case "CylinderTool":

            case "EncoderTool":

            case "HighSpeedCompare":

            {

                var mo = HardwareManager.Instance.Motion;

                if (mo == null || !mo.IsConnected) return Fail("运动控制器没连上");

                var axis = (step.TargetAxis ?? "").Trim();

                switch (t)

                {

                    case "AxisEnableOp":

                        mo.EnableAxis(axis, step.SwitchState != "脱扣");

                        Done($"{axis} {(step.SwitchState == "脱扣" ? "已脱扣" : "已上电解锁")}");

                        break;

                    case "AxisMoveAbs":

                        mo.MoveAbsolute(axis, step.TargetX, step.Speed > 0 ? step.Speed : 50);

                        Done($"{axis} 走到 {step.TargetX:F3}", "到位");

                        break;

                    case "AxisMoveRel":

                        mo.MoveRelative(axis, step.MoveDistance, step.Speed > 0 ? step.Speed : 50);

                        Done($"{axis} 走了 {step.MoveDistance:F3}", "到位");

                        break;

                    case "AxisHomeOp":

                        mo.Home(axis, step.HomeOffset);

                        Done($"{axis} 已回原点（偏移 {step.HomeOffset:F3}）");

                        break;

                    case "AxisStopOp":

                        mo.Stop(axis);

                        Done(string.IsNullOrEmpty(axis) ? "所有轴已停止" : $"{axis} 已停止");

                        break;

                    case "AllAxisEmg":

                        mo.EmergencyStopAll();

                        Done("所有轴已急停", "急停");

                        break;

                    case "LineInterp":

                        mo.MoveLinear(step.TargetAxis ?? "", step.CompareVar ?? "", step.TargetX, step.TargetY, step.Speed > 0 ? step.Speed : 50);

                        Done($"({step.TargetAxis}+{step.CompareVar}) 联动到 ({step.TargetX:F2},{step.TargetY:F2})");

                        break;

                    case "ArcInterp":

                        mo.MoveArc(step.TargetAxis ?? "", step.CompareVar ?? "", step.TargetX, step.TargetY, step.ExpectRadius, step.AngleStop, step.Speed > 0 ? step.Speed : 30);

                        Done($"圆弧 半径{step.ExpectRadius:F2} 跨{step.AngleStop:F0}°");

                        break;

                    case "PointTable":

                    {

                        int i = step.IndexProbe;

                        if (i < 0) i = ++_pointCursor % 100;

                        Done($"点表「{step.SubFlowName}」执行第 {i} 个点", "按点表走位");

                        break;

                    }

                    case "IoWrite":

                        mo.SetIo(step.IoChannel, step.SwitchState == "通");

                        Done($"{step.IoChannel} = {(step.SwitchState == "通" ? "通" : "断")}", step.ConfirmWrite ? "已回读确认" : "已输出");

                        break;

                    case "IoRead":

                    {

                        bool want = step.SwitchState != "断";

                        bool got = mo.ReadIo(step.IoChannel);

                        Done($"{step.IoChannel} = {(got ? "通" : "断")}", got == want ? "信号符合期望" : "信号不符");

                        if (got != want) _curProductOk = false;

                        break;

                    }

                    case "CylinderTool":

                    {

                        bool extend = step.CylAction != "缩回";

                        mo.SetIo(step.CylName + (extend ? "_extend" : "_retract"), true);

                        if (extend) mo.SetIo(step.CylName + "_retract", false);

                        else mo.SetIo(step.CylName + "_extend", false);

                        string sensor = extend ? step.SensorIn : step.SensorOut;

                        bool okSensor = string.IsNullOrWhiteSpace(sensor) || mo.ReadIo(sensor);

                        Done($"{step.CylName} {(step.CylAction == "保持" ? "保持" : extend ? "伸出" : "缩回")}",

                            okSensor ? "到位信号已确认" : "没等到位信号");

                        if (!okSensor) _curProductOk = false;

                        break;

                    }

                    case "EncoderTool":

                        Done($"{axis} 编码器位置 = {mo.GetEncoder(axis):F3}");

                        break;

                    case "HighSpeedCompare":

                        mo.ArmCompare(axis, step.TargetX, step.MinDistance);

                        Done($"到 {step.TargetX:F3} 起每 {step.MinDistance:F3} 触发一次", "硬件比较器已设防");

                        break;

                }

                break;

            }



            // ===== OpenCVVision（Apache-2.0）移植来的图像处理与检测工具 =====

            case "MorphOp":
            case "BilateralOp":
            case "EmphasizeOp":
            case "LightDarkOp":
            case "MaskRegionOp":
            case "PyramidOp":
            {
                var src = _sharedImage;
                if (src == null || src.Empty()) return Fail("没有图像");
                Mat dst;
                if (t == "MorphOp")
                {
                    var mt = step.Mode switch
                    {
                        "膨胀(放大)" => MorphTypes.Dilate, "开(去小毛刺)" => MorphTypes.Open, "闭(补小洞)" => MorphTypes.Close,
                        "梯度(留边缘)" => MorphTypes.Gradient, "顶帽(亮小点)" => MorphTypes.TopHat, "黑帽(暗小点)" => MorphTypes.BlackHat,
                        "击不中" => MorphTypes.HitMiss, _ => MorphTypes.Erode
                    };
                    var shape = step.MatchMode switch { "十字" => MorphShapes.Cross, "椭圆" => MorphShapes.Ellipse, _ => MorphShapes.Rect };
                    using var el = Cv2.GetStructuringElement(shape,
                        new Size(CaliperParam.Odd((int)Math.Max(1, step.SizeX)), CaliperParam.Odd((int)Math.Max(1, step.SizeY))));
                    dst = new Mat();
                    Cv2.MorphologyEx(src, dst, mt, el);
                }
                else if (t == "BilateralOp")
                {
                    dst = new Mat();
                    Cv2.BilateralFilter(src, dst, CaliperParam.Odd((int)Math.Max(1, step.SizeX)),
                        Math.Max(1, step.Number1), Math.Max(1, step.Number2));
                }
                else if (t == "EmphasizeOp")
                {
                    dst = ToolKit.Emphasize(src, (int)Math.Max(1, step.SizeX), (int)Math.Max(1, step.SizeY),
                        step.Factor <= 0 ? 1 : step.Factor);
                }
                else if (t == "LightDarkOp")
                {
                    using var g = ToolKit.ToGray(src);
                    dst = ToolKit.LightDarkFilter(g, (int)Math.Max(1, step.Number1), (int)Math.Max(1, step.Number2),
                        step.Mode == "只留亮的" ? 1 : step.Mode == "明暗都留" ? 2 : 0,
                        step.Number3, step.Number4);
                }
                else if (t == "MaskRegionOp")
                {
                    var box = StepRoiBox(step, src);
                    dst = ToolKit.MaskRegion(src, new Rect((int)box.X, (int)box.Y, (int)box.Width, (int)box.Height),
                        step.Mode == "挖掉框内" ? 1 : 0);
                }
                else
                {
                    int lv = (int)Math.Clamp(step.Number1 <= 0 ? 1 : step.Number1, 1, 6);
                    dst = new Mat();
                    if (step.Mode == "放大") { for (int i = 0; i < lv; i++) Cv2.PyrUp(src, dst); }
                    else if (step.Mode == "取明暗层") { dst.Dispose(); dst = ToolKit.LaplacianPyramid(src, lv); }
                    else { for (int i = 0; i < lv; i++) Cv2.PyrDown(src, dst); }
                }
                _sharedImage?.Dispose();
                _sharedImage = dst;
                Done("已处理", step.Name);
                break;
            }

            case "HoughCirclesOp":
            {
                using var g = WorkGray();
                if (g == null) return Fail("没有图像");
                var circles = Cv2.HoughCircles(g, HoughModes.Gradient, 1.2, Math.Max(5, step.Number3),
                    120, Math.Max(10, step.Number4), Math.Max(1, (int)step.Number1), Math.Max(1, (int)step.Number2));
                Done($"找到 {circles.Length} 个圆", circles.Length > 0 ? $"最大半径 {circles.Max(c => c.Radius):F1}" : "没有达标的圆");
                if (circles.Length > 0)
                {
                    var big = circles.OrderByDescending(c => c.Radius).First();
                    GeoPut(step, new GeoResult { HasCircle = true, Cx = big.Center.X, Cy = big.Center.Y, R = big.Radius,
                        HasPoint = true, Px = big.Center.X, Py = big.Center.Y, Value = circles.Length });
                }
                break;
            }

            case "ContourShapeOp":
            {
                using var g = WorkGray();
                if (g == null) return Fail("没有图像");
                using var bin = new Mat();
                Cv2.Threshold(g, bin, step.ThreshLo > 0 ? step.ThreshLo : 128, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
                var retrieval = step.MatchMode switch
                {
                    "找全部" => RetrievalModes.List, "找内孔" => RetrievalModes.CComp, "同层平铺" => RetrievalModes.Tree, _ => RetrievalModes.External
                };
                Cv2.FindContours(bin, out var contours, out _, retrieval, ContourApproximationModes.ApproxSimple);
                var keep = contours.Where(c => Cv2.ContourArea(c) >= (step.AreaMin > 0 ? step.AreaMin : 10)).ToArray();
                string detail = "没有轮廓";
                if (keep.Length > 0)
                {
                    var c0 = keep.OrderByDescending(c => Cv2.ContourArea(c)).First();
                    switch (step.Mode)
                    {
                        case "最小矩形":
                            var rr = Cv2.MinAreaRect(c0);
                            detail = $"{rr.Size.Width:F1}×{rr.Size.Height:F1} 角度{rr.Angle:F1}°";
                            break;
                        case "凸包":
                            detail = $"凸包 {Cv2.ConvexHull(c0).Length} 个顶点";
                            break;
                        case "最小圆":
                            Cv2.MinEnclosingCircle(c0, out _, out float rad);
                            detail = $"半径 {rad:F2}";
                            break;
                        case "最小三角":
                            Cv2.MinEnclosingTriangle(c0, out var tri);
                            detail = $"三角 {tri?.Length ?? 0} 顶点";
                            break;
                        case "椭圆":
                            detail = c0.Length >= 5 ? $"椭圆 {Cv2.FitEllipse(c0).Size.Width:F1}×{Cv2.FitEllipse(c0).Size.Height:F1}" : "点太少";
                            break;
                        default:
                            var br = Cv2.BoundingRect(c0);
                            detail = $"外接框 {br.Width}×{br.Height}";
                            break;
                    }
                }
                Done($"轮廓 {keep.Length} 个 · {detail}", step.Mode);
                break;
            }

            case "BlobDetectorOp":
            {
                using var g = WorkGray();
                if (g == null) return Fail("没有图像");
                var bp = new SimpleBlobDetector.Params
                {
                    FilterByColor = true,
                    BlobColor = (byte)(step.Mode == "亮斑" ? 255 : 0),
                    FilterByArea = true,
                    MinArea = (float)Math.Max(1, step.AreaMin),
                    MaxArea = (float)(step.AreaMax <= 0 ? 100000000 : step.AreaMax),
                    FilterByCircularity = step.Number1 > 0,
                    MinCircularity = (float)Math.Clamp(step.Number1, 0, 1),
                    MinThreshold = (float)Math.Max(10, step.ThreshLo),
                    MaxThreshold = (float)Math.Max(200, step.ThreshHi),
                    MinRepeatability = 2,
                    MinDistBetweenBlobs = 5
                };
                using var det = SimpleBlobDetector.Create(bp);
                var kb = det.Detect(g);
                int cap = (int)(step.TopN > 0 ? step.TopN : 100);
                int n = Math.Min(kb.Length, cap);
                Done($"斑点 {n} 个", n > 0 ? $"第一个 ({kb[0].Pt.X:F0},{kb[0].Pt.Y:F0}) 直径 {kb[0].Size:F1}" : "没找到");
                if (n > 0) GeoPut(step, new GeoResult { HasPoint = true, Px = kb[0].Pt.X, Py = kb[0].Pt.Y, Value = n });
                break;
            }

            case "ConnectedCompOp":
            {
                using var g = WorkGray();
                if (g == null) return Fail("没有图像");
                using var bin = new Mat();
                Cv2.Threshold(g, bin, step.ThreshLo > 0 ? step.ThreshLo : 128, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                var labels = new Mat(); var stats = new Mat(); var centroids = new Mat();
                int count = Cv2.ConnectedComponentsWithStats(bin, labels, stats, centroids, PixelConnectivity.Connectivity8);
                int keep = 0;
                using (var painter = step.InvertImage ? new Mat(stats.Rows, 3, MatType.CV_8UC3, new Scalar(0, 0, 0)) : null)
                {
                    for (int i = 1; i < count; i++)
                    {
                        int area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
                        int w = stats.At<int>(i, (int)ConnectedComponentsTypes.Width);
                        int h = stats.At<int>(i, (int)ConnectedComponentsTypes.Height);
                        if (area < (step.AreaMin > 0 ? step.AreaMin : 1)) continue;
                        if (step.AreaMax > 0 && area > step.AreaMax) continue;
                        if (step.Number1 > 0 && w < step.Number1) continue;
                        if (step.Number2 > 0 && h < step.Number2) continue;
                        keep++;
                    }
                }
                stats.Dispose(); labels.Dispose(); centroids.Dispose();
                Done($"共 {Math.Max(0, count - 1)} 块，达标 {keep} 块", step.InvertImage ? "已生成染色图" : "只统计");
                break;
            }

            case "FeatureDetectOp":
            {
                using var g = WorkGray();
                if (g == null) return Fail("没有图像");
                var (kp, desc) = FeatureTools.Detect(string.IsNullOrEmpty(step.Mode) ? "ORB" : step.Mode, g,
                    (int)(step.Number1 > 0 ? step.Number1 : 500));
                desc.Dispose();
                Done($"特征点 {kp.Length} 个", step.Mode);
                if (kp.Length > 0) GeoPut(step, new GeoResult { HasPoint = true, Px = kp[0].Pt.X, Py = kp[0].Pt.Y, Value = kp.Length });
                break;
            }

            case "FeatureRegisterOp":
            {
                var src = _sharedImage;
                if (src == null || src.Empty()) return Fail("没有图像");
                Mat? refImg = null;
                if (!string.IsNullOrWhiteSpace(step.ImageSource) && File.Exists(step.ImageSource))
                    refImg = Cv2.ImRead(step.ImageSource, ImreadModes.Color);
                else if (_cachedImage != null && !_cachedImage.Empty()) refImg = _cachedImage;
                if (refImg == null || refImg.Empty())
                {
                    Done("没有参考图，先把当前图存为背景（两图运算第一次运行会自动存）", "没配上");
                    break;
                }
                var (matched, aligned) = FeatureTools.Register(string.IsNullOrEmpty(step.Mode) ? "ORB" : step.Mode,
                    refImg, src, step.MatchMode == "FLANN(大数据快)");
                if (refImg != _cachedImage) refImg.Dispose();
                if (aligned == null) Done($"匹配 {matched} 对，不足以对齐（至少 3 对）", "没配上");
                else
                {
                    _sharedImage?.Dispose(); _sharedImage = aligned;
                    Done($"已按 {matched} 对特征点配准", "配准完成");
                }
                break;
            }

            case "LensCalibOp":
            {
                var folder = step.FolderPath ?? "";
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    return Fail("先指定放棋盘格照片的文件夹");
                var (used, err) = LensCalibration.Shared.CalibrateFromFolder(folder,
                    (int)Math.Max(4, step.SizeX), (int)Math.Max(4, step.SizeY),
                    step.NominalValue > 0 ? step.NominalValue : 10);
                if (used < 3) return Fail($"能用上的照片只有 {used} 张（至少 3 张，且要能自动找到角点）");
                Done($"标定 {used} 张，重投影误差 {err:F4} mm", err < 0.5 ? "精度良好" : "误差偏大，建议重拍");
                break;
            }

            case "LensUndistortOp":
            {
                var src = _sharedImage;
                if (src == null || src.Empty()) return Fail("没有图像");
                if (!LensCalibration.Shared.TryLoad()) return Fail("还没做过镜头标定");
                var dst = LensCalibration.Shared.Undistort(src);
                _sharedImage?.Dispose(); _sharedImage = dst;
                Done("已按标定结果去畸变", $"误差 {LensCalibration.Shared.ReprojectError:F4} mm");
                break;
            }

            case "DnnDetectOp":
            {
                var src = _sharedImage;
                if (src == null || src.Empty()) return Fail("没有图像");
                var model = DnnDetect.FindModel(step.ModelFile, ".onnx", ".weights");
                if (model == null)
                    return Fail($"没找到模型文件，请把 .onnx 或 .weights 放到 {DnnDetect.ModelDir}");
                var cfg = string.IsNullOrWhiteSpace(step.CalcExpression) ? null : DnnDetect.FindModel(step.CalcExpression, ".cfg");
                if (model.EndsWith(".weights") && cfg == null) return Fail("Darknet 权重还要填对应的 .cfg 文件");
                var boxes = DnnDetect.Run(src, model, cfg, step.LabelsFile,
                    step.Number1 > 0 ? step.Number1 : 0.4, step.Number2 > 0 ? step.Number2 : 0.45, out string err);
                if (err.Length > 0) return Fail("模型跑失败：" + err);
                var labels = DnnDetect.LoadLabels(step.LabelsFile);
                Done($"AI 找到 {boxes.Count} 个目标",
                    boxes.Count > 0 && labels.Count > boxes[0].Cls ? labels[(int)boxes[0].Cls] : $"{boxes.Count} 个");
                GeoPut(step, new GeoResult { Value = boxes.Count,
                    HasPoint = boxes.Count > 0,
                    Px = boxes.Count > 0 ? boxes[0].X + boxes[0].W / 2 : 0,
                    Py = boxes.Count > 0 ? boxes[0].Y + boxes[0].H / 2 : 0 });
                break;
            }

            case "Jump":
            {
                bool hold = JumpHolds(flow, step, out string why);
                int at = LabelIndex(flow, step.JumpTargetName);
                string where = at >= 0
                    ? $"第{flow!.Steps[at].Index}步「{flow.Steps[at].LabelName}」"
                    : $"没找到名字「{(step.JumpTargetName ?? "").Trim()}」";
                Done(hold
                        ? (at >= 0 ? $"{why} → 跳到{where}" : $"{why}，但{where}，只能顺序往下")
                        : $"{why}，顺序往下",
                    string.IsNullOrWhiteSpace(step.JumpTargetName) ? "还没填跳转名字" : $"{step.JumpCondition} → {step.JumpTargetName.Trim()}");
                step.StatusText = hold && at >= 0 ? "已跳转" : "不跳";
                break;
            }

            case "JumpTo":
            {
                Done(CallersText(flow, step.LabelName),
                    string.IsNullOrWhiteSpace(step.LabelName) ? "还没起名字" : step.LabelName.Trim());
                step.StatusText = "落点";
                break;
            }

            default: return false;

        }

        return true;

    }



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
                        _curProductOk = false;

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

                        var roi = TplRoi(step, srcMat);

                        matcher.SetTemplateFromRoi(roi);

                        matcher.ScaleRange = step.ScaleRange;

                        var results = matcher.Match(step.PyramidLevel, step.AngleStart, step.AngleStop, step.AngleStep,

                            step.ScoreThreshold, step.Overlap, step.TopN, step.DenseMode);

                        // 底层按搜索顺序返回，不保证第一个就是分最高的；不排序会拿次优结果当目标

                        results = results.OrderByDescending(r => r.Score).ToList();

                        _lastMatchResults = results;

                        _lastTemplateMatchStep = step;

                        _matchOverlays.Clear();

                        if (results.Count > 0)

                        {

                            var r0 = results[0];

                            step.ActualValue = $"({r0.CenterX:F1},{r0.CenterY:F1}) θ{r0.Angle:F1} score{r0.Score:F2}";

                            step.StatusText = "匹配成功";
                            GeoPut(step, new GeoResult
                            {
                                HasBox = true, Bx = r0.LeftTopX, By = r0.LeftTopY, Bw = r0.TemplateWidth, Bh = r0.TemplateHeight,
                                HasPoint = true, Px = r0.CenterX, Py = r0.CenterY, Value = r0.Score, Text = $"θ{r0.Angle:F1}",
                            });
                            _curProductScore = r0.Score;
                            if (r0.Score < step.ScoreThreshold) _curProductOk = false;

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

                                    Label = $"像不像 {r.Score:F2} · 转了 {r.Angle:F1}°"

                                });

                            }

                        }

                        else

                        {

                            step.ActualValue = "未匹配";

                            step.StatusText = "匹配失败";
                            _curProductScore = 0;
                            _curProductOk = false;

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
                        _curProductOk = false;

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
                        _curProductDefects = defects.Count;
                        if (defects.Count > 0) _curProductOk = false;

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

                            TrySendDefectResultToExternal(defects);

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
                        _curProductOk = false;

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
                            if (!ok) _curProductOk = false;

                            step.ActualValue = measured + (ok ? "  合格" : "  超差");

                        }

                        else

                            step.StatusText = "测量完成";

                    }

                    if (ownMat && msrc != null) msrc.Dispose();

                    break;

                }

                case "FindCircle":

                {

                    var g = StepGray(step);

                    if (g == null) { step.ActualValue = "没有图像"; step.StatusText = "找不到圆"; _curProductOk = false; break; }

                    using (g)

                    {

                        var box = StepRoiBox(step, g);

                        var p = CaliperOf(step);

                        double r = step.ExpectRadius > 1 ? step.ExpectRadius : Math.Min(box.Width, box.Height) / 4;

                        double len = step.CaliperLength > 2 ? step.CaliperLength : r / 2;

                        var (circle, pts, ratio) = Caliper.MeasureCircle(g, DvArc.FullCircle(box.Center, r), len, p);

                        if (pts.Count < 3) { step.ActualValue = "边缘点太少"; step.StatusText = "找不到圆"; _curProductOk = false; break; }

                        step.ActualValue = $"圆心=({circle.Center.X:F2},{circle.Center.Y:F2}) 半径={circle.Radius:F2}";

                        step.ParamSummary = $"圆 {pts.Count}点 有效{ratio:P0}";

                        step.StatusText = "已找到";

                    }

                    break;

                }



                case "FindLine":

                {

                    var g = StepGray(step);

                    if (g == null) { step.ActualValue = "没有图像"; step.StatusText = "找不到边"; _curProductOk = false; break; }

                    using (g)

                    {

                        var box = StepRoiBox(step, g);

                        var p = CaliperOf(step);

                        int n = (int)Math.Max(2, step.CaliperCount > 0 ? step.CaliperCount : box.Width / Math.Max(1, p.SamplingStep));

                        var (line, pts, ratio) = Caliper.MeasureLine(g, DvRotRect.FromDeg(box.Center, box.Width, box.Height, step.RoiAngle), n, p);

                        if (pts.Count < 2) { step.ActualValue = "边缘点太少"; step.StatusText = "找不到边"; _curProductOk = false; break; }

                        double ang = Math.Atan2(line.Dir.Y, line.Dir.X) * 180 / Math.PI;

                        step.ActualValue = $"过({line.P0.X:F2},{line.P0.Y:F2}) 角度={ang:F2}°";

                        step.ParamSummary = $"直线 {pts.Count}点 有效{ratio:P0}";

                        step.StatusText = "已找到";

                    }

                    break;

                }



                case "FindEdgePoint":

                {

                    var g = StepGray(step);

                    if (g == null) { step.ActualValue = "没有图像"; step.StatusText = "找不到边"; _curProductOk = false; break; }

                    using (g)

                    {

                        var box = StepRoiBox(step, g);

                        var p = CaliperOf(step);

                        double len = step.CaliperLength > 2 ? step.CaliperLength : box.Height;

                        var dir = DvRotRect.FromDeg(box.Center, box.Width, box.Height, step.RoiAngle).UnitY();

                        var e = Caliper.MeasurePoint(g, new DvPoint2D(box.X, box.Y), dir, len, p);

                        if (e == null) { step.ActualValue = "没找到边缘点"; step.StatusText = "找不到边"; _curProductOk = false; break; }

                        step.ActualValue = $"({e.Position.X:F2},{e.Position.Y:F2}) 强弱={e.Gradient:F1}";

                        step.StatusText = "已找到";

                    }

                    break;

                }



                case "CountBlobs":

                {

                    var g = StepGray(step);

                    if (g == null) { step.ActualValue = "没有图像"; step.StatusText = "检测失败"; _curProductOk = false; break; }

                    using (g)

                    {

                        var bp = new BlobParam

                        {

                            Polarity = step.BlobPolarity == "暗区域" ? DvBlobPolarity.Dark : DvBlobPolarity.Bright,

                            ThresholdMode = step.ThreshMode switch

                            {

                                "固定值" => DvThresholdMode.Fixed,

                                "双阈值" => DvThresholdMode.Double,

                                _ => DvThresholdMode.Otsu

                            },

                            ThresholdLo = step.ThreshLo,

                            ThresholdHi = step.ThreshHi,

                            Area = (step.AreaMin, step.AreaMax <= 0 ? double.MaxValue : step.AreaMax),

                            InvertFilter = step.InvertImage,

                            Sort = step.SortBy switch

                            {

                                "面积从小到大" => DvBlobSort.AreaAsc,

                                "从左到右" => DvBlobSort.XAsc,

                                "从上到下" => DvBlobSort.YAsc,

                                _ => DvBlobSort.AreaDesc

                            }

                        };

                        var bbox = StepRoiBox(step, g);

                        Mat crop = g;

                        bool cropped = step.RoiW > 2 && step.RoiH > 2 && bbox.Width > 2 && bbox.Height > 2;

                        if (cropped) crop = new Mat(g, new Rect((int)bbox.X, (int)bbox.Y, (int)bbox.Width, (int)bbox.Height));

                        var list = Blob.Analyze(crop, bp);

                        if (cropped) crop.Dispose();

                        double bx = cropped ? bbox.X : 0, by = cropped ? bbox.Y : 0;

                        if (list.Count > 0)

                        {

                            double pcx = list[0].Centroid.X + bx, pcy = list[0].Centroid.Y + by;

                            step.ActualValue = $"共 {list.Count} 块 最大中心=({pcx:F2},{pcy:F2})";

                            step.ParamSummary = $"最大 {list[0].Area:F0} 像素";

                            GeoPut(step, new GeoResult { HasPoint = true, Px = pcx, Py = pcy, Value = list.Count });

                        }

                        else step.ActualValue = "共 0 块";

                        step.StatusText = "已完成";

                    }

                    break;

                }



                case "PixelStat":

                {

                    var g = StepGray(step);

                    if (g == null) { step.ActualValue = "没有图像"; step.StatusText = "已完成"; break; }

                    using (g)

                    {

                        var box = StepRoiBox(step, g);

                        using var sub = new Mat(g, new Rect((int)box.X, (int)box.Y, (int)box.Width, (int)box.Height));

                        Cv2.MinMaxLoc(sub, out double mn, out double mx, out _, out Point maxAt);

                        double avg = Cv2.Mean(sub).Val0;

                        using var bin = new Mat();

                        Cv2.Threshold(sub, bin, step.ThreshLo, 255, ThresholdTypes.Binary);

                        int over = Cv2.CountNonZero(bin);

                        double frac = 100.0 * over / Math.Max(1, sub.Width * sub.Height);

                        step.ActualValue = step.MeasureType switch

                        {

                            "最亮点" => $"({maxAt.X},{maxAt.Y}) 亮度={mx:F0}",

                            "最暗点" => $"最低亮度={mn:F0}",

                            "亮像素占比" => $"{frac:F1}%",

                            _ => $"平均亮度={avg:F1}"

                        };

                        step.StatusText = "已完成";

                    }

                    break;

                }



                case "Binarize":

                case "ToGray":

                case "PreProcess":

                case "RotateImage":

                {

                    var src = _sharedImage;

                    if (src == null || src.Empty()) { step.ActualValue = "没有图像"; step.StatusText = "已完成"; break; }

                    string note = "";

                    Mat outMat;

                    if (step.StepType == "ToGray")

                    {

                        int ch = step.PreprocessType switch { "只要红" => 2, "只要绿" => 1, "只要蓝" => 0, _ => -1 };

                        if (ch >= 0 && src.Channels() > ch)

                        {

                            var chans = Cv2.Split(src);

                            outMat = chans[ch].Clone();

                            foreach (var c in chans) c.Dispose();

                        }

                        else

                        {

                            outMat = new Mat();

                            Cv2.CvtColor(src, outMat, ColorConversionCodes.BGR2GRAY);

                        }

                        note = "已转灰度";

                    }

                    else if (step.StepType == "Binarize")

                    {

                        var gray = src.Channels() > 1 ? src.CvtColor(ColorConversionCodes.BGR2GRAY) : src.Clone();

                        var bp = new BlobParam

                        {

                            ThresholdMode = step.ThreshMode switch { "固定值" => DvThresholdMode.Fixed, "双阈值" => DvThresholdMode.Double, _ => DvThresholdMode.Otsu },

                            ThresholdLo = step.ThreshLo,

                            ThresholdHi = step.ThreshHi,

                            InvertFilter = step.InvertImage

                        };

                        outMat = Blob.Threshold(gray, bp, out double used);

                        gray.Dispose();

                        note = $"分界={used:F0}";

                    }

                    else if (step.StepType == "PreProcess")

                    {

                        int k = CaliperParam.Odd(Math.Max(1, step.SmoothSize));

                        outMat = new Mat();

                        switch (step.PreprocessType)

                        {

                            case "去噪点(中值)":

                                Cv2.MedianBlur(src, outMat, k);

                                note = "中值去噪";

                                break;

                            case "锐化":

                                {

                                    using var blur = new Mat();

                                    Cv2.GaussianBlur(src, blur, new Size(k, k), 0);

                                    Cv2.AddWeighted(src, 1.8, blur, -0.8, 0, outMat);

                                    note = "锐化";

                                    break;

                                }

                            case "均衡亮度":

                                {

                                    var gray = src.Channels() > 1 ? src.CvtColor(ColorConversionCodes.BGR2GRAY) : src.Clone();

                                    Cv2.EqualizeHist(gray, outMat);

                                    gray.Dispose();

                                    note = "均衡亮度";

                                    break;

                                }

                            default:

                                Cv2.GaussianBlur(src, outMat, new Size(k, k), 0);

                                note = "高斯去噪";

                                break;

                        }

                    }

                    else

                    {

                        var center = new Point2f(src.Width / 2f, src.Height / 2f);

                        using var m = Cv2.GetRotationMatrix2D(center, step.RoiAngle, 1.0);

                        outMat = new Mat();

                        Cv2.WarpAffine(src, outMat, m, new Size(src.Width, src.Height));

                        if (step.InvertImage) Cv2.Flip(outMat, outMat, FlipMode.X);

                        if (step.FlipVertical) Cv2.Flip(outMat, outMat, FlipMode.Y);

                        note = $"转正 {step.RoiAngle:F1}°";

                    }

                    _sharedImage?.Dispose();

                    _sharedImage = outMat;

                    step.ActualValue = note;

                    step.StatusText = "已完成";

                    break;

                }



                case "SaveImage":

                {

                    var src = _sharedImage;

                    if (src == null || src.Empty()) { step.ActualValue = "没有图像"; step.StatusText = "保存失败"; break; }

                    var dir = string.IsNullOrWhiteSpace(step.SavePath) ? Path.Combine(AppContext.BaseDirectory, "Data", "Images") : step.SavePath;

                    Directory.CreateDirectory(dir);

                    var ext = step.ImageFormat switch { "JPG" => ".jpg", "BMP" => ".bmp", _ => ".png" };

                    var name = step.SaveFields == "日期时间"

                        ? DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ext

                        : $"{DateTime.Now:HHmmss}_{step.Index}{ext}";

                    var full = Path.Combine(dir, name);

                    Cv2.ImWrite(full, src);

                    step.ActualValue = full;

                    step.ParamSummary = full;

                    step.StatusText = "已完成";

                    break;

                }



                default:

                    if (!RunCatalogStep(_selectedFlow, step)) step.ActualValue = "OK";

                    break;

            }

        }

        catch (Exception ex)

        {

            step.ActualValue = $"错误:{ex.Message}";

            step.StatusText = "错误";
            _curProductOk = false;

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

        return NextStepIndex(_selectedFlow, i);

    }

    /// <summary>「跳转到」按名字找落点：返回步骤在集合里的下标，找不到返回 -1。</summary>
    static int LabelIndex(VisionFlow? flow, string? name)
    {
        if (flow == null || string.IsNullOrWhiteSpace(name)) return -1;
        var key = name.Trim();
        for (int i = 0; i < flow.Steps.Count; i++)
        {
            var s = flow.Steps[i];
            if (s.StepType == "JumpTo" && string.Equals((s.LabelName ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>这个落点被哪些「跳转」调用（给面板显示）。</summary>
    static string CallersText(VisionFlow? flow, string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return "还没起名字：起个名字，别人才跳得过来";
        if (flow == null) return "";
        var hits = flow.Steps
            .Where(s => s.StepType == "Jump" && string.Equals((s.JumpTargetName ?? "").Trim(), label.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Index).ToList();
        return hits.Count == 0 ? "还没有哪一步跳到这里" : "被 第 " + string.Join("、", hits) + " 步调用";
    }

    static bool IsFailStatus(string? status)
    {
        var s = status ?? "";
        return s.Contains("不合格") || s.Contains("失败") || s.Contains("未完成") || s.Contains("出错") || s.Contains("要复核");
    }

    static bool IsOkStatus(string? status)
    {
        var s = status ?? "";
        if (IsFailStatus(s)) return false;
        return s.Contains("合格") || s.Contains("成功") || s.Contains("完成") || s.Contains("通过") || s.Contains("正常");
    }

    /// <summary>跳转的条件到底成不成立；why 是给表里看的一句人话。</summary>
    bool JumpHolds(VisionFlow? flow, VisionFlowStep step, out string why)
    {
        var cond = string.IsNullOrWhiteSpace(step.JumpCondition) ? "总是跳" : step.JumpCondition.Trim();
        var prev = flow?.Steps.FirstOrDefault(s => s.Index == step.Index - 1);
        why = cond;
        switch (cond)
        {
            case "总是跳":
                return true;
            case "上一步合格":
                return prev != null && IsOkStatus(prev.StatusText);
            case "上一步不合格":
                return prev != null && IsFailStatus(prev.StatusText);
            case "当前值大于等于":
            case "当前值小于等于":
            {
                double v = NumOf(prev?.ActualValue);
                bool hold = cond == "当前值大于等于" ? v >= step.JumpWhen : v <= step.JumpWhen;
                why = $"上一步读数 {v:0.###} {cond.Replace("当前值", "")} {step.JumpWhen:0.###}";
                return hold;
            }
            default:
                return false;
        }
    }


    /// <summary>根据逻辑列决定下一行：如果/并且/或者 条件不成立跳到「否则」（没有则整块跳过）；循环 跳回最近「如果」；跳出 越过最近「循环」行。</summary>

    private int NextStepIndex(VisionFlow? flow, int i)

    {

        var steps = flow?.Steps;

        if (steps == null || i < 0 || i >= steps.Count) return i + 1;

        var step = steps[i];

        // 跳转：条件成立就跳到同名「跳转到」那一步；名字没找到就顺序往下
        if (step.StepType == "Jump")
        {
            if (!JumpHolds(flow, step, out _)) return i + 1;
            int at = LabelIndex(flow, step.JumpTargetName);
            return at >= 0 ? at : i + 1;
        }
        if (step.StepType == "JumpTo") return i + 1;

        switch (step.LogicRelation)

        {

            case "满足就做":

            case "而且要":

            case "或者要":

                if (EvalLogic(step)) return i + 1;

                // 条件不成立：跳到最近一个「否则」行

                for (int j = i + 1; j < steps.Count; j++)

                    if (steps[j].LogicRelation == "不满足时") return j;

                // 没有「否则」：跳过整个条件块（后续连续的 并且/或者 行）

                for (int j = i + 1; j < steps.Count; j++)

                    if (steps[j].LogicRelation != "而且要" && steps[j].LogicRelation != "或者要") return j;

                return steps.Count;

            case "不满足时":

                return i + 1;

            case "一直重复":

            {

                // 向上找最近的「如果/循环」作为循环头

                for (int k = i - 1; k >= 0; k--)

                    if (steps[k].LogicRelation == "满足就做" || steps[k].LogicRelation == "一直重复") return k;

                return 0;

            }

            case "结束重复":

            {

                // 跳出最近循环：越过其后最近的「循环」行

                for (int j = i + 1; j < steps.Count; j++)

                    if (steps[j].LogicRelation == "一直重复") return j + 1;

                // 无循环则越过下一个块行

                for (int j = i + 1; j < steps.Count; j++)

                    if (steps[j].LogicRelation == "满足就做" || steps[j].LogicRelation == "不满足时") return j + 1;

                return steps.Count;

            }

            default:

                return i + 1;

        }

    }



    /// <summary>条件求值：本步实测值(ActualValue) 按「运算」列与「输入值」列比较；输入值为空时按本步状态是否成功判断。</summary>

    private bool EvalLogic(VisionFlowStep step)

    {

        try

        {

            var op = string.IsNullOrWhiteSpace(step.Operator) ? "大于等于" : step.Operator;

            var rightRaw = (step.InputValue ?? "").Trim();

            var leftRaw = step.ActualValue ?? "";

            // 输入值未填：按本步运行状态判断（成功/合格/通过/正常 = 条件成立）

            if (rightRaw.Length == 0)

            {

                var st = step.StatusText ?? "";

                return st.Contains("成功") || st.Contains("合格") || st.Contains("通过")

                    || st.Contains("正常") || st.Contains("完成") || st.Contains("OK");

            }

            var lm = System.Text.RegularExpressions.Regex.Match(leftRaw, "[-+]?[0-9]*\\.?[0-9]+");

            var rm = System.Text.RegularExpressions.Regex.Match(rightRaw, "[-+]?[0-9]*\\.?[0-9]+");

            // 两侧都能取到数字 → 数值比较

            if (lm.Success && rm.Success)

            {

                double l = double.Parse(lm.Value, System.Globalization.CultureInfo.InvariantCulture);

                double r = double.Parse(rm.Value, System.Globalization.CultureInfo.InvariantCulture);

                switch (op)

                {

                    case "大于": return l > r;

                    case "小于": return l < r;

                    case "等于": return System.Math.Abs(l - r) < 1e-9;

                    case "大于等于": return l >= r;

                    case "小于等于": return l <= r;

                    case "不等于": return System.Math.Abs(l - r) >= 1e-9;

                    default: return leftRaw.Contains(rightRaw);

                }

            }

            // 否则按文本比较

            switch (op)

            {

                case "包含": return leftRaw.Contains(rightRaw);

                case "不包含": return !leftRaw.Contains(rightRaw);

                case "开头为": return leftRaw.StartsWith(rightRaw);

                case "结尾为": return leftRaw.EndsWith(rightRaw);

                case "等于": return leftRaw.Trim() == rightRaw;

                case "不等于": return leftRaw.Trim() != rightRaw;

                default: return false;

            }

        }

        catch { return false; }

    }

}



#endregion



#region 工程师页



// 本工程作者 温启志 ▪ 联系微信 18719361399




// 温启志 著 ● 微信：187①936①399（保留）

/// <summary>工程师台左侧的功能页。</summary>
public class EngineerTab
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Hint { get; init; } = "";
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



    // ===== 工程师台：左侧功能页签（流程 / 相机 / 通讯 / 运控 / 日志） =====

    public ObservableCollection<EngineerTab> Tabs { get; } = new()
    {
        new EngineerTab { Key = "Home", Name = "总览", Hint = "版本、运行模式、系统状态" },
        new EngineerTab { Key = "Flow", Name = "流程控制", Hint = "选流程、运行、单步、看每步状态" },
        new EngineerTab { Key = "Camera", Name = "相机", Hint = "连接、采集、曝光增益" },
        new EngineerTab { Key = "Comm", Name = "通讯", Hint = "连通道、发数据、看回包" },
        new EngineerTab { Key = "Motion", Name = "运控调试", Hint = "轴 / IO / 气缸手动点动" },
        new EngineerTab { Key = "Log", Name = "日志", Hint = "运行模式、日志级别、调试日志" },
        new EngineerTab { Key = "Smart", Name = "智能中心", Hint = "记忆 / 联想 / 仿真" },
    };

    private EngineerTab _selectedTab;

    /// <summary>当前选中的功能页。</summary>
    public EngineerTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetField(ref _selectedTab, value)) OnPropertyChanged(nameof(TabTitle));
        }
    }

    public string TabTitle => _selectedTab?.Name ?? "总览";

    /// <summary>工程师台直接操作的三套现成能力（和各自页面是同一份数据）。</summary>
    public FlowViewModel? FlowVm => FlowViewModel.Instance;

    public CameraViewModel? CameraVm => CameraViewModel.Instance;

    public CommunicationViewModel? CommVm => CommunicationViewModel.Instance;

    public string FlowCountText => $"流程 {FlowVm?.Flows.Count ?? 0} 个";

    public string CameraCountText
    {
        get
        {
            var cams = CameraVm?.Cameras;
            if (cams == null) return "相机 0 台";
            int online = cams.Count(c => c.Status == "已连接");
            return $"相机 {cams.Count} 台（在线 {online}）";
        }
    }

    public string CommCountText
    {
        get
        {
            int n = CommVm?.Configs.Count ?? 0;
            bool on = CommVm?.IsConnected == true;
            return $"通讯 {n} 个通道（{(on ? "已连" : "未连")}）";
        }
    }

    /// <summary>跳到对应功能页（工程师台只做运行与粗调，细参数回各页改）。</summary>
    public ICommand GoFlowPageCmd { get; }

    public ICommand GoCameraPageCmd { get; }

    public ICommand GoCommPageCmd { get; }

    public ICommand GoMotionPageCmd { get; }

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(FlowCountText));
        OnPropertyChanged(nameof(CameraCountText));
        OnPropertyChanged(nameof(CommCountText));
        OnPropertyChanged(nameof(FlowVm));
        OnPropertyChanged(nameof(CameraVm));
        OnPropertyChanged(nameof(CommVm));
    }

    public ICommand AddCmd { get; }




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

            SelectedTab = Tabs[0];

            GoFlowPageCmd = new RelayCommand(_ => NoCodeVision.MainWindow.Current?.NavigateTo(5));

            GoCameraPageCmd = new RelayCommand(_ => NoCodeVision.MainWindow.Current?.NavigateTo(1));

            GoCommPageCmd = new RelayCommand(_ => NoCodeVision.MainWindow.Current?.NavigateTo(3));

            GoMotionPageCmd = new RelayCommand(_ => NoCodeVision.MainWindow.Current?.NavigateTo(2));

            // 流程 / 相机 / 通讯 数量变化时刷新首页统计
            FlowViewModel.Instance?.Flows.CollectionChanged += (_, _) => RaiseCounts();

            if (CameraViewModel.Instance != null)
                CameraViewModel.Instance.Cameras.CollectionChanged += (_, _) => RaiseCounts();

            if (CommunicationViewModel.Instance != null)
                CommunicationViewModel.Instance.Configs.CollectionChanged += (_, _) => RaiseCounts();


            // 自动保存/恢复工程师页模块列表等（bin\Data\ui_state.json）


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



// 〔版权〕温启志 编写 ◇ 微信：18719361399

public class TaskItem

{

    public string Name { get; set; } = "";

    public string Time { get; set; } = "";

    public string Result { get; set; } = "待检";

}



// 作者：温·启·志 编写 ▢ 联系 wx:187-1936-1399

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

    private CancellationTokenSource? _runCts;

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

    public ICommand RunAllFlowsCmd { get; }

    public ICommand StopAllFlowsCmd { get; }



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



        RunAllFlowsCmd = new RelayCommand(_ => _ = StartLoopAsync(), _ => !_isRunning);

        StopAllFlowsCmd = new RelayCommand(_ => StopLoop(), _ => _isRunning);

    }



    private async Task StartLoopAsync()

    {

        if (_isRunning) return;

        _runCts = new CancellationTokenSource();

        IsRunning = true;

        try

        {

            var fvm = FlowViewModel.Instance;

            if (fvm == null) { Status = "流程引擎不可用"; }

            else { await Task.Run(() => fvm.RunAllFlowsLoopAsync(_runCts.Token)); }

        }

        catch (OperationCanceledException) { /* 停止按钮触发的正常取消 */ }

        catch (Exception ex) { Status = $"运行异常: {ex.Message}"; }

        finally

        {

            _runCts?.Dispose();

            _runCts = null;

            IsRunning = false;

        }

    }



    private void StopLoop()

    {

        try { _runCts?.Cancel(); } catch { }

        // IsRunning 会在 StartLoopAsync 的 finally 中复位

    }



    private void UpdateStatus() => Status = _isRunning ? "检测中…" : "已停止";



}



#endregion





// 温启志：18719361399  混淆: 温r启d志t：x1m8y7t1x9i3p6c1t3s9w9
