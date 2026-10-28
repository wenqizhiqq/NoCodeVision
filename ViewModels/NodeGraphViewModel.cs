using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace NoCodeVision.ViewModels;

// 编写：温启志 ‖ 微信：187①936①399

/// <summary>节点图流程的编辑模型：把 VisionFlow.Steps / VisionFlow.Links 包成可拖拽的节点与连线。
/// 画布上的每次增删移动都直接写回流程本体，所以步骤表格、自动保存和运行器看到的始终是同一份数据。</summary>

public class NodeGraphViewModel : ViewModelBase

{

    public ObservableCollection<NodeGraphNode> Nodes { get; } = new();

    public ObservableCollection<NodeGraphLink> Links { get; } = new();

    public ObservableCollection<NodeGraphNode> SelectedNodes { get; } = new();



    public NodeGraphNode? SelectedNode { get => _selectedNode; set => SetField(ref _selectedNode, value); }

    private NodeGraphNode? _selectedNode;



    public NodeGraphPending PendingConnection { get; }



    /// <summary>分类 → 可拖拽算子，覆盖工具箱全部 22 个算子。</summary>
    public List<NodeGraphCategory> Palette { get; } = new();



    /// <summary>宿主流程页 VM：点节点时把 SelectedStep 指过去，右侧属性页随之切换。</summary>
    public FlowViewModel Owner { get; }



    private VisionFlow? _flow;

    private bool _syncing;



    public NodeGraphViewModel(FlowViewModel owner)

    {

        Owner = owner;

        PendingConnection = new NodeGraphPending(this);

        BuildPalette();



        DeleteNodeCmd = new RelayCommand(o =>

        {

            if (o is NodeGraphNode n) DeleteNode(n);

        });



        CreateLinkCmd = new RelayCommand(o =>

        {

            var target = o as NodeGraphPort ?? (o as NodeGraphNode)?.InputPort;

            var source = PendingConnection.Source;

            if (target == null || source == null) return;

            TryConnect(source, target);

        }, _ => PendingConnection.Source != null);



        DisconnectCmd = new RelayCommand(o =>

        {

            if (o is not NodeGraphPort port) return;

            foreach (var l in port.Links.ToList()) RemoveLink(l);

        });



        SelectedNodes.CollectionChanged += (_, _) =>

        {

            // 事件派发期间不能改集合（双向绑定会回写 SelectedNodes），推到本帧结束后再同步属性面板

            if (_syncing) return;

            Application.Current?.Dispatcher.BeginInvoke(new Action(SyncSelectionToPropertyPanel));

        };

    }



    private void SyncSelectionToPropertyPanel()

    {

        SelectedNode = SelectedNodes.FirstOrDefault();

        Owner.SelectedStep = SelectedNode?.Step;

    }



    static string GroupTitle(string g) => g switch

    {

        "Locate" => "找位置",

        "Measure" => "数与量",

        "Color" => "先修图",
        "OpenCVVision" => "OpenCVVision",

        "Calib" => "标定换算",

        "Save" => "留证据",

        "Calc" => "算几何",

        "Code" => "读码识字",

        "Logic" => "判断和重复",

        "Motion" => "让设备动",

        "Output" => "发出去",

        "Comm" => "收发数据",

        _ => "看图"

    };



    private void BuildPalette()

