using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace NoCodeVision.Views;

public partial class FlowView : UserControl
{
    public FlowView()
    {
        InitializeComponent();
        DataContext = new ViewModels.FlowViewModel();
    }

    /// <summary>「➕ 添加」点开两级菜单：默认停在第一个分类上。</summary>
    private void AddToolBtn_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.FlowViewModel vm) return;
        if (vm.SelectedToolGroup == null) vm.SelectedToolGroup = vm.ToolGroups.FirstOrDefault();
        vm.IsAddMenuOpen = true;
    }

    /// <summary>双击流程名 → 就地改名。</summary>
    private void FlowName_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not System.Windows.Controls.TextBlock tb) return;
        if (tb.Tag is not ViewModels.VisionFlow f) return;
        foreach (var fl in Flows) fl.IsEditing = ReferenceEquals(fl, f);   // 输入框靠 IsEditing 的触发器显出来，VisibleChanged 里抢焦点
    }

    /// <summary>输入框一显示就抢焦点，双击完直接能打字。</summary>
    private void FlowNameEdit_VisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb && tb.IsVisible) tb.Focus();
    }

    /// <summary>改完名：回车或点到别处就收起输入框。</summary>
    private void FlowNameEdit_LostFocus(object sender, System.Windows.RoutedEventArgs e) => EndRename(sender);

    private void FlowNameEdit_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Escape)
        {
            EndRename(sender);
            e.Handled = true;
        }
    }

    static void EndRename(object sender)
    {
        if (sender is System.Windows.FrameworkElement fe && fe.Tag is ViewModels.VisionFlow f) f.IsEditing = false;
    }

    IEnumerable<ViewModels.VisionFlow> Flows =>
        (DataContext as ViewModels.FlowViewModel)?.Flows ?? Enumerable.Empty<ViewModels.VisionFlow>();
}
// 温启志：18719361399  混淆: 温u启t志b：z1q8o7z1m9l3w6r1r3m9i9
