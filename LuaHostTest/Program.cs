using System;
using System.Collections.Generic;
using System.Threading;
using NoCodeVision.Scripting;

class Program
{
    static readonly List<string> Output = new();
    static List<VarItem> BpVars = new();
    static List<string> BpStack = new();
    static int BpLine = -1;
    static double BpElapsed = 0;
    static volatile bool PausedSeen = false;
    static volatile bool Done = false;

    static LuaDebugHost MakeHost()
    {
        return new LuaDebugHost(
            null,
            s => { lock (Output) Output.Add(s ?? ""); },
            vars => { if (!PausedSeen) { lock (BpVars) { BpVars = new List<VarItem>(vars); } } },
            stack => { if (!PausedSeen) { lock (BpStack) { BpStack = new List<string>(stack); } } },
            line => { if (!PausedSeen) BpLine = line; },
            elapsed => { if (!PausedSeen) BpElapsed = elapsed; },
            (running, paused) => { if (paused) PausedSeen = true; if (!running) Done = true; });
    }

    static void WaitPauseOrDone(int maxMs = 10000)
    {
        int waited = 0;
        while (!PausedSeen && !Done && waited < maxMs) { Thread.Sleep(50); waited += 50; }
    }

    static void WaitDone(int maxMs = 10000)
    {
        int waited = 0;
        while (!Done && waited < maxMs) { Thread.Sleep(50); waited += 50; }
    }

    static string Script() =>
        "-- 用户脚本开始\n" +
        "local count = 0\n" +
        "local name = \"widget\"\n" +
        "for i = 1, 3 do\n" +
        "  count = count + i\n" +
        "  print(\"i=\" .. i .. \" count=\" .. count)\n" +
        "end\n" +
        "local total = count * 2\n" +
        "print(\"total=\" .. total)\n";

    static int Main()
    {
        int fails = 0;
        var script = Script();

        // ---------- 测试1：断点 + 变量采集 ----------
        Console.WriteLine("== 测试1: 断点运行（第6行 count = count + i） ==");
        var host = MakeHost();
        host.SetBreakpoints(new[] { 6 });
        Output.Clear();
        host.Run(script, stepMode: false);
        WaitPauseOrDone();

        bool bpOk = PausedSeen && BpLine == 6;
        Console.WriteLine($"  命中断点行 BpLine={BpLine} (期望6) -> {(bpOk ? "PASS" : "FAIL")}");
        if (!bpOk) fails++;

        var countVar = BpVars.Find(v => v.Name == "count");
        var iVar = BpVars.Find(v => v.Name == "i");
        var nameVar = BpVars.Find(v => v.Name == "name");
        Console.WriteLine($"  局部变量: count={countVar?.Value}(期望1) i={iVar?.Value}(期望1) name={nameVar?.Value}(期望widget) type={countVar?.Type}");
        bool varOk = countVar?.Value == "1" && iVar?.Value == "1" && nameVar?.Value == "widget";
        Console.WriteLine($"  变量采集正确 -> {(varOk ? "PASS" : "FAIL")}");
        if (!varOk) fails++;
        Console.WriteLine($"  调用栈条数={BpStack.Count}, 首帧={(BpStack.Count > 0 ? BpStack[0] : "(空)")}");

        host.Resume();
        WaitDone();
        Console.WriteLine($"  恢复后运行结束 -> {(Done ? "PASS" : "FAIL")}");
        if (!Done) fails++;
        Console.WriteLine("  脚本输出:");
        lock (Output) foreach (var o in Output) Console.WriteLine("    " + o);

        // ---------- 测试2：单步执行 ----------
        Console.WriteLine("\n== 测试2: 单步执行 ==");
        PausedSeen = false; Done = false; BpLine = -1; BpVars.Clear(); BpStack.Clear();
        host = MakeHost();
        Output.Clear();
        host.Run(script, stepMode: true);
        WaitPauseOrDone();
        Console.WriteLine($"  单步首停行 BpLine={BpLine} (第一可执行行应为2) -> {(BpLine == 2 ? "PASS" : "INFO")}");
        int steps = 0;
        while (!Done && steps < 20)
        {
            host.Step(script);
            Thread.Sleep(60);
            steps++;
        }
        WaitDone();
        Console.WriteLine($"  单步推进至结束 -> {(Done ? "PASS" : "FAIL")} (步数={steps})");
        if (!Done) fails++;

        // ---------- 测试3：停止中断 ----------
        Console.WriteLine("\n== 测试3: 停止中断 ==");
        PausedSeen = false; Done = false;
        host = MakeHost();
        Output.Clear();
        host.Run(script, stepMode: false);
        Thread.Sleep(150);
        host.Stop();
        WaitDone();
        Console.WriteLine($"  停止后运行结束 -> {(Done ? "PASS" : "FAIL")}");
        if (!Done) fails++;

        Console.WriteLine($"\n========== 结果: {(fails == 0 ? "全部通过 ✅" : fails + " 项失败 ❌")} ==========");
        return fails == 0 ? 0 : 1;
    }
}
