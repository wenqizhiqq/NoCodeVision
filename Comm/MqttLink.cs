using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using NoCodeVision.ViewModels;

namespace NoCodeVision.Comm;

public sealed class MqttLink : ICommLink
{
    private IMqttClient? _client;
    private CommConfigItem? _cfg;
    public bool IsOpen { get; private set; }
    public event Action<string>? Log;
    public event Action<string>? DataReceived;

    public async Task ConnectAsync(CommConfigItem c, CancellationToken ct = default)
    {
        _cfg = c;
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMsg;
        _client.DisconnectedAsync += e => { IsOpen = false; Log?.Invoke("[MQTT] 断开"); return Task.CompletedTask; };

        string host = string.IsNullOrWhiteSpace(c.NetIp) ? "localhost" : c.NetIp;
        int port = int.TryParse(c.NetPort, out var p) && p > 0 ? p : 1883;
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(string.IsNullOrWhiteSpace(c.ClientId) ? "NoCodeVision_" + Guid.NewGuid().ToString("N").Substring(0, 8) : c.ClientId);
        if (!string.IsNullOrWhiteSpace(c.Username)) builder = builder.WithCredentials(c.Username, c.Password);
        builder = builder.WithCleanSession();

        try
        {
            await _client.ConnectAsync(builder.Build(), ct);
            IsOpen = _client.IsConnected;
            Log?.Invoke($"[MQTT] 已连接 {host}:{port}");
            if (!string.IsNullOrWhiteSpace(c.SubTopics))
            {
                foreach (var t in c.SubTopics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    await _client.SubscribeAsync(t);
                Log?.Invoke($"[MQTT] 已订阅 {c.SubTopics}");
            }
        }
        catch (Exception ex) { IsOpen = false; Log?.Invoke("[MQTT 连接错误] " + ex.Message); }
    }

    private Task OnMsg(MqttApplicationMessageReceivedEventArgs e)
    {
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
        DataReceived?.Invoke($"[{e.ApplicationMessage.Topic}] {payload}");
        return Task.CompletedTask;
    }

    public async Task SendAsync(string text)
    {
        if (_client == null || !_client.IsConnected || _cfg == null) return;
        var topic = string.IsNullOrWhiteSpace(_cfg.Topic) ? "NoCodeVision/out" : _cfg.Topic;
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(text))
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await _client.PublishAsync(msg);
        Log?.Invoke($"[发布] -> {topic} : {text}");
    }

    public async Task DisconnectAsync()
    {
        if (_client != null && _client.IsConnected)
        {
            try { await _client.DisconnectAsync(); } catch { }
        }
        _client?.Dispose(); _client = null; IsOpen = false;
        await Task.CompletedTask;
    }

    public void Dispose() { try { DisconnectAsync().GetAwaiter().GetResult(); } catch { } }
}
