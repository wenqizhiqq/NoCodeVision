using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace NoCodeVision.Views
{
    // 与加密的 ScriptFlowView.xaml.cs 同属一个 partial 类：补齐 NoCodeMotion Lua 编辑器
    // 已有的「智能插入 / 回撤 / 重做」等交互（加密文件无法直改，故拆到本文件）。
    public partial class ScriptFlowView
    {
        private class ApiItem
        {
            public string Name { get; set; } = "";
            public string Snippet { get; set; } = "";
        }

        private class ApiCat
        {
            public string Name { get; set; } = "";
            public List<ApiItem> Items { get; set; } = new();
        }

        // 智能插入目录：对齐 NoCodeVision 实际 Lua API（vision / plc / 流程 / math / print）
        private readonly List<ApiCat> _apiCatalog = new()
        {
            new ApiCat { Name = "视觉 vision", Items = new() {
                new ApiItem { Name = "采集图像 grab()", Snippet = "vision.grab()" },
                new ApiItem { Name = "模板匹配 match()", Snippet = "vision.match(\"tpl_A.png\")" },
                new ApiItem { Name = "查找 find()", Snippet = "vision.find(\"tpl_A.png\")" },
            }},
            new ApiCat { Name = "运控 plc", Items = new() {
                new ApiItem { Name = "写 PLC write()", Snippet = "plc.write(200, 1)" },
                new ApiItem { Name = "读 PLC read()", Snippet = "plc.read(200)" },
            }},
            new ApiCat { Name = "流程控制", Items = new() {
                new ApiItem { Name = "延时 sleep()", Snippet = "sleep(50)" },
                new ApiItem { Name = "条件 if", Snippet = "if score >= 0.85 then\n    \nend" },
                new ApiItem { Name = "循环 for", Snippet = "for i = 1, 10 do\n    \nend" },
            }},
            new ApiCat { Name = "计算 math", Items = new() {
                new ApiItem { Name = "开方 sqrt()", Snippet = "math.sqrt(x)" },
                new ApiItem { Name = "绝对值 abs()", Snippet = "math.abs(x)" },
                new ApiItem { Name = "取整 floor()", Snippet = "math.floor(x)" },
            }},
            new ApiCat { Name = "输出 print", Items = new() {
                new ApiItem { Name = "打印 print()", Snippet = "print(\"hello\")" },
            }},
        };

        private void Partial_Loaded(object sender, RoutedEventArgs e)
        {
            if (FuncList != null && FuncList.ItemsSource == null)
            {
                FuncList.ItemsSource = _apiCatalog;
                if (_apiCatalog.Count > 0)
                    FuncList.SelectedIndex = 0;
            }
        }

        private void FuncList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FuncList?.SelectedItem is ApiCat cat && NameList != null)
                NameList.ItemsSource = cat.Items;
        }

        private void NameList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (NameList?.SelectedItem is ApiItem item && Editor != null)
            {
                Editor.Document.Insert(Editor.CaretOffset, item.Snippet);
                Editor.Focus();
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => Editor?.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => Editor?.Redo();
    }
}
