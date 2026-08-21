using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using NoCodeVision.ViewModels;

namespace NoCodeVision.Views;

/// <summary>
/// 脚本流程视图：基于 AvalonEdit 的 Lua 编辑器，集成 运行/单步/暂停/停止/断点/耗时、
/// 智能提示、变量点击查看、调用栈与输出控制台。
/// </summary>
public partial class ScriptFlowView : UserControl
{
    private FlowViewModel? _vm;
    private CompletionWindow? _completionWindow;
    private SearchPanel? _searchPanel;
    private readonly BreakpointMargin _bpMargin;

    public ScriptFlowView()
    {
        InitializeComponent();
        _bpMargin = new BreakpointMargin(this);
        Editor.TextArea.LeftMargins.Insert(0, _bpMargin);
        Editor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineBackgroundRenderer(this));
        Editor.SyntaxHighlighting = LoadLuaHighlighting();

        Editor.TextChanged += (_, _) =>
        {
            if (_vm?.SelectedFlow != null) _vm.SelectedFlow.ScriptContent = Editor.Text;
        };
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateSelectedVariable();
        Editor.TextArea.TextEntered += OnTextEntered;
        Editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
        Editor.TextArea.PreviewMouseWheel += OnPreviewMouseWheel;

        _searchPanel = SearchPanel.Install(Editor.TextArea);

