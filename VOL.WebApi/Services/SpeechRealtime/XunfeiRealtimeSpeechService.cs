using System;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace VOL.WebApi.Services.SpeechRealtime
{
    public class XunfeiRealtimeSpeechService : IXunfeiRealtimeSpeechService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<XunfeiRealtimeSpeechService> _logger;

        public XunfeiRealtimeSpeechService(IConfiguration configuration, ILogger<XunfeiRealtimeSpeechService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private string WsUrl => _configuration["IFlytekIat:WsUrl"]?.Trim() ?? "wss://iat-api.xfyun.cn/v2/iat";
        private string AppId => _configuration["IFlytekIat:AppId"] ?? _configuration["IFlytekAsr:AppId"] ?? string.Empty;
        private string ApiKey => _configuration["IFlytekIat:APIKey"] ?? _configuration["IFlytekAsr:APIKey"] ?? string.Empty;
        private string ApiSecret => _configuration["IFlytekIat:APISecret"] ?? _configuration["IFlytekAsr:APISecret"] ?? string.Empty;
        private string Language => _configuration["IFlytekIat:Language"] ?? "zh_cn";
        private string Domain => _configuration["IFlytekIat:Domain"] ?? "iat";
        private string Accent => _configuration["IFlytekIat:Accent"] ?? "mandarin";
        private string AudioEncoding => _configuration["IFlytekIat:AudioEncoding"] ?? "raw";
        private string AudioFormat => _configuration["IFlytekIat:AudioFormat"] ?? "audio/L16;rate=16000";

        public async Task<IRealtimeSpeechSession> CreateSessionAsync(
            Func<string, Task> onResult,
            Func<string, Task> onLog,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(AppId) || string.IsNullOrEmpty(ApiKey) || string.IsNullOrEmpty(ApiSecret))
            {
                throw new InvalidOperationException("未配置 IFlytekIat:AppId/APIKey/APISecret");
            }

            var authUrl = BuildAuthUrl(WsUrl, ApiKey, ApiSecret);
            var ws = new ClientWebSocket();
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            await ws.ConnectAsync(new Uri(authUrl), cancellationToken);

            await onLog("已连接讯飞实时听写服务");
            return new XunfeiRealtimeSpeechSession(ws, AppId, Language, Domain, Accent, AudioEncoding, AudioFormat, _logger, onResult, onLog);
        }

        private static string BuildAuthUrl(string hostUrl, string apiKey, string apiSecret)
        {
            var uri = new Uri(hostUrl);
            var host = uri.Host;
            var path = uri.AbsolutePath;
            if (string.IsNullOrEmpty(path))
            {
                path = "/v2/iat";
            }

            var date = DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture);
            var requestLine = $"GET {path} HTTP/1.1";
            var signatureOrigin = $"host: {host}\ndate: {date}\n{requestLine}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
            var signatureSha = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureOrigin)));
            var authOrigin = $"api_key=\"{apiKey}\", algorithm=\"hmac-sha256\", headers=\"host date request-line\", signature=\"{signatureSha}\"";
            var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authOrigin));

            return $"{hostUrl}?authorization={Uri.EscapeDataString(authorization)}&date={Uri.EscapeDataString(date)}&host={Uri.EscapeDataString(host)}";
        }

        private class XunfeiRealtimeSpeechSession : IRealtimeSpeechSession
        {
            private readonly ClientWebSocket _xunfeiSocket;
            private readonly string _appId;
            private readonly string _language;
            private readonly string _domain;
            private readonly string _accent;
            private readonly string _audioEncoding;
            private readonly string _audioFormat;
            private readonly ILogger _logger;
            private readonly Func<string, Task> _onResult;
            private readonly Func<string, Task> _onLog;
            private readonly XunfeiResultAssembler _assembler = new XunfeiResultAssembler();
            private readonly CancellationTokenSource _receiverCts = new CancellationTokenSource();
            private readonly Task _receiverTask;
            private readonly TaskCompletionSource<bool> _firstAckTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private DateTime _lastSendAt = DateTime.MinValue;
            private bool _firstFrameSent;
            private int _audioFrameCount;
            private bool _completed;

            public XunfeiRealtimeSpeechSession(
                ClientWebSocket xunfeiSocket,
                string appId,
                string language,
                string domain,
                string accent,
                string audioEncoding,
                string audioFormat,
                ILogger logger,
                Func<string, Task> onResult,
                Func<string, Task> onLog)
            {
                _xunfeiSocket = xunfeiSocket;
                _appId = appId;
                _language = string.IsNullOrWhiteSpace(language) ? "zh_cn" : language;
                _domain = string.IsNullOrWhiteSpace(domain) ? "iat" : domain;
                _accent = string.IsNullOrWhiteSpace(accent) ? "mandarin" : accent;
                _audioEncoding = string.IsNullOrWhiteSpace(audioEncoding) ? "raw" : audioEncoding;
                _audioFormat = string.IsNullOrWhiteSpace(audioFormat) ? "audio/L16;rate=16000" : audioFormat;
                _logger = logger;
                _onResult = onResult;
                _onLog = onLog;
                _receiverTask = Task.Run(ReceiveLoopAsync);
            }

            public async Task SendAudioFrameAsync(byte[] pcmBytes, int length, CancellationToken cancellationToken)
            {
                if (_completed || length <= 0 || _xunfeiSocket.State != WebSocketState.Open)
                {
                    return;
                }

                JObject payload;
                if (!_firstFrameSent)
                {
                    payload = BuildFirstFramePayload(pcmBytes, length);
                    _firstFrameSent = true;
                }
                else
                {
                    // 首帧发送后等待讯飞返回第一帧响应，再发送续帧，避免 invalid handle。
                    var ackTask = _firstAckTcs.Task;
                    var completed = await Task.WhenAny(ackTask, Task.Delay(1500, cancellationToken));
                    if (completed != ackTask)
                    {
                        await _onLog("等待讯飞首帧确认超时，跳过当前续帧");
                        return;
                    }
                    payload = BuildContinueFramePayload(pcmBytes, length);
                }

                // 控制发送节奏，确保帧间隔接近 40ms。
                if (_lastSendAt != DateTime.MinValue)
                {
                    var elapsed = DateTime.UtcNow - _lastSendAt;
                    if (elapsed.TotalMilliseconds < 38)
                    {
                        await Task.Delay((int)(38 - elapsed.TotalMilliseconds), cancellationToken);
                    }
                }

                await SendJsonAsync(payload, cancellationToken);
                _lastSendAt = DateTime.UtcNow;
                _audioFrameCount += 1;
                if (!_firstAckTcs.Task.IsCompleted)
                {
                    _logger.LogInformation("讯飞首帧已发送: bytes={Length}, language={Language}, domain={Domain}, accent={Accent}, encoding={Encoding}, format={Format}",
                        length, _language, _domain, _accent, _audioEncoding, _audioFormat);
                }
                else if (_audioFrameCount <= 5)
                {
                    _logger.LogInformation("讯飞续帧已发送: frame={FrameIndex}, bytes={Length}", _audioFrameCount, length);
                }
            }

            public async Task CompleteAsync(CancellationToken cancellationToken)
            {
                if (_completed)
                {
                    return;
                }

                _completed = true;
                if (_xunfeiSocket.State == WebSocketState.Open && _firstFrameSent)
                {
                    await SendJsonAsync(new JObject
                    {
                        ["data"] = new JObject
                        {
                            ["status"] = 2,
                            ["format"] = _audioFormat,
                            ["encoding"] = _audioEncoding,
                            ["audio"] = ""
                        }
                    }, cancellationToken);
                    _logger.LogInformation("讯飞结束帧已发送: audioFrames={AudioFrameCount}", _audioFrameCount);
                }
                else if (_xunfeiSocket.State == WebSocketState.Open && !_firstFrameSent)
                {
                    _logger.LogWarning("未发送任何音频帧，跳过讯飞结束帧，直接关闭会话");
                }

                _receiverCts.CancelAfter(TimeSpan.FromSeconds(3));
                try
                {
                    await _receiverTask;
                }
                catch
                {
                    // ignore
                }

                if (_xunfeiSocket.State == WebSocketState.Open)
                {
                    await _xunfeiSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                }
                else if (_xunfeiSocket.State == WebSocketState.CloseReceived)
                {
                    await _xunfeiSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                }
            }

            public async ValueTask DisposeAsync()
            {
                _receiverCts.Cancel();
                try
                {
                    await _receiverTask;
                }
                catch
                {
                    // ignore
                }

                _xunfeiSocket.Dispose();
                _receiverCts.Dispose();
            }

            private JObject BuildFirstFramePayload(byte[] pcmBytes, int length)
            {
                var chunk = new byte[length];
                Buffer.BlockCopy(pcmBytes, 0, chunk, 0, length);
                return new JObject
                {
                    ["common"] = new JObject
                    {
                        ["app_id"] = _appId
                    },
                    ["business"] = new JObject
                    {
                        ["language"] = _language,
                        ["domain"] = _domain,
                        ["accent"] = _accent
                    },
                    ["data"] = new JObject
                    {
                        ["status"] = 0,
                        ["format"] = _audioFormat,
                        ["encoding"] = _audioEncoding,
                        ["audio"] = Convert.ToBase64String(chunk)
                    }
                };
            }

            private JObject BuildContinueFramePayload(byte[] pcmBytes, int length)
            {
                var chunk = new byte[length];
                Buffer.BlockCopy(pcmBytes, 0, chunk, 0, length);
                return new JObject
                {
                    ["data"] = new JObject
                    {
                        ["status"] = 1,
                        ["format"] = _audioFormat,
                        ["encoding"] = _audioEncoding,
                        ["audio"] = Convert.ToBase64String(chunk)
                    }
                };
            }

            private async Task SendJsonAsync(JObject payload, CancellationToken cancellationToken)
            {
                var json = payload.ToString(Newtonsoft.Json.Formatting.None);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _xunfeiSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
            }

            private async Task ReceiveLoopAsync()
            {
                var buffer = new byte[64 * 1024];
                while (!_receiverCts.IsCancellationRequested &&
                       (_xunfeiSocket.State == WebSocketState.Open || _xunfeiSocket.State == WebSocketState.CloseReceived))
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _xunfeiSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _receiverCts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    try
                    {
                        var root = JObject.Parse(json);
                        var code = root["code"]?.Value<int>() ?? -1;
                        if (code == 0)
                        {
                            _firstAckTcs.TrySetResult(true);
                        }
                    }
                    catch
                    {
                        // ignore parse errors here; assembler will handle
                    }

                    var (text, hasUpdate, error) = _assembler.AppendResponse(json);
                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogWarning("讯飞原始响应: {Json}", json);
                        _logger.LogWarning("讯飞实时听写异常: {Error}", error);
                        await _onLog(error);
                        continue;
                    }
                    if (hasUpdate)
                    {
                        await _onResult(text);
                    }
                }
            }
        }
    }
}
