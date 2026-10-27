// 算子元数据目录：新增算子只在这里加一行，算子库、节点配色、右侧参数面板都从这里取。
// 实现：温启志

using System.Collections.Generic;

namespace NoCodeVision;

/// <summary>参数编辑控件类型。</summary>
public enum OpKind { Number, Text, Bool, Choice }

/// <summary>算子的一个参数：显示名 + 绑到 VisionFlowStep 的哪个属性 + 怎么编辑。</summary>
public sealed class OpParam
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";
    public OpKind Kind { get; set; } = OpKind.Number;
    public string Help { get; set; } = "";
    public string[]? Options { get; set; }
}

/// <summary>一个算子的完整定义。</summary>
public sealed class OperatorDef
{
    public string StepType { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "\u2795";
    public string Group { get; set; } = "Vision";
    public string Desc { get; set; } = "";
    public OpParam[] Params { get; set; } = System.Array.Empty<OpParam>();

    /// <summary>要不要在图上框一块区域：视觉类要，逻辑/运动/通讯类不要。</summary>
    public bool UsesRoi => Group is "Locate" or "Measure" or "Calc" or "Calib" or "Code"
        || StepType is "MaskRegionOp";
}

public static class OperatorCatalog
{
    static OpParam N(string l, string p, string h = "") => new() { Label = l, Path = p, Kind = OpKind.Number, Help = h };
    static OpParam T(string l, string p, string h = "") => new() { Label = l, Path = p, Kind = OpKind.Text, Help = h };
    static OpParam B(string l, string p, string h = "") => new() { Label = l, Path = p, Kind = OpKind.Bool, Help = h };
    static OpParam C(string l, string p, string h, params string[] o) => new() { Label = l, Path = p, Kind = OpKind.Choice, Help = h, Options = o };

    static OperatorDef O(string type, string name, string icon, string group, string desc, params OpParam[] ps)
        => new() { StepType = type, Name = name, Icon = icon, Group = group, Desc = desc, Params = ps };

    static readonly string[] POL = { "从暗到亮", "从亮到暗", "两种都要" };
    static readonly string[] FIT = { "最小二乘", "正交距离(更准)", "带权(按边强弱)" };
    static readonly string[] THMODE = { "自动", "固定值", "双阈值" };
    static readonly string[] ZERO = { "向上(12点)", "向右(3点)", "向下(6点)", "向左(9点)" };
    static readonly string[] SRC = { "上一步结果", "第1步", "第2步", "第3步", "第4步", "第5步", "第6步", "第7步", "第8步", "第9步", "第10步" };

    static string ByStep(string h) => h + "：可以选上一步的结果，也可以直接选第几步的结果";