        DataContextChanged += (_, _) => BindVm();
        Loaded += (_, _) => { BindVm(); if (_vm?.SelectedFlow != null) Editor.Text = _vm.SelectedFlow.ScriptContent ?? ""; };
    }

    private void BindVm()
    {
        if (ReferenceEquals(_vm, DataContext)) return;
        if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as FlowViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            if (_vm.SelectedFlow != null) Editor.Text = _vm.SelectedFlow.ScriptContent ?? "";
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "SelectedFlow")
        {
            Editor.Text = _vm?.SelectedFlow?.ScriptContent ?? "";
        }
        else if (e.PropertyName == "Breakpoints" || e.PropertyName == "CurrentLine")
        {
            Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            _bpMargin.InvalidateVisual();
        }
    }

    // ---------- 工具栏按钮 ----------
    private void BreakpointButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm?.SelectedFlow == null) return;
        int line = Editor.TextArea.Caret.Line;
        _vm.ToggleBreakpointCmd.Execute(line);
    }

    private void FindButton_Click(object sender, RoutedEventArgs e)
    {
        _searchPanel?.Open();
        _searchPanel?.Reactivate();
    }

    private void CompleteButton_Click(object sender, RoutedEventArgs e) => ShowCompletion();

    // ---------- 智能提示 ----------
    private void OnTextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length == 1 && char.IsLetter(e.Text[0]))
            ShowCompletion();
    }

    private void ShowCompletion()
    {
        if (_vm == null) return;
        _completionWindow = new CompletionWindow(Editor.TextArea);
        _completionWindow.Closed += (_, _) => _completionWindow = null;
        foreach (var c in CompletionItems)
            _completionWindow.CompletionList.CompletionData.Add(c);
        _completionWindow.Show();
    }

    // ---------- 键盘快捷键 ----------
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            ShowCompletion();
        }
        else if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            ToggleComment();
        }
    }

    private void ToggleComment()
    {
        var doc = Editor.Document;
        var seg = (ICSharpCode.AvalonEdit.Document.ISegment)Editor.TextArea.Selection;
        int caretOff = Editor.TextArea.Caret.Offset;
        int startOff = seg.Length == 0 ? caretOff : seg.Offset;
        int endOff = seg.Length == 0 ? caretOff : seg.EndOffset;
        int start = doc.GetLineByOffset(startOff).LineNumber;
        int end = doc.GetLineByOffset(endOff).LineNumber;
        bool hasComment = false;
        for (int i = start; i <= end; i++)
        {
            var l = doc.GetLineByNumber(i);
            var t = doc.GetText(l.Offset, l.Length);
            if (t.TrimStart().StartsWith("--")) { hasComment = true; break; }
        }
        Editor.Document.BeginUpdate();
        for (int i = start; i <= end; i++)
        {
            var l = doc.GetLineByNumber(i);
            var t = doc.GetText(l.Offset, l.Length);
            if (hasComment)
            {
                int idx = t.IndexOf("--", StringComparison.Ordinal);
                if (idx >= 0) doc.Remove(l.Offset + idx, 2);
            }
            else
            {
                doc.Insert(l.Offset, "-- ");
            }
        }
        Editor.Document.EndUpdate();
    }

    private void OnPreviewMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            Editor.FontSize = Math.Max(9, Math.Min(32, Editor.FontSize + (e.Delta > 0 ? 1 : -1)));
        }
    }

    // ---------- 点击变量显示名称 + 值 ----------
    private void UpdateSelectedVariable()
    {
        if (_vm?.Variables == null) return;
        var doc = Editor.Document;
        int off = Editor.TextArea.Caret.Offset;
        if (off < 0 || off > doc.TextLength) { _vm.SelectedVariable = null; return; }
        int s = off, en = off;
        while (s > 0 && IsIdent(doc.GetCharAt(s - 1))) s--;
        while (en < doc.TextLength && IsIdent(doc.GetCharAt(en))) en++;
        if (s == en) { _vm.SelectedVariable = null; return; }
        string word = doc.GetText(s, en - s);
        _vm.SelectedVariable = _vm.Variables.FirstOrDefault(v => v.Name == word);
    }

    private static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c == '_';

    // ---------- Lua 语法高亮 ----------
    private static IHighlightingDefinition? LoadLuaHighlighting()
    {
        try
        {
            const string xshd = @"<?xml version=""1.0""?>
<SyntaxDefinition name=""Lua"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition"">
  <Color name=""Comment"" foreground=""#6A9955"" />
  <Color name=""String"" foreground=""#CE9178"" />
  <Color name=""Number"" foreground=""#B5CEA8"" />
  <Color name=""Keyword"" foreground=""#569CD6"" fontWeight=""bold"" />
  <Color name=""Builtin"" foreground=""#4EC9B0"" />
  <RuleSet>
    <Span name=""Comment"" color=""Comment"" begin=""--\[\["" end=""\]\]"" />
    <Span name=""Comment"" color=""Comment"" begin=""--"" end=""\n"" />
    <Span name=""String"" color=""String"" begin=""'"" end=""'"" />
    <Span name=""String"" color=""String"" begin=""&quot;"" end=""&quot;"" />
    <Span name=""String"" color=""String"" begin=""\[\["" end=""\]\]"" />
    <Rule foreground=""Number"">\b\d+(\.\d+)?\b</Rule>
    <Keywords color=""Keyword"">
      <Word>and</Word><Word>break</Word><Word>do</Word><Word>else</Word>
      <Word>elseif</Word><Word>end</Word><Word>false</Word><Word>for</Word>
      <Word>function</Word><Word>goto</Word><Word>if</Word><Word>in</Word>
      <Word>local</Word><Word>nil</Word><Word>not</Word><Word>or</Word>
      <Word>repeat</Word><Word>return</Word><Word>then</Word><Word>true</Word>
      <Word>until</Word><Word>while</Word>
    </Keywords>
    <Keywords color=""Builtin"">
      <Word>print</Word><Word>log</Word><Word>sleep</Word>
      <Word>vision</Word><Word>plc</Word>
      <Word>string</Word><Word>table</Word><Word>math</Word><Word>os</Word>
      <Word>ipairs</Word><Word>pairs</Word><Word>tonumber</Word><Word>tostring</Word>
      <Word>type</Word><Word>assert</Word><Word>error</Word><Word>pcall</Word>
      <Word>require</Word><Word>select</Word><Word>next</Word><Word>unpack</Word>
      <Word>rawget</Word><Word>rawset</Word><Word>setmetatable</Word><Word>getmetatable</Word>
    </Keywords>
  </RuleSet>
</SyntaxDefinition>";
            using var reader = XmlReader.Create(new StringReader(xshd));
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch
        {
            return null;
        }
    }

    // ---------- 断点槽（左侧边栏） ----------
    private sealed class BreakpointMargin : AbstractMargin
    {
        private readonly ScriptFlowView _owner;
        public BreakpointMargin(ScriptFlowView owner) { _owner = owner; Width = 18; }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var vm = _owner._vm;
            if (vm == null || TextView == null) return;
            foreach (int line in vm.Breakpoints)
            {
                if (line < 1 || line > TextView.Document.LineCount) continue;
                double y = TextView.GetVisualTopByDocumentLine(line) + TextView.DefaultLineHeight / 2;
                drawingContext.DrawEllipse(Brushes.Crimson, new Pen(Brushes.White, 1.2), new Point(9, y), 6, 6);
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            var vm = _owner._vm;
            if (vm == null || TextView == null) return;
            e.Handled = true;
            int line = TextView.GetDocumentLineByVisualTop(e.GetPosition(this).Y)?.LineNumber ?? 0;
            if (line > 0) vm.ToggleBreakpointCmd.Execute(line);
        }
    }

    // ---------- 当前执行行高亮 ----------
    private sealed class CurrentLineBackgroundRenderer : IBackgroundRenderer
    {
        private readonly ScriptFlowView _owner;
        public CurrentLineBackgroundRenderer(ScriptFlowView owner) { _owner = owner; }
        public KnownLayer Layer => KnownLayer.Background;
        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var vm = _owner._vm;
            if (vm == null || vm.CurrentLine < 1 || vm.CurrentLine > textView.Document.LineCount) return;
            double y = textView.GetVisualTopByDocumentLine(vm.CurrentLine);
            var brush = new SolidColorBrush(Color.FromArgb(46, 37, 99, 235));
            drawingContext.DrawRectangle(brush, null, new Rect(0, y, textView.ActualWidth, textView.DefaultLineHeight));
        }
    }

    // ---------- 智能提示数据 ----------
    private static readonly List<LuaCompletion> CompletionItems = new()
    {
        new("local", "声明局部变量"),
        new("function", "定义函数"),
        new("if", "条件分支"), new("elseif"), new("else"), new("then"), new("end"),
        new("for", "循环"), new("while", "循环"), new("repeat"), new("until"),
        new("do"), new("in"), new("break"), new("return"),
        new("and"), new("or"), new("not"), new("nil"), new("true"), new("false"),
        new("print", "打印输出"), new("log", "记录日志"), new("sleep", "延时(ms)"),
        new("vision", "视觉接口"), new("vision.match", "模板匹配，返回分数"),
        new("vision.find", "查找目标"), new("vision.grab", "抓取图像"),
        new("plc", "PLC 接口"), new("plc.write", "写寄存器(addr,value)"),
        new("plc.read", "读寄存器(addr)"),
        new("pairs"), new("ipairs"), new("tonumber"), new("tostring"), new("type"),
        new("assert"), new("error"), new("pcall"), new("xpcall"), new("require"),
        new("select"), new("next"), new("unpack"), new("rawget"), new("rawset"),
        new("setmetatable"), new("getmetatable"), new("string"), new("table"), new("math"),
        new("string.format"), new("string.sub"), new("string.find"), new("string.len"),
        new("table.insert"), new("table.remove"), new("table.concat"), new("table.sort"),
        new("math.floor"), new("math.ceil"), new("math.abs"), new("math.max"), new("math.min"), new("math.pi")
    };

    private sealed class LuaCompletion : ICompletionData
    {
        public LuaCompletion(string text, string desc = "")
        {
            Text = text;
            Description = desc;
        }
        public ImageSource? Image => null;
        public string Text { get; }
        public object Content => Text;
        public object Description { get; }
        public double Priority => 0;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs e)
            => textArea.Document.Replace(completionSegment, Text);
    }
}
