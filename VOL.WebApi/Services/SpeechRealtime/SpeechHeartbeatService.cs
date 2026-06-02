using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace VOL.WebApi.Services.SpeechRealtime
{
    public class SpeechHeartbeatService : BackgroundService
    {
        private readonly ISpeechWebSocketConnectionManager _connectionManager;
        private readonly ILogger<SpeechHeartbeatService> _logger;

        public SpeechHeartbeatService(
            ISpeechWebSocketConnectionManager connectionManager,
            ILogger<SpeechHeartbeatService> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                var snapshot = _connectionManager.Connections.ToArray();
                foreach (var item in snapshot)
                {
                    var connectionId = item.Key;
                    var socket = item.Value;
                    if (socket == null || socket.State != WebSocketState.Open)
                    {
                        continue;
                    }

                    try
                    {
                        var payload = JsonConvert.SerializeObject(new { type = "heartbeat", ts = DateTimeOffset.UtcNow });
                        var bytes = Encoding.UTF8.GetBytes(payload);
                        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "语音心跳发送失败，移除连接: {ConnectionId}", connectionId);
                        if (_connectionManager.Remove(connectionId, out var removed) && removed != null)
                        {
                            removed.Abort();
                        }
                    }
                }
            }
        }
    }
}
