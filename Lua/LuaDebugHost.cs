using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using NLua;

namespace NoCodeVision.Scripting;

/// <summary>
/// 脚本调试变量项（名称 / 类型 / 值 / 作用域）。
/// </summary>
public class VarItem
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
    public string Scope { get; set; } = "";
}

/// <summary>
/// 基于 NLua + debug.sethook 的 Lua 调试宿主。
/// 在后台线程执行脚本，通过行钩子在断点 / 单步 / 暂停处阻塞并让出 UI 线程，
/// 同时用 debug.getlocal / debug.getupvalue / _G 采集变量与调用栈。
/// </summary>
public sealed class LuaDebugHost : IDisposable
{
    private Lua? _lua;
    private readonly SynchronizationContext? _uiCtx;
    private readonly Action<string> _onOutput;
    private readonly Action<IList<VarItem>> _onVariables;
    private readonly Action<IList<string>> _onCallStack;
    private readonly Action<int> _onCurrentLine;
    private readonly Action<double> _onElapsed;
    private readonly Action<bool, bool> _onRunState; // running, paused
    private readonly Stopwatch _sw = new();

    private readonly ManualResetEvent _gate = new(true);
    private volatile bool _abort;
    private volatile bool _singleStep;
    private volatile bool _pauseRequested;
    private volatile HashSet<int> _breakpoints = new();
    private volatile bool _running;
    private int _lineOffset; // Preamble+hook 占用的行数，用于把绝对行号换算成用户脚本行号

    public bool IsRunning => _running;
    public bool IsPaused => _running && !_gate.WaitOne(0);

    public LuaDebugHost(
        SynchronizationContext? uiCtx,
        Action<string> onOutput,
        Action<IList<VarItem>> onVariables,
        Action<IList<string>> onCallStack,
        Action<int> onCurrentLine,
        Action<double> onElapsed,
        Action<bool, bool> onRunState)
    {
        _uiCtx = uiCtx;
        _onOutput = onOutput;
        _onVariables = onVariables;
        _onCallStack = onCallStack;
        _onCurrentLine = onCurrentLine;
        _onElapsed = onElapsed;
        _onRunState = onRunState;
    }

    public void SetBreakpoints(IEnumerable<int> lines)
        => _breakpoints = new HashSet<int>(lines);

    /// <summary>运行（若 stepMode 则在首行即暂停，进入单步模式）。</summary>
    public void Run(string script, bool stepMode)
    {
        _abort = false;
        _singleStep = stepMode;
        _pauseRequested = false;
        _gate.Set();
        _running = true;
        _sw.Restart();
        _uiCtx?.Post(_ => _onRunState(true, stepMode), null);
        ThreadPool.QueueUserWorkItem(_ => Execute(script));
    }

    /// <summary>单步：若未运行则从头以单步模式启动；若已暂停则执行下一行。</summary>
    public void Step(string script)
    {
        if (!_running)
        {
            Run(script, true);
            return;
        }
        _singleStep = true;
        _gate.Set();
    }

    public void Pause() => _pauseRequested = true;

    public void Resume()
    {
        _singleStep = false;
        _pauseRequested = false;
        _gate.Set();
    }

    public void Stop()
    {
        _abort = true;
        _gate.Set();
    }

    private void Execute(string script)
    {
        try
        {
            _lua = new Lua();
            SetupGlobals(_lua);

            string hookHead = "debug.sethook(function(event, line)\n"
                            + "  __on_line(line)\n"
                            + "  if __ABORT then error(\"已停止\") end\n"
                            + "end, \"l\")\n";
            // 把 Preamble + hook 头拼成前缀，统计其换行数即为用户脚本起始偏移。
            // debug.sethook 报告的是组合 chunk 的绝对行号，减去该偏移即得到编辑器中的用户行号，
            // 这样断点 / 单步 / 高亮才能与用户在 TextEditor 里看到的行对齐。
            string prefix = Preamble + "\n" + hookHead;
            _lineOffset = CountNewlines(prefix);
            string wrapped = prefix + script;

            _lua.DoString(wrapped);
            SafePost(() => _onOutput("[完成] 脚本执行结束，耗时 " + _sw.ElapsedMilliseconds + " ms"));
        }
        catch (Exception ex)
        {
            var msg = ex is NLua.Exceptions.LuaException ? ex.Message : ("[错误] " + ex.GetType().Name + ": " + ex.Message);
            SafePost(() => _onOutput(msg));
        }
        finally
        {
            _lua?.Dispose();
            _lua = null;
            _running = false;
            _sw.Stop();
            SafePost(() =>
            {
                _onRunState(false, false);
                _onCurrentLine(-1);
            });
        }
    }