    {

        var cats = new Dictionary<string, NodeGraphCategory>();



        void Cat(string key, string title)

        {

            if (!cats.ContainsKey(key)) cats[key] = new NodeGraphCategory(title, key);

        }



        void Add(string key, string name)

        {

            var (icon, type) = FlowViewModel.ToolToIconType(name);

            cats[key].Tools.Add(new NodeGraphTool(name, icon, type, key));

        }



        Cat("Vision", "先看图");

        foreach (var n in new[] { "拍张照片", "图里找图案", "挑瑕疵", "量尺寸", "看颜色", "数有几个", "扫条码", "认文字" }) Add("Vision", n);



        Cat("Logic", "再判断");

        foreach (var n in new[] { "判一下对不对", "走哪一条", "重复做", "算个数", "等一会", "调子流程", "写段代码" }) Add("Logic", n);



        Cat("Motion", "让设备动");

        foreach (var n in new[] { "动轴", "开关电", "伸缩气缸" }) Add("Motion", n);



        Cat("Output", "发出去");

        foreach (var n in new[] { "报结果", "存文件", "弹提示", "发数据" }) Add("Output", n);



        // 目录里的算子并进同名分组，不重复出现

        foreach (var d in OperatorCatalog.All)

        {

            Cat(d.Group, GroupTitle(d.Group));

            if (cats[d.Group].Tools.Any(x => x.Name == d.Name)) continue;

            cats[d.Group].Tools.Add(new NodeGraphTool(d.Name, d.Icon, d.StepType, d.Group));

        }



        foreach (var key in new[] { "Vision", "Locate", "Measure", "Calc", "Calib", "Color", "OpenCVVision", "Code", "Logic", "Motion", "Output", "Comm", "Save" })

            if (cats.TryGetValue(key, out var c) && c.Tools.Count > 0) Palette.Add(c);

    }



    private void AddCategory(string name, string groupKey, params string[] tools)

    {

        var cat = new NodeGraphCategory(name, groupKey);

        foreach (var t in tools)

        {

            var (icon, type) = FlowViewModel.ToolToIconType(t);

            cat.Tools.Add(new NodeGraphTool(t, icon, type, groupKey));

        }

        Palette.Add(cat);

    }



    /// <summary>切换流程时重建画布；同一个流程且节点数没变则不动，避免把用户正在拖的节点刷掉。</summary>

    public void Bind(VisionFlow? flow)

    {

        if (ReferenceEquals(_flow, flow) && flow != null && flow.Steps.Count == Nodes.Count) return;

        _flow = flow;

        Nodes.Clear();

        Links.Clear();

        SelectedNodes.Clear();

        if (flow == null) return;

        foreach (var s in flow.Steps) Nodes.Add(new NodeGraphNode(this, s));

        foreach (var l in flow.Links) AddLinkFromModel(l);

        StackUnplacedNodes();

    }



    /// <summary>从「逻辑」列表带过来的步骤没有画布坐标，全挤在原点上会叠成一坨：
    /// 这里按步骤顺序从上到下竖着排一遍；用户手动拖过的节点（有坐标）保持不动。</summary>

    private void StackUnplacedNodes()

    {

        const double pitch = 140;

        double y = 0;

        foreach (var n in Nodes)

        {

            if (n.Step.NodeX != 0 || n.Step.NodeY != 0) y = Math.Max(y, n.Step.NodeY + pitch);

        }

        foreach (var n in Nodes)

        {

            if (n.Step.NodeX != 0 || n.Step.NodeY != 0) continue;

            n.Step.NodeY = y;

            y += pitch;

        }

    }




    private NodeGraphNode? NodeOf(string id) => Nodes.FirstOrDefault(n => n.Step.NodeId == id);



    private void AddLinkFromModel(FlowNodeLink model)

    {

        var from = NodeOf(model.FromId);

        var to = NodeOf(model.ToId);

        if (from == null || to == null || ReferenceEquals(from, to)) return;

        // 手写/旧配置里的出口名可能写成「出」，而默认出口的 Name 是空串，这里兜一层
        var port = from.Outputs.FirstOrDefault(o => o.Name == model.FromPort)
                   ?? from.Outputs.FirstOrDefault(o => string.IsNullOrEmpty(model.FromPort))
                   ?? from.Outputs.FirstOrDefault();

        if (port == null) return;

        var link = new NodeGraphLink(this, from, port, to);

        port.Links.Add(link);

        to.InputPort.Links.Add(link);

        Links.Add(link);

    }



    /// <summary>视图层拖拽落点调用：在画布坐标处新建一个算子节点。</summary>

    public void AddNodeAt(string toolName, Point at)

