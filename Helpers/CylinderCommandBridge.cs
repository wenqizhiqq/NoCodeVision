using System.Reflection;
using System.Windows.Input;
using NoCodeVision.ViewModels;

namespace NoCodeVision.Helpers;

/// <summary>
/// 气缸列表动作列的命令桥。
/// 由于 MotionRow.Status/Action 是自动属性（无 INPC），按钮点击后通过反射触发 OnPropertyChanged 刷新列表。
/// </summary>
public static class CylinderCommandBridge
{
    public static ICommand ExtendCmd { get; } = new RelayCommand(p =>
    {
        if (p is not MotionRow row) return;
        row.Status = "伸出";
        row.Action = "缩回";
        RaisePropertyChanged(row, nameof(MotionRow.Status));
        RaisePropertyChanged(row, nameof(MotionRow.Action));
    });

    public static ICommand RetractCmd { get; } = new RelayCommand(p =>
    {
        if (p is not MotionRow row) return;
        row.Status = "缩回";
        row.Action = "伸出";
        RaisePropertyChanged(row, nameof(MotionRow.Status));
        RaisePropertyChanged(row, nameof(MotionRow.Action));
    });

    private static void RaisePropertyChanged(MotionRow row, string name)
    {
        var method = typeof(ViewModelBase).GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(row, new object?[] { name });
    }
}
