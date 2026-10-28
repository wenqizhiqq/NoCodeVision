// 裸 TCP 客户端：按 IP:端口 复用一条连接，真连真发；连不上/发不出去都给一句人话，供「TCP 发送」算子和连接测试用。
// 实现：温启志

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NoCodeVision.Comm;

public static class TcpSender
{
    static readonly ConcurrentDictionary<string, TcpClient> _pool = new();

    static string Key(string host, int port) => $"{host}:{port}";

    /// <summary>发一段文本。返回 true 表示字节已写进对端的接收缓冲；error 非空表示失败原因。</summary>
    public static bool Send(string host, int port, string text, int timeoutMs, out string error, out int bytes)
    {
        error = ""; bytes = 0;
        if (string.IsNullOrWhiteSpace(host)) { error = "没填 IP 地址"; return false; }
        if (port <= 0 || port > 65535) { error = $"端口 {port} 不对（1~65535）"; return false; }
        var payload = Encoding.UTF8.GetBytes(text ?? "");
        var sw = Stopwatch.StartNew();
        try
        {
            if (!GetClient(host, port, timeoutMs, out var client, out error)) return false;
            var stream = client.GetStream();
            var w = stream.WriteAsync(payload, 0, payload.Length);
            if (!w.Wait(Math.Max(200, timeoutMs)))
            {
                Drop(host, port);
                error = $"发送超时（等了 {Math.Max(200, timeoutMs)} 毫秒，对端不收）";
                return false;
            }
            stream.Flush();
            bytes = payload.Length;
            LastMs = sw.ElapsedMilliseconds;
            return true;
        }
        catch (AggregateException aex)
        {
            Drop(host, port);
            error = Describe(aex.GetBaseException());
            return false;
        }
        catch (Exception ex)
        {
            Drop(host, port);
            error = Describe(ex);
            return false;
        }
    }

    /// <summary>只测连不连得上（不动已复用的连接）。</summary>
    public static bool Test(string host, int port, int timeoutMs, out string error)
    {
        error = "";
        var sw = Stopwatch.StartNew();
        try
        {
            using var c = new TcpClient { NoDelay = true };
            var t = c.ConnectAsync(host, port);
            if (!t.Wait(Math.Max(200, timeoutMs))) { error = $"连接超时（{Math.Max(200, timeoutMs)} 毫秒内 {host}:{port} 没应答）"; return false; }
            if (!c.Connected) { error = "连上了但马上断了"; return false; }
            error = "";   // 成功：耗时写进 LastMs
            LastMs = sw.ElapsedMilliseconds;
            return true;
        }
        catch (AggregateException aex) { error = Describe(aex.GetBaseException()); return false; }
        catch (Exception ex) { error = Describe(ex); return false; }
    }

    static bool GetClient(string host, int port, int timeoutMs, out TcpClient client, out string error)
    {
        error = "";
        var key = Key(host, port);
        if (_pool.TryGetValue(key, out var old) && old.Connected)
        {
            client = old;
            return true;
        }
        if (old != null) { try { old.Close(); } catch { } _pool.TryRemove(key, out _); }

        var c = new TcpClient { NoDelay = true };
        try
        {
            var t = c.ConnectAsync(host, port);
            if (!t.Wait(Math.Max(200, timeoutMs)))
            {
                c.Close();
                error = $"连接超时（{Math.Max(200, timeoutMs)} 毫秒内 {host}:{port} 没应答）";
                client = c; return false;
            }
        }
        catch (Exception ex)
        {
            c.Close();
            error = Describe(ex.GetBaseException());
            client = c; return false;
        }
        _pool[key] = c;
        client = c;
        return true;
    }

    static string Describe(Exception ex) => ex switch
    {
        SocketException se => se.SocketErrorCode switch
        {
            SocketError.ConnectionRefused => "对方拒绝连接（端口没开，或服务端没起来）",
            SocketError.TimedOut or SocketError.HostNotFound or SocketError.HostUnreachable => "地址不通或 IP 写错了",
            SocketError.NetworkUnreachable => "网络不通（本机和对端不在一个网段？）",
            _ => $"网络错误：{se.Message}",
        },
        _ => ex.Message,
    };

    /// <summary>最近一次成功发送/测试的耗时（毫秒），界面那行状态用。</summary>
    public static long LastMs { get; private set; }

    public static void Drop(string host, int port)
    {
        var key = Key(host, port);
        if (_pool.TryRemove(key, out var c)) { try { c.Close(); } catch { } }
    }

    public static void DropAll()
    {
        foreach (var k in _pool.Keys) Drop(k.Split(':')[0], int.TryParse(k.Split(':')[1], out var p) ? p : 0);
    }
}