    private void SetupGlobals(Lua lua)
    {
        lua["__ABORT"] = false;
        lua.RegisterFunction("__on_line", this, GetType().GetMethod(nameof(OnLine), BindingFlags.Public | BindingFlags.Instance)!);
        lua.RegisterFunction("__print", this, GetType().GetMethod(nameof(OnPrint), BindingFlags.Public | BindingFlags.Instance)!);
        lua.RegisterFunction("__log", this, GetType().GetMethod(nameof(OnLog), BindingFlags.Public | BindingFlags.Instance)!);
        lua.RegisterFunction("__vision_match", this, GetType().GetMethod(nameof(OnVisionMatch), BindingFlags.Public | BindingFlags.Instance)!);
        lua.RegisterFunction("__vision_grab", this, GetType().GetMethod(nameof(OnVisionGrab), BindingFlags.Public | BindingFlags.Instance)!);
        lua.RegisterFunction("__plc_write", this, GetType().GetMethod(nameof(OnPlcWrite), BindingFlags.Public | BindingFlags.Instance)!);
        lua.RegisterFunction("__plc_read", this, GetType().GetMethod(nameof(OnPlcRead), BindingFlags.Public | BindingFlags.Instance)!);
        lua.RegisterFunction("__sleep", this, GetType().GetMethod(nameof(OnSleep), BindingFlags.Public | BindingFlags.Instance)!);
        lua.RegisterFunction("__getinfo", this, GetType().GetMethod(nameof(OnGetInfo), BindingFlags.Public | BindingFlags.Instance)!);
    }

    // 供脚本使用的桩：print / log / sleep / vision / plc
    private const string Preamble = @"
function print(...)
  local t = {}
  for i, v in ipairs({...}) do t[i] = tostring(v) end
  __print(table.concat(t, '\t'))
end
function log(...)
  local t = {}
  for i, v in ipairs({...}) do t[i] = tostring(v) end
  __log(table.concat(t, '\t'))
end
function sleep(ms) __sleep(ms or 0) end
vision = {
  match = function(tpl) return __vision_match(tpl or '') end,
  find  = function(tpl) return __vision_match(tpl or '') end,
  grab  = function() return __vision_grab() end,
}
plc = {
  write = function(addr, val) __plc_write(addr or 0, val or 0) end,
  read  = function(addr) return __plc_read(addr or 0) end,
}
";

    // ---- C# 桩实现（被 Lua 通过 debug 调用）----
    public void OnLine(object line) => OnLineCore(Convert.ToInt32(line));
    public void OnPrint(object s) => SafePost(() => _onOutput(s == null ? "" : s.ToString()));
    public void OnLog(object s) => SafePost(() => _onOutput("[日志] " + (s == null ? "" : s.ToString())));
    public double OnVisionMatch(object tpl)
    {
        double score = 0.86 + ((tpl?.ToString()?.GetHashCode() ?? 0) % 13) / 100.0;
        SafePost(() => _onOutput("[vision.match] " + (tpl ?? "") + " -> " + score.ToString("F3")));
        return score;
    }
    public double OnVisionGrab() => 1.0;
    public void OnPlcWrite(object addr, object val)
        => SafePost(() => _onOutput("[plc.write] addr=" + addr + " value=" + val));
    public double OnPlcRead(object addr) => 0.0;
    public void OnSleep(object ms)
    {
        int m = Convert.ToInt32(ms);
        if (m > 0 && m <= 5000) Thread.Sleep(m);
    }
    public object? OnGetInfo(object lvl)
    {
        // 由 Lua 调用：return debug.getinfo 的结果表
        try
        {
            var f = _lua?.GetFunction("debug.getinfo");
            var r = f?.Call(Convert.ToInt32(lvl), "nSl") as object[];
            return r != null && r.Length > 0 ? r[0] : null;
        }
        catch { return null; }
    }

    private void OnLineCore(int absLine)
    {
        int line = absLine - _lineOffset;
        // Preamble / hook 安装行不是用户代码：跳过暂停与高亮，避免断点/单步误命中前导代码
        if (line < 1) return;

        SafePost(() =>
        {
            _onCurrentLine(line);
            _onElapsed(_sw.Elapsed.TotalMilliseconds);
        });

        if (_abort)
        {
            try { if (_lua != null) _lua["__ABORT"] = true; } catch { }
            return;
        }

        bool pause = _breakpoints.Contains(line) || _singleStep || _pauseRequested;
            if (pause)
            {
                _singleStep = false;
                _pauseRequested = false;
                var vars = CaptureVariables();
                var stack = CaptureCallStack(line);
            SafePost(() =>
            {
                _onVariables(vars);
                _onCallStack(stack);
                _onRunState(true, true);
            });
            _gate.WaitOne();
            if (!_abort)
                SafePost(() => _onRunState(true, false));
        }
    }

    private static int CountNewlines(string s)
    {
        int n = 0;
        foreach (var c in s) if (c == '\n') n++;
        return n;
    }