    public static readonly List<OperatorDef> All = new()
    {

        // ================= 找位置 =================
        O("FindCircle", "找圆", "⭕", "Locate", "在框定的区域里找一个圆，输出圆心和半径，精度可到亚像素。",
            N("半径大概多少", "ExpectRadius", "填 0 表示自动估一个"),
            N("卡尺长度", "CaliperLength", "每个测量点沿半径方向看多远"),
            N("采样步长", "SamplingStep", "沿圆周每隔多少像素测一个点，越小越准也越慢"),
            N("边缘灵敏度", "EdgeConfidence", "0~1，越大只认最清楚的边；光线乱时调小"),
            C("明暗方向", "EdgePolarity", "边是从暗到亮还是从亮到暗；选错就找不到边", POL),
            C("拟合方式", "FitMethod", "有毛刺时选带权那一档", FIT)),

        O("FindArc", "找圆弧", "🌜", "Locate", "只找一段圆弧（零件只露出半个圆时用），其余同找圆。",
            N("半径大概多少", "ExpectRadius", ""),
            N("起始角度(度)", "AngleStart", "圆弧从哪个角度开始"),
            N("跨越角度(度)", "AngleStop", "圆弧一共跨过多大角度"),
            N("卡尺长度", "CaliperLength", ""),
            N("采样步长", "SamplingStep", ""),
            C("明暗方向", "EdgePolarity", "", POL)),

        O("FindLine", "找直线", "📏", "Locate", "在框定的区域里找一条边线，输出位置与角度。",
            N("卡尺长度", "CaliperLength", "每个测量点垂直于直线方向看多远"),
            N("卡尺条数", "CaliperCount", "沿直线摆多少个测量点，越多越稳"),
            N("采样步长", "SamplingStep", ""),
            C("明暗方向", "EdgePolarity", "", POL),
            C("拟合方式", "FitMethod", "", FIT)),

        O("FindEdgePoint", "找边缘点", "📍", "Locate", "在一个带方向的矩形框里找第几条边，用来定基准点。",
            N("第几条边", "EdgeIndex", "1=遇到的第一条边"),
            N("卡尺长度", "CaliperLength", ""),
            C("明暗方向", "EdgePolarity", "", POL)),

        O("CaliperTool", "卡尺测量", "📐", "Locate", "一条完整卡尺：沿一条线逐点找边并拟合，输出每点坐标。",
            N("卡尺长度", "CaliperLength", ""),
            N("卡尺条数", "CaliperCount", ""),
            N("采样步长", "SamplingStep", ""),
            N("最小间距", "MinDistance", "两个边之间至少隔多少像素"),
            C("明暗方向", "EdgePolarity", "", POL)),

        O("MultiLine", "找多条线", "≡", "Locate", "同一区域一次找出多条平行边线（栅格、针脚）。",
            N("要找几条", "TopN", ""),
            N("卡尺长度", "CaliperLength", ""),
            N("最小间距", "MinDistance", ""),
            C("明暗方向", "EdgePolarity", "", POL)),

        O("VertexFind", "找尖角", "🔶", "Locate", "找两条边的交点/尖角，常用于缺料、缺口判断。",
            N("卡尺长度", "CaliperLength", ""),
            N("夹角小于多少度算尖角", "AngleStop", "默认 120"),
            C("明暗方向", "EdgePolarity", "", POL)),

        O("QuadFind", "找四边形", "▱", "Locate", "找一个矩形/梯形轮廓（开口、盖板、贴装位置对不对）。",
            N("卡尺长度", "CaliperLength", ""),
            N("每条边取几点", "CaliperCount", ""),
            C("明暗方向", "EdgePolarity", "", POL)),

        O("Straightness", "查直不直", "〰", "Locate", "看找出来的边有多直，用最大偏差衡量（翘边、弯曲检测）。",
            T("量哪一步的边", "CompareSource", "选上一步或第几步找到的直线"),
            N("允许多大偏差", "Tolerance", "超过这个值判不合格")),

        O("Fixture", "位置补正", "🎯", "Locate", "把后面所有工具的搜索框，跟着前一步找到的位置一起平移旋转。",
            T("跟随哪一步", "CompareSource", "通常是粗定位那一步"),
            B("要不要跟着转角度", "InvertImage", "打开后旋转也跟着补偿"),
            N("整体再偏移X", "TargetX", "在补偿基础上再加的量"),
            N("整体再偏移Y", "TargetY", "")),

        // ================= 数与量 =================
        O("CountBlobs", "数块数", "🧩", "Measure", "把图分成一块一块，数有几个、多大、在哪。适合数产品、查有无。",
            C("数亮的还是暗的", "BlobPolarity", "", new[] { "亮区域", "暗区域" }),
            C("二值化方式", "ThreshMode", "", THMODE),
            N("阈值", "ThreshLo", "0~255，比它亮的算白"),
            N("上限阈值", "ThreshHi", "只有双阈值方式用到"),
            N("最小面积", "AreaMin", "比这更小的碎点不算，用来滤噪点"),
            N("最大面积", "AreaMax", "比这更大的不算"),
            C("怎么排顺序", "SortBy", "", new[] { "面积从大到小", "面积从小到大", "从左到右", "从上到下" }),
            B("反选", "InvertImage", "把过滤条件反过来")),

        O("BlobMask", "按形状筛块", "🧮", "Measure", "先二值化找块，再按圆形度/矩形度筛，只留形状对得上的。",
            C("看哪个形状指标", "MeasureType", "", new[] { "圆形度", "矩形度", "面积" }),
            N("最小值", "AreaMin", ""),
            N("最大值", "AreaMax", ""),
            C("二值化方式", "ThreshMode", "", THMODE),
            N("阈值", "ThreshLo", "")),

        O("PixelStat", "看明暗", "🌗", "Measure", "算一块区域的平均亮度、最亮最暗和明暗分布，适合判断有没有料、脏不脏。",
            C("统计方式", "MeasureType", "", new[] { "平均亮度", "最亮点", "最暗点", "亮像素占比" }),
            N("阈值", "ThreshLo", "算占比时，比这个亮度更亮的才算数")),

        O("HistTool", "直方图", "📊", "Measure", "看这块区域里各档亮度各有多少像素，用来定阈值或比色。",
            B("累计直方图", "InvertImage", "从暗往亮累加"),
            N("要看的档位", "IndexProbe", "取第几档的像素数，0=只看最大最小")),

        O("Pitch", "量间距", "↔", "Measure", "沿一个方向连续找边，输出相邻边的间距（针脚间距、孔距）。",
            N("卡尺长度", "CaliperLength", ""),
            N("要找几条边", "TopN", ""),
            N("最小间距", "MinDistance", ""),
            C("明暗方向", "EdgePolarity", "", POL)),

        O("CircleFit", "圆拟合", "⭕", "Measure", "把已有的若干个点拟成一个圆，看圆心和半径合不合格。",
            T("点从哪来", "CompareSource", "选前面哪一步给的点集"),
            C("拟合方式", "FitMethod", "", FIT)),

        O("LineFit", "直线拟合", "📏", "Measure", "把已有的若干个点拟成一条直线，输出位置和角度。",
            T("点从哪来", "CompareSource", ""),
            C("拟合方式", "FitMethod", "", FIT)),

        // ================= 几何计算 =================
        O("CalcPointPoint", "点点距离", "🔹", "Calc", "两个点之间的直线距离。",
            C("第一个点", "CompareSource", ByStep(""), SRC),
            C("第二个点", "CompareVar", ByStep(""), SRC)),
        O("CalcPointLine", "点线距离", "🔸", "Calc", "一个点到一条直线的垂直距离。",
            C("点从哪来", "CompareSource", ByStep(""), SRC),
            C("线从哪来", "CompareVar", ByStep(""), SRC)),
        O("CalcLineLine", "线线计算", "🔺", "Calc", "两条直线的夹角与交点。",
            C("第一条线", "CompareSource", ByStep(""), SRC),
            C("第二条线", "CompareVar", ByStep(""), SRC)),
        O("CalcLineCircle", "线圆计算", "🔵", "Calc", "直线和圆的关系：圆心到线的距离、有没有相交。",
            C("线从哪来", "CompareSource", ByStep(""), SRC),
            C("圆从哪来", "CompareVar", ByStep(""), SRC)),
        O("CalcCircleCircle", "圆圆计算", "🟣", "Calc", "两个圆的圆心距、半径差。",
            C("第一个圆", "CompareSource", ByStep(""), SRC),
            C("第二个圆", "CompareVar", ByStep(""), SRC)),
        O("CalcPointCircle", "点圆计算", "🟡", "Calc", "一个点到圆周的距离（在圆内还是圆外）。",
            C("点从哪来", "CompareSource", ByStep(""), SRC),
            C("圆从哪来", "CompareVar", ByStep(""), SRC)),
        O("AngleFromCenter", "求角度", "🧭", "Calc", "算一个点相对中心偏了多少度（瓶底黑色线圈朝哪边就用它）。",
            C("中心从哪来", "CompareSource", "选前面「找圆」那一步，圆心就当瓶底中心", SRC),
            C("点从哪来", "CompareVar", "选前面「数块数」那一步，最大那块的中心就是线圈", SRC),
            C("0度朝哪边", "AngleZero", "选「向上」就是时钟12点算0度，现场最好读", ZERO),
            B("顺时针算正数", "InvertImage", "打开=顺时针为正（像钟表）；关掉=逆时针为正"),
            N("基准偏移(度)", "HomeOffset", "读数 = 算出来的角度 + 这个值，用来对齐现场认定的零点"),
            T("结果写入", "OutputVar", "存进变量，后面判定、发PLC都取这个值")),
        O("MidPoint", "求中点", "◽", "Calc", "两个点的中点。",
            C("第一个点", "CompareSource", ByStep(""), SRC),
            C("第二个点", "CompareVar", ByStep(""), SRC)),
        O("OffsetLine", "平行偏移线", "⫽", "Calc", "把一条线沿法向平移一段距离，得到新的边线。",
            C("线从哪来", "CompareSource", ByStep(""), SRC),
            N("偏移多少", "MoveDistance", "正负代表两侧")),
        O("PerpLine", "垂线", "⟂", "Calc", "过某点作已知直线的垂线。",
            C("点从哪来", "CompareSource", ByStep(""), SRC),
            C("线从哪来", "CompareVar", ByStep(""), SRC)),
        O("Bisector", "角平分线", "∠", "Calc", "两条直线的角平分线。",
            C("第一条线", "CompareSource", ByStep(""), SRC),
            C("第二条线", "CompareVar", ByStep(""), SRC)),
        O("RotateCalc", "旋转计算", "↺", "Calc", "把一个点/线/圆按角度绕中心转一遍（坐标系对齐）。",
            C("算哪个结果", "CompareSource", ByStep(""), SRC),
            N("中心X", "TargetX", ""),
            N("中心Y", "TargetY", ""),
            N("旋转角度(度)", "RoiAngle", "")),
        O("MakePoint", "造一个点", "✛", "Calc", "手动给坐标造一个基准点，供后面的测量引用。",
            N("X 坐标", "TargetX", ""),
            N("Y 坐标", "TargetY", "")),
        O("MakeCircle", "造一个圆", "◯", "Calc", "手动给圆心和半径造一个参考圆。",
            N("圆心X", "TargetX", ""), N("圆心Y", "TargetY", ""), N("半径", "ExpectRadius", "")),
        O("MeasureDist", "量距离", "📏", "Calc", "直接量两个几何结果之间的距离，并和标称值比公差。",
            C("第一个", "CompareSource", ByStep(""), SRC),
            C("第二个", "CompareVar", ByStep(""), SRC),
            N("标称值", "NominalValue", "图纸上写的标准尺寸"),
            N("公差", "Tolerance", "允许偏差多少")),

        // ================= 标定换算 =================
        O("CalibNPoint", "几点标定", "🎯", "Calib", "点几个已知机台位置的点，算出像素和毫米的换算关系。",
            T("标定点数据", "CalcExpression", "每行一个点：像素X,像素Y,机械X,机械Y"),
            N("移动步长(mm)", "NominalValue", "标定时每次移动多少毫米")),
        O("CalibManual", "手动标定", "🧮", "Calib", "直接给比例和旋转，快速近似换算（不用打标定板）。",
            N("X方向毫米/像素", "TargetX", ""),
            N("Y方向毫米/像素", "TargetY", ""),
            N("旋转角度(度)", "RoiAngle", "")),
        O("CoordTrans", "坐标转换", "", "Calib", "把图像坐标换算成机台坐标（或反过来）。",
            C("换算哪个结果", "CompareSource", ByStep(""), SRC),
            B("反过来算(机台→图像)", "InvertImage", "")),
        O("ScalarTrans", "标量换算", "🔤", "Calib", "把像素值按比例换成毫米、角度、脉冲等工程单位。",
            N("乘这个系数", "NominalValue", ""),
            N("保留几位小数", "Decimals", "")),
        O("CalibAssist", "标定检查", "✅", "Calib", "用标定数据重算一遍，看误差有多大，够不够用。",
            N("允许误差(mm)", "Tolerance", "超过就报警")),

        // ================= 先修图 =================
        O("Binarize", "变黑白", "◐", "Color", "把图变成只有黑和白，后面的数块数、量尺寸都靠它。",
            C("二值化方式", "ThreshMode", "", THMODE),
            N("阈值", "ThreshLo", "0~255"),
            N("上限阈值", "ThreshHi", "只有双阈值方式用到"),
            B("反相", "InvertImage", "黑白对调一下")),
        O("PreProcess", "先修图", "🧹", "Color", "找图之前先把图洗干净：去噪点、加强边缘、补光线不均。",
            C("处理方式", "PreprocessType", "", new[] { "去噪点(高斯)", "去噪点(中值)", "锐化", "均衡亮度", "阴影校正" }),
            N("核大小", "SmoothSize", "奇数，越大处理得越狠也越糊")),
        O("ToGray", "转成黑白图", "🌑", "Color", "彩色图转成灰度图。大部分算法只看明暗，不看颜色。",
            C("取哪个通道", "PreprocessType", "只保留某一种颜色时选", new[] { "整图转灰", "只要红", "只要绿", "只要蓝" })),
        O("ColorSpace", "换颜色格式", "🎨", "Color", "在 RGB / HSV / 灰度之间转换，按颜色找东西时先做这步。",
            C("转成", "MatchMode", "", new[] { "HSV", "HLS", "灰度", "RGB", "BGR" })),
        O("SplitChannel", "拆通道", "🧵", "Color", "把彩色图拆成单通道图，挑最清楚的那一路去处理。",
            C("取哪一路", "ColorChannel", "", new[] { "R", "G", "B", "H", "S", "V" })),
        O("ImageMath", "两图运算", "➕", "Color", "两张图做加减或逻辑运算，常用于背景差分。",
            C("怎么算", "Operator", "", new[] { "相加", "相减", "取绝对差", "求与", "求或", "求异或", "取较亮", "取较暗" }),
            T("另一张图", "ImageSource", "留空则用上一次缓存的图")),
        O("RotateImage", "把图转正", "🔄", "Color", "按角度旋转或左右上下翻转图像。",
            N("旋转角度(度)", "RoiAngle", "正值逆时针"),
            B("左右翻转", "InvertImage", "像照镜子一样翻一下"),
            B("上下翻转", "FlipVertical", "")),
        O("MirrorFix", "镜像校正", "🪞", "Color", "相机装反或从背面拍时把图翻回来。",
            C("怎么翻", "MatchMode", "", new[] { "左右翻", "上下翻", "转180度" })),
        O("RemoveColorMark", "去掉彩色笔迹", "🧼", "Color", "把图上红笔/绿笔画的圈和线抹掉，用周围颜色填平，免得干扰找黑块。",
            C("抹掉哪种颜色", "MatchMode", "一般选红：产线/HMI 圈的记号多是红的", new[] { "红", "绿", "蓝", "任意彩色" }),
            N("颜色浓淡门槛", "Number1", "0~255，越大只抹越鲜艳的，默认 90"),
            N("抹平范围", "Number2", "用多大半径把抹掉的地方填平，默认 4")),

        // ================= 识别 =================
        O("ReadBarcode", "读条码", "𝄃", "Code", "读一维条码（Code128、EAN、39 等），把里面的字取出来。",
            C("码制", "MatchMode", "不确定就选自动", new[] { "自动识别", "一维条码", "二维码" })),
        O("ReadQR", "读二维码", "▦", "Code", "读 QR / DataMatrix 二维码，常用于追溯码、工单码。",
            C("码制", "MatchMode", "", new[] { "自动识别", "QR码", "DataMatrix" })),
        O("CompareText", "比对文字", "🔤", "Code", "把读到的码/文字和规定值比一比，不一样就报警。",
            T("应该是什么", "InputValue", "要比对的目标内容"),
            C("怎么算对", "Operator", "", new[] { "完全相同", "包含即可", "开头是" })),

        // ================= 逻辑与变量 =================
        O("IfThen", "如果就跳", "🔀", "Logic", "条件成立就跳到指定步骤，否则顺序往下。",
            C("比较哪一步结果", "CompareSource", ByStep(""), SRC),
            C("怎么比", "Operator", "", new[] { "大于", "小于", "等于", "不等于", "包含" }),
            T("和什么比", "InputValue", ""),
            N("成立时跳到第几步", "JumpTarget", "0 表示不跳")),
        O("SwitchCase", "多选一分流", "🔱", "Logic", "按变量的值走不同分支（像多档开关）。",
            T("看哪个变量", "CompareVar", ""),
            T("取值对应表", "CalcExpression", "写成 值=步骤号，用分号隔开，如 A=2;B=5")),
        O("Counter", "计数器", "🔢", "Logic", "每跑一次加一，到设定值触发或清零。",
            T("计数器名字", "LoopVar", ""),
            N("到多少触发", "LoopCount", "0 表示只计数不触发")),
        O("TimerTool", "计时看超时", "⏲", "Logic", "记下从开始到现在过了多久，用来做看门狗。",
            T("计时器名字", "LoopVar", ""),
            N("超过多少毫秒报警", "Timeout", "")),
        O("TimeStart", "计时开始", "▶", "Logic", "给某个计时器按下开始。",
            T("计时器名字", "LoopVar", "")),
        O("TimeEnd", "计时结束", "⏹", "Logic", "结束计时并把耗时写进结果。",
            T("计时器名字", "LoopVar", "")),
        O("ReadVar", "读变量", "📖", "Logic", "把某个变量的值取出来给后面用。",
            T("变量名", "CompareVar", "")),
        O("WriteVar", "写变量", "✍", "Logic", "把一个值存进变量。",
            T("变量名", "CompareVar", ""),
            T("要写的值", "InputValue", "可以写数字，也可以写 {3} 取第3步结果")),
        O("ClearVar", "清变量", "🧽", "Logic", "把变量恢复成初始值，换型时常用。",
            T("变量名", "CompareVar", "留空=清空全部局部变量")),
        O("ArrayTool", "数组", "🗂", "Logic", "把多个结果攒成一串，取第几个或算总和平均。",
            T("数组名", "LoopVar", ""),
            N("取第几个", "IndexProbe", "-1 表示统计整体"),
            C("怎么统计", "MeasureType", "", new[] { "取第几个", "求和", "求平均", "最大", "最小", "个数" })),
        O("PointSet", "点集", "🎇", "Logic", "把若干个点收集起来，供拟合或补偿使用。",
            T("点集名", "LoopVar", ""),
            C("收集哪一步的点", "CompareSource", ByStep(""), SRC)),
        O("JudgeValue", "判个数对不对", "⚖", "Logic", "拿一个数值和上下限比，判合格不合格。",
            C("判哪一步结果", "CompareSource", ByStep(""), SRC),
            N("下限", "AreaMin", ""),
            N("上限", "AreaMax", "")),
        O("Formula", "公式计算", "🧮", "Logic", "写一条算式，用前面步骤的结果当输入。",
            T("算式", "CalcExpression", "例如 {3} * 2 + {5}，花括号里是步号"),
            T("结果写入", "OutputVar", "")),
        O("WaitCondition", "等条件", "⌛", "Logic", "一直等到条件成立（或超时）再往下走。",
            T("等什么", "WaitCondition", "例如 DI_3=通 或 {5}>0.85"),
            N("最多等多久(ms)", "WaitTime", "")),
        O("Group", "工具组", "📦", "Logic", "把几步打包成一组，方便整组跳过或重跑。",
            T("组名", "Remark", "")),
        O("BreakFlow", "中断流程", "⛔", "Logic", "满足条件就直接结束本条流程。",
            C("什么时候中断", "Operator", "", new[] { "无条件", "上一步失败", "上一步合格", "上一步不合格" })),

        // ================= 输出与界面 =================
        O("ShowResult", "显示结果", "🖥", "Output", "把结果画到图上（圆、十字、连线、大字），现场一眼看到。",
            C("画哪一步结果", "CompareSource", ByStep(""), SRC),
            C("再叠哪一步", "CompareVar", "想把基准圆也画上，就选「找圆」那一步", SRC),
            C("画什么", "MeasureType", "", new[] { "圆和点", "只要十字", "只要文字" }),
            T("标签文字", "NotifyMessage", "只支持英文数字，例如 OK0；留空写 OK，后面自动跟(读数)")),
        O("PopNotice", "说句话", "💬", "Output", "在软件右下角提示一条消息，不打断操作。",
            T("提示内容", "NotifyMessage", ""),
            C("级别", "NotifyLevel", "", new[] { "普通", "成功", "警告", "错误" })),
        O("OutputPlc", "送结果给机台", "📤", "Output", "把合格/不合格或测量值写到通讯通道给 PLC。",
            C("送什么", "MeasureType", "", new[] { "合格标志", "匹配分数", "坐标X", "坐标Y", "角度", "数量" }),
            T("送到哪", "CommChannel", "选一个已配置的通讯通道")),
        O("SaveData", "存数据表", "🗃", "Output", "把这一步结果按行存成表格文件，方便追溯。",
            C("格式", "SaveFormat", "", new[] { "CSV", "Excel" }),
            T("存到哪", "SavePath", "留空存到软件数据目录")),
        O("SaveImage", "存这张图", "💾", "Save", "把当前这张图存成文件，留证据或做追溯。",
            T("存到哪", "SavePath", "留空存到软件图片目录"),
            C("格式", "ImageFormat", "", new[] { "PNG", "JPG", "BMP" }),
            C("文件名带什么", "SaveFields", "", new[] { "序号", "日期时间" })),
        O("LogBook", "记日志", "📝", "Output", "往运行日志里写一行，事后排查用。",
            T("日志内容", "NotifyMessage", "")),

        // ================= 让设备动 =================
        O("AxisMoveAbs", "轴走到位置", "🦾", "Motion", "让某个轴走到绝对位置并等它到位。",
            T("哪个轴", "TargetAxis", ""),
            N("走到哪", "TargetX", "绝对位置"),
            N("速度", "Speed", ""),
            N("到位最多等多久(ms)", "InPosTimeout", "")),
        O("AxisMoveRel", "轴走多远", "🦿", "Motion", "相对当前位置再走一段距离。",
            T("哪个轴", "TargetAxis", ""),
            N("走多少", "MoveDistance", "正负代表方向"),
            N("速度", "Speed", "")),
        O("AxisHomeOp", "轴回原点", "🏠", "Motion", "让轴回到机械原点并建立坐标零点。",
            T("哪个轴", "TargetAxis", ""),
            N("回原点偏移", "HomeOffset", "回到原点后再走这么多才停")),
        O("AxisEnableOp", "轴上电解锁", "🔋", "Motion", "给驱动器使能（上电）或禁能（脱扣）。",
            T("哪个轴", "TargetAxis", ""),
            C("做什么", "SwitchState", "", new[] { "上电解锁", "脱扣" })),
        O("AxisStopOp", "轴停止", "🛑", "Motion", "让轴立刻减速停下。",
            T("哪个轴", "TargetAxis", "留空=所有轴")),
        O("AllAxisEmg", "整机急停", "⛔", "Motion", "所有轴同时急停，安全回路上用。"),
        O("LineInterp", "直线走两点", "↗", "Motion", "两个轴同时联动走一条斜线。",
            T("X 轴", "TargetAxis", ""), T("Y 轴", "CompareVar", ""),
            N("目标X", "TargetX", ""), N("目标Y", "TargetY", ""), N("速度", "Speed", "")),
        O("ArcInterp", "圆弧走一圈", "◑", "Motion", "两轴联动走一段圆弧。",
            T("X 轴", "TargetAxis", ""), T("Y 轴", "CompareVar", ""),
            N("圆心X", "TargetX", ""), N("圆心Y", "TargetY", ""),
            N("半径", "ExpectRadius", ""), N("跨越角度(度)", "AngleStop", "")),
        O("PointTable", "按点表走位", "📇", "Motion", "依次执行点表里存好的位置（换型、多工位）。",
            T("点表名", "SubFlowName", ""),
            N("执行第几个点", "IndexProbe", "-1 表示按顺序自动往下")),
        O("IoWrite", "写开关量", "🔌", "Motion", "输出一个开关信号（气缸、灯、继电器）。",
            T("输出点", "IoChannel", ""),
            C("输出什么", "SwitchState", "", new[] { "通", "断" }),
            B("写完回读确认", "ConfirmWrite", "多花一点时间，但更保险")),
        O("IoRead", "读开关量", "📡", "Motion", "读一个输入信号，判断到位没、有料没。",
            T("输入点", "IoChannel", ""),
            C("期望是什么", "SwitchState", "", new[] { "通", "断" }),
            N("最多等多久(ms)", "WaitTime", "")),
        O("CylinderTool", "气缸动作", "🟢", "Motion", "控制气缸伸出/缩回，并等磁性开关确认。",
            T("气缸名", "CylName", ""),
            C("做什么", "CylAction", "", new[] { "伸出", "缩回", "保持" }),
            T("伸出到位信号", "SensorIn", ""), T("缩回到位信号", "SensorOut", ""),
            N("最多等多久(ms)", "CylTimeout", "")),
        O("EncoderTool", "读编码器", "🌀", "Motion", "读外部编码器当前位置，飞拍/跟随用。",
            T("编码器轴", "TargetAxis", "")),
        O("HighSpeedCompare", "高速比较触发", "⚡", "Motion", "位置到了设定点就由控制卡硬件直接触发拍照。",
            T("哪个轴", "TargetAxis", ""),
            N("触发位置", "TargetX", ""),
            N("间隔多少触发一次", "MinDistance", "")),

        // ================= 发出去 =================
        O("CommSend", "发一条", "📤", "Comm", "通过已连上的通道发一条内容（文本或指令）。",
            T("通道", "CommChannel", "选一个通讯配置"),
            T("发什么", "CommContent", "支持 {3} 取第3步结果"),
            C("编码", "CommEncoding", "", new[] { "文本", "十六进制" })),
        O("CommRecv", "收数据", "📥", "Comm", "等对方发来一条内容并存进变量。",
            T("通道", "CommChannel", ""),
            N("最多等多久(ms)", "Timeout", ""),
            T("存到变量", "OutputVar", "")),
        O("ModbusWrite", "Modbus 写", "🟦", "Comm", "往 PLC 的寄存器或线圈写值。",
            T("通道", "CommChannel", ""),
            C("写哪种", "IoType", "", new[] { "保持寄存器", "线圈" }),
            N("地址", "RoiX", "例如 40001 或 0"),
            N("写多少", "TargetX", "")),
        O("ModbusRead", "Modbus 读", "🟩", "Comm", "从 PLC 读一个寄存器或线圈。",
            T("通道", "CommChannel", ""),
            C("读哪种", "IoType", "", new[] { "保持寄存器", "线圈" }),
            N("地址", "RoiX", ""),
            T("存到变量", "OutputVar", "")),
        O("ModbusReadArr", "Modbus 读一串", "🟨", "Comm", "一次读连续多个寄存器，适合批量数据。",
            T("通道", "CommChannel", ""),
            N("起始地址", "RoiX", ""),
            N("读几个", "TopN", ""),
            T("存到数组", "LoopVar", "")),
        O("KeyenceLink", "读基恩士PLC", "🟥", "Comm", "基恩士 KV 系列主机链路读写。",
            T("通道", "CommChannel", ""),
            C("读还是写", "IoType", "", new[] { "读", "写" }),
            T("软元件地址", "IoChannel", "例如 DM100"),
            N("值", "TargetX", "")),
        // ===== 以下来自开源项目 OpenCVVision（Apache-2.0）的工具移植 =====
        O("MorphOp", "形态学处理", "⬛", "OpenCVVision", "把黑白图上的块放大缩小、补洞、去毛刺（腐蚀/膨胀/开/闭/顶帽等）。",
            C("做什么", "Mode", "", new[] { "腐蚀(缩小)", "膨胀(放大)", "开(去小毛刺)", "闭(补小洞)", "梯度(留边缘)", "顶帽(亮小点)", "黑帽(暗小点)", "击不中" }),
            C("核形状", "MatchMode", "", new[] { "矩形", "十字", "椭圆" }),
            N("核宽", "SizeX", ""), N("核高", "SizeY", "")),

        O("BilateralOp", "保边去噪", "🌫", "OpenCVVision", "只抹平小噪点、不糊掉边缘，金属和塑料件特别好用。",
            N("邻域大小", "SizeX", ""), N("颜色差阈值", "Number1", "差值小于这个才互相平均"),
            N("距离差阈值", "Number2", "")),

        O("EmphasizeOp", "加强边缘", "✨", "OpenCVVision", "把和周围平均亮度差别大的地方拉得更明显，让浅淡的边看得清。",
            N("核宽", "SizeX", ""), N("核高", "SizeY", ""),
            N("加强倍数", "Factor", "1=轻微，3=很强也容易放大噪点")),

        O("LightDarkOp", "明暗筛选", "", "OpenCVVision", "按局部背景只留明显更亮或更暗的部分，专门对付光照不均、反光。",
            N("核大小", "Number1", "局部背景取样范围，默认 9"),
            N("块宽", "Number2", "核必须能被它整除，默认 3"),
            C("留哪边", "Mode", "", new[] { "只留暗的", "只留亮的", "明暗都留" }),
            N("亮保留比例(%)", "Number3", "越大留得越多"), N("暗保留比例(%)", "Number4", "")),

        O("MaskRegionOp", "只看这块", "🔲", "OpenCVVision", "把不关心的区域涂黑，只留框内（或反过来只留框外），后面的处理就不会被干扰。",
            C("怎么留", "Mode", "", new[] { "只留框内", "挖掉框内" })),

        O("PyramidOp", "金字塔缩放", "🔻", "OpenCVVision", "整张图缩小或放大几档，或取出明暗漂移层。",
            C("做什么", "Mode", "", new[] { "缩小", "放大", "取明暗层" }),
            N("做几层", "Number1", "1~6")),

        O("HoughCirclesOp", "找一堆圆", "⭕", "OpenCVVision", "一次找出画面里所有大小相近的圆（孔、珠子、瓶盖）。",
            N("最小半径", "Number1", ""), N("最大半径", "Number2", ""),
            N("圆心最小间隔", "Number3", "防止同一个圆重复报"),
            N("灵敏度", "Number4", "累加阈值，越小找到的圆越多也越容易误报")),

        O("ContourShapeOp", "配外形框", "🔷", "OpenCVVision", "给找到的轮廓套一个外接框/最小矩形/凸包/最小圆/椭圆，输出尺寸角度。",
            C("找轮廓方式", "MatchMode", "", new[] { "只找最外圈", "找全部", "找内孔", "同层平铺" }),
            C("套什么外形", "Mode", "", new[] { "外接矩形", "最小矩形", "凸包", "最小圆", "最小三角", "椭圆" }),
            N("最小面积", "AreaMin", "")),

        O("BlobDetectorOp", "斑点检测", "🫧", "OpenCVVision", "按颜色、大小、圆度、凸性挑出斑点（黑点、气泡、脏点）。",
            C("找哪种斑", "Mode", "", new[] { "暗斑", "亮斑" }),
            N("最小面积", "AreaMin", ""), N("最大面积", "AreaMax", ""),
            N("最圆程度", "Number1", "0~1，越接近 1 越圆"), N("最多几个", "TopN", "")),

        O("ConnectedCompOp", "连通域染色", "🎨", "OpenCVVision", "把每一块独立区域染成不同颜色，可按面积/宽/高/位置筛掉不要的。",
            N("最小面积", "AreaMin", ""), N("最大面积", "AreaMax", ""),
            N("最小宽度", "Number1", ""), N("最小高度", "Number2", ""),
            B("输出染色图", "InvertImage", "生成一张每块一色的图")),

        O("FeatureDetectOp", "找特征点", "✳", "OpenCVVision", "自动找出图上有辨识度的角点/斑点，用来配准或比对。",
            C("用哪种方法", "Mode", "", new[] { "ORB", "SIFT", "AKAZE", "BRISK", "FAST", "角点(GFTT)" }),
            N("最多几个点", "Number1", "")),

        O("FeatureRegisterOp", "特征配准", "🧷", "OpenCVVision", "把第二张图按特征点对齐到第一张（位置角度都自动纠正）。",
            C("用哪种方法", "Mode", "", new[] { "ORB", "SIFT", "AKAZE", "BRISK", "FAST", "角点(GFTT)" }),
            C("匹配器", "MatchMode", "", new[] { "暴力匹配", "FLANN(大数据快)" }),
            T("参考图", "ImageSource", "对齐的目标图，留空用当前缓存图")),

        O("LensCalibOp", "镜头标定", "🎥", "OpenCVVision", "拍一批棋盘格照片，算出镜头畸变并修正——尺寸测量精度靠它。",
            N("横向内角点数", "SizeX", "11 格板填 10"),
            N("纵向内角点数", "SizeY", ""),
            N("每格多少毫米", "NominalValue", ""),
            T("照片文件夹", "FolderPath", "放 10~20 张不同角度拍的棋盘格")),

        O("LensUndistortOp", "去掉镜头畸变", "🫳", "OpenCVVision", "用上次标定的结果把当前图拉直，边缘不再拉伸变形。"),

        O("DnnDetectOp", "AI 找目标", "🧠", "OpenCVVision", "用训练好的模型找出目标位置（缺什么、装没装、有没有划伤）。",
            T("模型文件", "ModelFile", ".onnx，或 .weights（要配 cfg）；放进步骤同目录 Data\\Models 可自动找"),
            T("cfg 文件", "CalcExpression", "只有 Darknet 权重需要"),
            T("类别名单", "LabelsFile", "一行一个类别名的 txt"),
            N("置信度门槛", "Number1", "0~1，建议 0.4"),
            N("重叠抑制", "Number2", "建议 0.45")),

    };

    static readonly Dictionary<string, OperatorDef> ByType = Build();

    static Dictionary<string, OperatorDef> Build()
    {
        var d = new Dictionary<string, OperatorDef>();
        foreach (var o in All) d[o.StepType] = o;
        return d;
    }

    /// <summary>按 StepType 取算子定义；取不到说明这个算子还用旧的手写面板。</summary>
    public static OperatorDef? Find(string? stepType) =>
        string.IsNullOrEmpty(stepType) ? null : ByType.GetValueOrDefault(stepType!);

    public static bool Has(string? stepType) => stepType != null && ByType.ContainsKey(stepType);
}