    {

        var tool = Palette.SelectMany(c => c.Tools).FirstOrDefault(t => t.Name == toolName);

        if (tool == null || _flow == null) return;

        var step = new VisionFlowStep

        {

            Index = _flow.Steps.Count + 1,

            Function = tool.Name,

            Name = tool.Name,

            Icon = tool.Icon,

            StepType = tool.StepType,

            ParamSummary = "-",

            NodeX = at.X,

            NodeY = at.Y

        };

        _flow.Steps.Add(step);

        var node = new NodeGraphNode(this, step);

        Nodes.Add(node);

        _syncing = true;

        try

        {

            SelectedNodes.Clear();

            SelectedNodes.Add(node);

        }

        finally { _syncing = false; }

        SelectedNode = node;

        Owner.SelectedStep = node.Step;

    }



    public void DeleteNode(NodeGraphNode? node)

    {

        if (node == null || _flow == null) return;

        foreach (var l in node.InputPort.Links.ToList()) RemoveLink(l);

        foreach (var o in node.Outputs) foreach (var l in o.Links.ToList()) RemoveLink(l);

        Nodes.Remove(node);

        _flow.Steps.Remove(node.Step);

        for (int i = 0; i < _flow.Steps.Count; i++) _flow.Steps[i].Index = i + 1;

    }



    /// <summary>连一条线：同一对端口不重复连，禁止自连。</summary>

    public bool TryConnect(NodeGraphPort from, NodeGraphPort to)

    {

        if (_flow == null || ReferenceEquals(from.Owner, to.Owner)) return false;

        if (!from.Owner.Outputs.Contains(from)) return false;

        if (to.Owner.InputPort.Links.Any(l => ReferenceEquals(l.FromPort, from))) return false;

        var model = new FlowNodeLink { FromId = from.Owner.Step.NodeId, FromPort = from.Name, ToId = to.Owner.Step.NodeId };

        _flow.Links.Add(model);

        var link = new NodeGraphLink(this, from.Owner, from, to.Owner);

        from.Links.Add(link);

        to.Owner.InputPort.Links.Add(link);

        Links.Add(link);

        return true;

    }



    public void RemoveLink(NodeGraphLink link)

    {

        if (_flow == null) return;

        var model = _flow.Links.FirstOrDefault(l =>

            l.FromId == link.From.Step.NodeId && l.FromPort == link.FromPort.Name && l.ToId == link.To.Step.NodeId);

        if (model != null) _flow.Links.Remove(model);

        link.FromPort.Links.Remove(link);

        link.To.InputPort.Links.Remove(link);

        Links.Remove(link);

    }



    public ICommand DeleteNodeCmd { get; }

    public ICommand CreateLinkCmd { get; }

    public ICommand DisconnectCmd { get; }

}



public class NodeGraphCategory

{

    public string Name { get; }

    public string GroupKey { get; }

    public List<NodeGraphTool> Tools { get; } = new();

    public NodeGraphCategory(string name, string groupKey)

    {

        Name = name; GroupKey = groupKey;

    }

}



public class NodeGraphTool

{

    public string Name { get; }

    public string Icon { get; }

    public string StepType { get; }

    public string GroupKey { get; }

    public NodeGraphTool(string name, string icon, string stepType, string groupKey)

    {

        Name = name; Icon = icon; StepType = stepType; GroupKey = groupKey;

    }

}



public class NodeGraphNode : ViewModelBase

{

    private readonly NodeGraphViewModel _graph;



    public VisionFlowStep Step { get; }

    /// <summary>执行入口端口（每个节点一个）。</summary>
    public List<NodeGraphPort> Input { get; }

    public NodeGraphPort InputPort => Input[0];

    public List<NodeGraphPort> Outputs { get; }



    public NodeGraphNode(NodeGraphViewModel graph, VisionFlowStep step)