    private List<VarItem> CaptureVariables()
    {
        var list = new List<VarItem>();
        try
        {
            var getlocal = _lua?.GetFunction("debug.getlocal");
            if (getlocal != null)
            {
                for (int lvl = 1; lvl <= 12; lvl++)
                {
                    // 该层级没有栈帧则停止扫描
                    var first = getlocal.Call(lvl, 1) as object[];
                    if (first == null || first.Length == 0 || first[0] == null) break;
                    for (int i = 1; i < 200; i++)
                    {
                        var res = getlocal.Call(lvl, i) as object[];
                        if (res == null || res.Length == 0 || res[0] == null) break;
                        string name = res[0]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(name) || name.StartsWith("(")) continue; // 跳过 (*temporary*) 等
                        if (list.Any(v => v.Name == name)) continue; // 内层覆盖外层
                        object val = res.Length > 1 ? res[1] : null;
                        list.Add(new VarItem
                        {
                            Name = name,
                            Scope = "local",
                            Type = TypeName(val),
                            Value = FormatVal(val)
                        });
                    }
                }
            }

            // 全局变量
            if (_lua?.GetTable("_G") is LuaTable g)
            {
                foreach (DictionaryEntry e in g)
                {
                    string name = e.Key?.ToString() ?? "";
                    if (string.IsNullOrEmpty(name) || StandardLibs.Contains(name)) continue;
                    object val = e.Value;
                    if (val == null) continue;
                    if (list.Any(v => v.Name == name)) continue;
                    list.Add(new VarItem
                    {
                        Name = name,
                        Scope = "global",
                        Type = TypeName(val),
                        Value = FormatVal(val)
                    });
                }
            }
        }
        catch { /* 采集失败不阻塞运行 */ }
        return list;
    }

    private List<string> CaptureCallStack(int currentUserLine)
    {
        var list = new List<string>();
        try
        {
            bool topPlaced = false;
            for (int lvl = 1; lvl <= 12; lvl++)
            {
                var info = OnGetInfo(lvl) as LuaTable;
                if (info == null) break;
                string name = info["name"]?.ToString();
                string what = info["what"]?.ToString();
                // 跳过 C 函数（含通过 RegisterFunction 注册的 __on_line 等内部桩）
                if (what == "C") continue;
                if (name == "__on_line") continue;
                // 顶层帧使用已知的正确用户行号（getinfo 在 hook 内取到的 currentline 是 hook 调用点，不可靠）
                if (!topPlaced)
                {
                    list.Add($"[脚本]:{currentUserLine}");
                    topPlaced = true;
                    continue;
                }
                var cl = info["currentline"];
                string ln = cl == null ? "?" : (Convert.ToInt32(cl) - GetInfoOffset()).ToString();
                string src = info["source"]?.ToString() ?? "?";
                list.Add(string.IsNullOrEmpty(name) ? $"{src}:{ln}" : $"{src}:{ln} [{name}]");
            }
        }
        catch { }
        return list;
    }

    // getinfo 采集到的 currentline 仍是组合 chunk 的绝对行号，需要减去同样的偏移
    private int GetInfoOffset() => _lineOffset;

    private static string TypeName(object? v) => v switch
    {
        null => "nil",
        double => "number",
        int => "number",
        string => "string",
        bool => "boolean",
        LuaFunction => "function",
        LuaTable => "table",
        _ => v!.GetType().Name
    };

    private static string FormatVal(object? v)
    {
        if (v == null) return "nil";
        if (v is LuaFunction) return "function";
        if (v is LuaTable) return "table";
        if (v is double d) return d.ToString("G");
        string s = v.ToString() ?? "";
        return s.Length > 200 ? s.Substring(0, 200) + "…" : s;
    }

    private static readonly HashSet<string> StandardLibs = new()
    {
        "string","table","math","io","os","coroutine","debug","_G","_VERSION",
        "ipairs","pairs","pcall","xpcall","tonumber","tostring","type","next",
        "select","assert","error","print","log","sleep","unpack","rawequal",
        "rawget","rawset","rawlen","setmetatable","getmetatable","dofile",
        "load","loadfile","require","tostring","warn","collectgarbage",
        "vision","plc","__ABORT","__on_line","__print","__log","__vision_match",
        "__vision_grab","__plc_write","__plc_read","__sleep","__getinfo"
    };

    private void SafePost(Action a)
    {
        if (_uiCtx != null) _uiCtx.Post(_ => a(), null);
        else a();
    }

        /// <summary>语法校验：仅编译不执行，返回 null 表示通过，否则返回错误信息（行号已折算为用户脚本行）。</summary>
        public static string? CheckSyntax(string script)
        {
            if (string.IsNullOrWhiteSpace(script)) return "脚本为空，无法校验";
            try
            {
                using var lua = new Lua();
                // 包成函数体仅做语法解析：编译但不执行用户代码；顶层 return / 局部声明均合法
                lua.DoString("return function()\n" + script + "\nend");
                return null;
            }
            catch (Exception ex)
            {
                var msg = ex is NLua.Exceptions.LuaException ? ex.Message : (ex.GetType().Name + ": " + ex.Message);
                // 去掉包函数引入的行偏移（前缀占 1 行），折算回用户脚本行号
                msg = System.Text.RegularExpressions.Regex.Replace(msg, @":(\d+):", m =>
                {
                    int n = int.Parse(m.Groups[1].Value) - 1;
                    return ":" + (n < 1 ? 1 : n) + ":";
                });
                return msg;
            }
        }

    public void Dispose()
    {
        try { Stop(); } catch { }
        _gate.Dispose();
    }
}
