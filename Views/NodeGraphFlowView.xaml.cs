using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoCodeVision.ViewModels;

namespace NoCodeVision.Views;

/// <summary>节点图流程编辑器视图：算子库拖拽落点、画布与 NodeGraphViewModel 的绑定入口。</summary>

public partial class NodeGraphFlowView : UserControl

{

    private const string ToolFormat = "NCV.NodeGraphTool";

    private string? _draggingTool;

    private Point _dragStart;



    public NodeGraphFlowView()

    {

        InitializeComponent();

    }



    private FlowViewModel? Vm => DataContext as FlowViewModel;



    private void OnViewLoaded(object sender, RoutedEventArgs e)

    {

        var vm = Vm;

        if (vm != null) vm.NodeGraph.Bind(vm.SelectedFlow);

    }



    private void OnToolDown(object sender, MouseButtonEventArgs e)

    {

        if (sender is not FrameworkElement el || el.DataContext is not NodeGraphTool tool) return;

        _draggingTool = tool.Name;

        _dragStart = e.GetPosition(el);

    }



    private void OnToolMove(object sender, MouseEventArgs e)

    {

        if (_draggingTool == null || e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement el) return;

        if ((e.GetPosition(el) - _dragStart).Length < SystemParameters.MinimumHorizontalDragDistance) return;

        DragDrop.DoDragDrop(el, new DataObject(ToolFormat, _draggingTool), DragDropEffects.Copy);

        _draggingTool = null;

    }



    private void OnCanvasDragOver(object sender, DragEventArgs e)

    {

        e.Effects = e.Data.GetDataPresent(ToolFormat) ? DragDropEffects.Copy : DragDropEffects.None;

        e.Handled = true;

    }



    private void OnCanvasDrop(object sender, DragEventArgs e)

    {

        if (sender is not Nodify.NodifyEditor editor) return;

        if (e.Data.GetData(ToolFormat) is not string name) return;

        Vm?.NodeGraph.AddNodeAt(name, editor.GetLocationInsideEditor(e));

        e.Handled = true;

    }

}