    {

        _graph = graph;

        Step = step;

        Input = new List<NodeGraphPort> { new NodeGraphPort(this, "", "入", true) };

        Outputs = IsCondition

            ? new List<NodeGraphPort> { new(this, "真", "真", false), new(this, "假", "假", false) }

            : new List<NodeGraphPort> { new(this, "", "出", false) };

        DeleteCommand = new RelayCommand(_ => _graph.DeleteNode(this));

        step.PropertyChanged += (_, e) =>

        {

            if (e.PropertyName is nameof(VisionFlowStep.Function) or nameof(VisionFlowStep.Name) or nameof(VisionFlowStep.Icon)

                or nameof(VisionFlowStep.StepType)

                or nameof(VisionFlowStep.StatusText) or nameof(VisionFlowStep.ActualValue) or nameof(VisionFlowStep.Index))

            {

                OnPropertyChanged(nameof(Title));

                OnPropertyChanged(nameof(Subtitle));

                OnPropertyChanged(nameof(Glyph));

                OnPropertyChanged(nameof(GroupKey));

            }

        };

    }



    /// <summary>判断/分支/循环类算子给出「真 / 假」两个出口，运行时按条件结果选边；其余算子单出口顺序执行。</summary>

    public bool IsCondition => Step.StepType is "Branch" or "Logic" or "Loop";



    public string Title => $"{Step.Index}．{Step.Function}";



    /// <summary>分类配色键：视觉=蓝、逻辑=橙、运动=绿、输出=紫，画布上一眼能分辨节点种类。</summary>

    public string GroupKey => OperatorCatalog.Find(Step.StepType)?.Group ?? Step.StepType switch

    {

        "ImageCapture" or "TemplateMatch" or "Defect" or "Measure" or "Color" or "Count" or "Code" or "Ocr" => "Vision",

        "Branch" or "Logic" or "Loop" or "Calc" or "Wait" or "SubFlow" or "Lua" => "Logic",

        "AxisMove" or "IOControl" or "Cylinder" => "Motion",

        "Output" or "Save" or "Notify" or "Comm" => "Output",

        _ => "Other"

    };



    public string Glyph => Step.Icon;



    public string Subtitle => Step.StatusText is "" or "未开始" ? Step.Name : $"{Step.Name} · {Step.StatusText}";



    public Point Location

    {

        get => new(Step.NodeX, Step.NodeY);

        set { Step.NodeX = value.X; Step.NodeY = value.Y; }

    }



    public ICommand DeleteCommand { get; }



}



public class NodeGraphPort : ViewModelBase

{

    public NodeGraphNode Owner { get; }

    public string Name { get; }

    public string Title { get; }

    public bool IsInput { get; }

    public ObservableCollection<NodeGraphLink> Links { get; } = new();



    public NodeGraphPort(NodeGraphNode owner, string name, string title, bool isInput)

    {

        Owner = owner; Name = name; Title = title; IsInput = isInput;

        Links.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsConnected));

    }



    public bool IsConnected => Links.Count > 0;



    public Point Anchor { get => _anchor; set => SetField(ref _anchor, value); }

    private Point _anchor;

}



public class NodeGraphLink

{

    public NodeGraphLink(NodeGraphViewModel graph, NodeGraphNode from, NodeGraphPort fromPort, NodeGraphNode to)

    {

        From = from; FromPort = fromPort; To = to;

        RemoveCommand = new RelayCommand(_ => graph.RemoveLink(this));

    }



    public ICommand RemoveCommand { get; }



    public NodeGraphNode From { get; }

    public NodeGraphPort FromPort { get; }

    public NodeGraphNode To { get; }

}



/// <summary>正在拉出的临时连线：记住起点端口和鼠标当前命中的目标，松手时据此建链。</summary>

public class NodeGraphPending : ViewModelBase

{

    public NodeGraphPending(NodeGraphViewModel graph) => Graph = graph;



    public NodeGraphViewModel Graph { get; }



    public NodeGraphPort? Source { get => _source; set => SetField(ref _source, value); }

    private NodeGraphPort? _source;



    public object? PreviewTarget { get => _previewTarget; set => SetField(ref _previewTarget, value); }

    private object? _previewTarget;



    public string? PreviewText { get => _previewText; set => SetField(ref _previewText, value); }

    private string? _previewText;

}
