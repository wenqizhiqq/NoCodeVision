using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace NoCodeVision.Comm;

/// <summary>
/// 真正的 Modbus-TCP 主站：自己建连接、发请求、等响应、验事务号、解异常码。
/// 不依赖界面也不依赖通讯配置——流程里的「Modbus 写/读」只要有 IP 就能直接连上发。
/// </summary>
public sealed class ModbusMaster : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly byte _unit;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private ushort _tid;

    public ModbusMaster(string host, int port = 502, int unitId = 1)
    {
        _host = host;
        _port = port;
        _unit = (byte)Math.Clamp(unitId, 0, 255);
    }

    public string Key => $"{_host}:{_port}#{_unit}";
    public bool IsOpen => _tcp?.Connected == true && _stream != null;
    public string LastError { get; private set; } = "";

    // ---------------- 连接池：同一个地址复用一条连接 ----------------
    private static readonly Dictionary<string, ModbusMaster> Pool = new();
    private static readonly object PoolGate = new();

    /// <summary>取一条已连接的会话；没连过就连上。</summary>
    public static ModbusMaster? Get(string host, int port, int unitId, out string error, int timeoutMs = 3000)
    {
        error = "";
        var m = new ModbusMaster(host, port, unitId);
        lock (PoolGate)
        {
            if (Pool.TryGetValue(m.Key, out var old) && old.IsOpen) return old;
            try
            {
                m.Connect(timeoutMs);
                Pool[m.Key] = m;
                return m;
            }
            catch (Exception ex)
            {
                error = $"连不上 Modbus 从站 {m.Key}：{ex.Message}";
                m.Dispose();
                return null;
            }
        }
    }

    /// <summary>断开并忘掉某条会话（PLC 重启后重连用）。</summary>
    public static void Drop(string key)
    {
        lock (PoolGate)
        {
            if (Pool.TryGetValue(key, out var m)) { m.Dispose(); Pool.Remove(key); }
        }
    }

    public static void DropAll()
    {
        lock (PoolGate)
        {
            foreach (var m in Pool.Values) m.Dispose();
            Pool.Clear();
        }
    }

    // ---------------- 连接 ----------------
    public void Connect(int timeoutMs = 3000)
    {
        _tcp = new TcpClient { NoDelay = true };
        var ar = _tcp.BeginConnect(_host, _port, null, null);
        if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) { _tcp.Close(); throw new TimeoutException($"连接 {_host}:{_port} 超时"); }
        _tcp.EndConnect(ar);
        _stream = _tcp.GetStream();
        _stream.ReadTimeout = timeoutMs;
        _stream.WriteTimeout = timeoutMs;
        LastError = "";
    }

    public void Close()
    {
        try { _stream?.Close(); } catch { }
        try { _tcp?.Close(); } catch { }
        _stream = null; _tcp = null;
    }

    public void Dispose() => Close();

    // ---------------- 一次事务：发请求 + 等响应 ----------------
    private byte[]? Transaction(byte func, byte[] pdu, int expectPduLen, out string error)
    {
        error = "";
        if (_stream == null) { error = "没连上"; return null; }
        ushort tid = ++_tid;
        try
        {
            var frame = new byte[7 + pdu.Length];
            frame[0] = (byte)(tid >> 8); frame[1] = (byte)(tid & 0xFF);
            frame[2] = 0; frame[3] = 0;
            int len = 1 + pdu.Length;
            frame[4] = (byte)(len >> 8); frame[5] = (byte)(len & 0xFF);
            frame[6] = _unit;
            Array.Copy(pdu, 0, frame, 7, pdu.Length);
            _stream.Write(frame, 0, frame.Length);
            _stream.Flush();

            var head = new byte[7];
            if (!ReadExact(head, 7)) { error = "等响应超时（PLC 没回）"; return null; }
            if (((head[0] << 8) | head[1]) != tid) { error = "事务号对不上（响应乱序）"; return null; }
            int rlen = (head[4] << 8) | head[5];
            int bodyLen = Math.Max(0, rlen - 1);
            var body = new byte[bodyLen];
            if (bodyLen > 0 && !ReadExact(body, bodyLen)) { error = "响应体读不全"; return null; }
            if (body.Length == 0) { error = "响应为空"; return null; }
            if (body[0] == (func | 0x80))
            {
                byte code = body.Length > 1 ? body[1] : (byte)0;
                error = $"PLC 报异常：{ExceptionText(code)}（功能码 {func:X2}）";
                return null;
            }
            if (body[0] != func) { error = $"响应功能码不对：{body[0]:X2}"; return null; }
            return body;
        }
        catch (Exception ex)
        {
            error = "通讯出错：" + ex.Message;
            Close();
            return null;
        }
    }

    private bool ReadExact(byte[] buf, int count)
    {
        int got = 0;
        while (got < count)
        {
            int n;
            try { n = _stream!.Read(buf, got, count - got); }
            catch (IOException ex) { LastError = ex.Message; return false; }
            if (n <= 0) return false;
            got += n;
        }
        return true;
    }

    private static string ExceptionText(byte code) => code switch
    {
        0x01 => "非法功能码",
        0x02 => "非法数据地址",
        0x03 => "非法数据值",
        0x04 => "从站设备故障",
        0x05 => "确认（正在处理）",
        0x06 => "从站忙",
        0x08 => "存储奇偶校验错",
        0x0A => "网关路径不可用",
        0x0B => "网关目标无响应",
        _ => $"错误码 {code}",
    };

    // ---------------- 读 ----------------
    public ushort[]? ReadRegisters(ushort addr, ushort count, bool input, out string error)
    {
        var func = input ? (byte)0x04 : (byte)0x03;
        var pdu = new byte[] { func, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(count >> 8), (byte)(count & 0xFF) };
        var body = Transaction(func, pdu, 2 + count * 2, out error);
        if (body == null) return null;
        int bc = body[1];
        var vals = new List<ushort>();
        for (int i = 2; i + 1 < body.Length && vals.Count < count; i += 2)
            vals.Add((ushort)((body[i] << 8) | body[i + 1]));
        if (vals.Count == 0) { error = "响应里没有数据"; return null; }
        return vals.ToArray();
    }

    public bool[]? ReadCoils(ushort addr, ushort count, out string error)
    {
        var pdu = new byte[] { 0x01, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(count >> 8), (byte)(count & 0xFF) };
        var body = Transaction(0x01, pdu, 2 + (count + 7) / 8, out error);
        if (body == null) return null;
        int bc = body[1];
        var bits = new bool[count];
        for (int i = 0; i < count && 2 + i / 8 < body.Length; i++)
            bits[i] = (body[2 + i / 8] & (1 << (i % 8))) != 0;
        return bits;
    }

    // ---------------- 写 ----------------
    public bool WriteRegister(ushort addr, ushort value, out string error)
    {
        var pdu = new byte[] { 0x06, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(value >> 8), (byte)(value & 0xFF) };
        var body = Transaction(0x06, pdu, 5, out error);
        return body != null;                      // 回显地址+值即成功
    }

    public bool WriteRegisters(ushort addr, ushort[] values, out string error)
    {
        var pdu = new byte[6 + values.Length * 2];
        pdu[0] = 0x10;
        pdu[1] = (byte)(addr >> 8); pdu[2] = (byte)(addr & 0xFF);
        pdu[3] = (byte)(values.Length >> 8); pdu[4] = (byte)(values.Length & 0xFF);
        pdu[5] = (byte)(values.Length * 2);
        for (int i = 0; i < values.Length; i++) { pdu[6 + i * 2] = (byte)(values[i] >> 8); pdu[7 + i * 2] = (byte)(values[i] & 0xFF); }
        var body = Transaction(0x10, pdu, 5, out error);
        return body != null;
    }

    public bool WriteCoil(ushort addr, bool on, out string error)
    {
        var pdu = new byte[] { 0x05, (byte)(addr >> 8), (byte)(addr & 0xFF), (byte)(on ? 0xFF : 0x00), 0x00 };   // 通=0xFF00，断=0x0000
        var body = Transaction(0x05, pdu, 5, out error);
        return body != null;
    }

    /// <summary>写 32 位浮点（两个寄存器，高字在前）。</summary>
    public bool WriteFloat(ushort addr, float value, out string error)
    {
        var raw = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(raw);
        var regs = new ushort[] { (ushort)((raw[0] << 8) | raw[1]), (ushort)((raw[2] << 8) | raw[3]) };
        return WriteRegisters(addr, regs, out error);
    }
}