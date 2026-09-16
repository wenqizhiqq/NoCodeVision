// 全局 UI/配置状态持久化：
// - 所有可保存状态集中写入 bin\Data\ui_state.json（遵循 AppPaths：不写 C 盘）
// - 支持两种用法：
//   1) 标量键值：GetInt / SetInt（如 MainWindow 记住上次选中的导航页）
//   2) ViewModel 反射快照：AttachVm(key, vm) —— 启动时回填、运行中自动保存
//      （仅捕获 标量属性 + string集合 + 自定义POCO集合；System.Windows 类型自动跳过）
// 作者：温启志 编写 ▢ 联系 wx:187-1936-1399
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;

namespace NoCodeVision.Helpers;

public static class UiState
{
    private static readonly object _lock = new();
    private static Dictionary<string, JsonObject>? _store;
    private static readonly List<(string Key, object Vm)> _attached = new();
    private static DispatcherTimer? _flushTimer;
    private static DispatcherTimer? _periodicTimer;
    private static bool _hooksDone;

    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "ui_state.json");

    private static Dictionary<string, JsonObject> Store
    {
        get
        {
            if (_store != null) return _store;
            _store = new Dictionary<string, JsonObject>();
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    if (!string.IsNullOrWhiteSpace(json) && JsonNode.Parse(json) is JsonObject root)
                        foreach (var kv in root)
                            if (kv.Value is JsonObject o)
                                _store[kv.Key] = o;
                }
            }
            catch { /* 损坏/不可读则从空开始 */ }
            HookExitAndPeriodic();
            return _store;
        }
    }

    private static void HookExitAndPeriodic()
    {
        if (_hooksDone) return;
        _hooksDone = true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushNow();

        // 每 5s 重新快照所有已挂接 VM（覆盖 VarItem/MotionRow 等无 INPC 的单元格编辑）并落盘
        var periodic = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        periodic.Tick += (_, _) =>
        {
            lock (_lock)
                foreach (var (k, vm) in _attached.ToList())
                    CaptureVmLocked(k, vm);
            FlushNow();
        };
        periodic.Start();
        _periodicTimer = periodic;
    }

    // ---------- 标量键值 ----------

    public static int GetInt(string key, int fallback)
    {
        lock (_lock)
        {
            return Store.TryGetValue(key, out var o) && o["v"] is JsonValue v && v.TryGetValue<int>(out var i)
                ? i : fallback;
        }
    }

    public static void SetInt(string key, int value)
    {
        lock (_lock)
        {
            Store[key] = new JsonObject { ["v"] = value };
            ScheduleFlush();
        }
    }

    // ---------- ViewModel 反射快照 ----------

    /// <summary>挂接 VM：先回填上次保存的状态，之后属性变化/集合增删自动快照落盘。</summary>
    public static void AttachVm(string key, object vm)
    {
        lock (_lock)
        {
            ApplyVmLocked(key, vm);
            if (!_attached.Any(a => a.Key == key && ReferenceEquals(a.Vm, vm)))
                _attached.Add((key, vm));
            if (vm is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += (_, e) =>
                {
                    if (string.IsNullOrEmpty(e.PropertyName) || IsTransient(e.PropertyName)) return;
                    CaptureVm(key, vm);
                    ScheduleFlush();
                };
            HookCollectionChanges(key, vm);
        }
    }

    /// <summary>运行期噪声属性：变化不触发保存（高频刷新/大对象）。</summary>
    private static bool IsTransient(string name) =>
        name is "CameraLiveImage" or "TemplatePreviewSource" or "LastImage" or "ExcelStatus"
            or "DebugLog" or "Status" or "StatusText" or "TemplatePreviewInfo";

    private static void HookCollectionChanges(string key, object vm)
    {
        foreach (var p in vm.GetType().GetProperties())
        {
            var g = p.GetGetMethod();
            if (g == null || !g.IsPublic) continue;
            if (p.Name.EndsWith("Log")) continue; // 日志类集合不保存
            if (p.Name is "Axes" or "DebugAxes") continue; // 400ms 模拟定时器高频 Clear/Add，不挂钩（5s 周期快照已覆盖）
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (!t.IsGenericType) continue;
            var def = t.GetGenericTypeDefinition();
            if (def != typeof(ObservableCollection<>) && def != typeof(List<>)) continue;
            if (p.GetValue(vm) is INotifyCollectionChanged ncc)
                ncc.CollectionChanged += (_, _) => { CaptureVm(key, vm); ScheduleFlush(); };
        }
    }

    private static bool IsScalar(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)
        || t == typeof(DateTime) || t == typeof(TimeSpan);

    private static bool IsStringCollection(Type t) =>
        t.IsGenericType
        && (t.GetGenericTypeDefinition() == typeof(ObservableCollection<>) || t.GetGenericTypeDefinition() == typeof(List<>))
        && t.GetGenericArguments()[0] == typeof(string);

    /// <summary>自定义 POCO 集合（应用命名空间、有无参构造），如 CameraItem/VarItem/MotionRow 等。</summary>
    private static bool IsPocoCollection(Type t) =>
        t.IsGenericType
        && (t.GetGenericTypeDefinition() == typeof(ObservableCollection<>) || t.GetGenericTypeDefinition() == typeof(List<>))
        && t.GetGenericArguments()[0] is { } it
        && it.IsClass
        && (it.Namespace == null || !it.Namespace.StartsWith("System"))
        && it.GetConstructor(Type.EmptyTypes) != null;

    private static void CaptureVm(string key, object vm)
    {
        lock (_lock) { CaptureVmLocked(key, vm); ScheduleFlush(); }
    }

    private static void CaptureVmLocked(string key, object vm)
    {
        var obj = new JsonObject();
        foreach (var p in vm.GetType().GetProperties())
        {
            var g = p.GetGetMethod();
            if (g == null || !g.IsPublic) continue;
            if (p.Name.EndsWith("Log")) continue;
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            object? val;
            try { val = g.Invoke(vm, null); } catch { continue; }
            try
            {
                if (IsScalar(t))
                {
                    obj[p.Name] = JsonSerializer.SerializeToNode(val, t);
                }
                else if (IsStringCollection(t) && val is IEnumerable en)
                {
                    var arr = new JsonArray();
                    foreach (var item in en) arr.Add(item?.ToString());
                    obj[p.Name] = arr;
                }
                else if (IsPocoCollection(t) && val != null)
                {
                    obj[p.Name] = JsonSerializer.SerializeToNode(val, p.PropertyType);
                }
                // 其余类型（图像/ICommand/复杂选中项等）自动跳过
            }
            catch { /* 单个属性失败不影响整体 */ }
        }
        Store[key] = obj;
    }

    private static void ApplyVmLocked(string key, object vm)
    {
        if (!Store.TryGetValue(key, out var obj)) return;
        foreach (var p in vm.GetType().GetProperties())
        {
            if (!obj.ContainsKey(p.Name)) continue;
            var g = p.GetGetMethod();
            if (g == null) continue;
            var setter = p.GetSetMethod();
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            try
            {
                if (IsScalar(t) && setter != null)
                {
                    var json = obj[p.Name]?.ToJsonString();
                    if (json != null)
                    {
                        var v = JsonSerializer.Deserialize(json, t);
                        if (v != null) setter.Invoke(vm, new[] { v });
                    }
                }
                else if (IsStringCollection(t) && obj[p.Name] is JsonArray sarr && g.Invoke(vm, null) is IList slist)
                {
                    slist.Clear();
                    foreach (var el in sarr) slist.Add(el?.ToString());
                }
                else if (IsPocoCollection(t) && obj[p.Name] is JsonArray parr && g.Invoke(vm, null) is IList plist)
                {
                    var itemType = t.GetGenericArguments()[0];
                    plist.Clear();
                    foreach (var el in parr)
                    {
                        var json = el?.ToJsonString();
                        if (json == null) continue;
                        var item = JsonSerializer.Deserialize(json, itemType);
                        if (item != null) plist.Add(item);
                    }
                }
            }
            catch { /* 单个属性失败不影响整体 */ }
        }
    }

    // ---------- 落盘 ----------

    private static void ScheduleFlush()
    {
        if (_flushTimer != null) return;
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        t.Tick += (_, _) => { t.Stop(); if (ReferenceEquals(_flushTimer, t)) _flushTimer = null; FlushNow(); };
        _flushTimer = t;
        t.Start();
    }

    public static void FlushNow()
    {
        JsonObject root;
        lock (_lock)
        {
            root = new JsonObject();
            foreach (var kv in _store ?? new Dictionary<string, JsonObject>())
                root[kv.Key] = kv.Value?.DeepClone()?.AsObject();
        }
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 目录不可写等，忽略 */ }
    }
}

// 温启志：18719361399  混淆: 温y启b志：e1z8g7f1m3u6k9w2q5
