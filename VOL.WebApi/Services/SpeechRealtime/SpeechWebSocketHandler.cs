using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace VOL.WebApi.Services.SpeechRealtime
{
    public class SpeechWebSocketHandler
    {
        private readonly ISpeechWebSocketConnectionManager _connectionManager;
        private readonly IXunfeiRealtimeSpeechService _speechService;
        private readonly ILogger<SpeechWebSocketHandler> _logger;

        public SpeechWebSocketHandler(
            ISpeechWebSocketConnectionManager connectionManager,
            IXunfeiRealtimeSpeechService speechService,
            ILogger<SpeechWebSocketHandler> logger)
        {
            _connectionManager = connectionManager;
            _speechService = speechService;
            _logger = logger;
        }

        public async Task HandleAsync(WebSocket clientSocket, CancellationToken cancellationToken)
        {
            var connectionId = _connectionManager.Add(clientSocket);
            _logger.LogInformation("语音连接建立: {ConnectionId}", connectionId);

            try
            {
                await using var session = await _speechService.CreateSessionAsync(
                    onResult: async text =>
                    {
                        await SendJsonAsync(clientSocket, new { type = "result", text }, cancellationToken);
                    },
                    onLog: async log =>
                    {
                        await SendJsonAsync(clientSocket, new { type = "log", message = log }, cancellationToken);
                    },
                    cancellationToken: cancellationToken);

                var buffer = new byte[8 * 1024];
                while (!cancellationToken.IsCancellationRequested && clientSocket.State == WebSocketState.Open)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await clientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }
                        if (result.Count > 0)
                        {
                            ms.Write(buffer, 0, result.Count);
                        }
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await session.CompleteAsync(cancellationToken);
                        break;
                    }

                    var body = ms.ToArray();
                    if (body.Length == 0)
                    {
                        continue;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        _logger.LogInformation("收到客户端音频帧: bytes={Bytes}", body.Length);
                        await session.SendAudioFrameAsync(body, body.Length, cancellationToken);
                        continue;
                    }

                    var message = Encoding.UTF8.GetString(body);
                    if (message.IndexOf("\"type\":\"stop\"", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        await session.CompleteAsync(cancellationToken);
                        await SendJsonAsync(clientSocket, new { type = "stopped" }, cancellationToken);
                        continue;
                    }

                    if (message.IndexOf("ping", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        await SendJsonAsync(clientSocket, new { type = "pong", ts = DateTimeOffset.UtcNow }, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("语音连接取消: {ConnectionId}", connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "语音连接处理异常: {ConnectionId}", connectionId);
                await SendJsonAsync(clientSocket, new { type = "error", message = ex.Message }, CancellationToken.None);
            }
            finally
            {
                if (_connectionManager.Remove(connectionId, out var socket))
                {
                    await SafeCloseAsync(socket);
                }
                _logger.LogInformation("语音连接关闭: {ConnectionId}", connectionId);
            }
        }

        private static async Task SendJsonAsync(WebSocket socket, object payload, CancellationToken cancellationToken)
        {
            if (socket == null || socket.State != WebSocketState.Open)
            {
                return;
            }

            var json = JsonConvert.SerializeObject(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private static async Task SafeCloseAsync(WebSocket socket)
        {
            try
            {
                if (socket != null && (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived))
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None);
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
