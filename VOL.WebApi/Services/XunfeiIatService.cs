using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace VOL.WebApi.Services
{
    /// <summary>
    /// 讯飞语音听写（流式版）WebSocket 代理，供小程序上传短音频后转文字。
    /// 文档：https://www.xfyun.cn/doc/asr/voicedictation/API.html
    /// </summary>
    public class XunfeiIatService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<XunfeiIatService> _logger;
        private const string DefaultWsUrl = "wss://iat-api.xfyun.cn/v2/iat";
        private const int PcmFrameSize = 1280;

        public XunfeiIatService(IConfiguration configuration, ILogger<XunfeiIatService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private string WsUrl => _configuration["IFlytekIat:WsUrl"]?.Trim() ?? DefaultWsUrl;
        private string AppId => _configuration["IFlytekIat:AppId"] ?? _configuration["IFlytekAsr:AppId"] ?? "";
        private string ApiKey => _configuration["IFlytekIat:APIKey"] ?? _configuration["IFlytekAsr:APIKey"] ?? "";
        private string ApiSecret => _configuration["IFlytekIat:APISecret"] ?? _configuration["IFlytekAsr:APISecret"] ?? "";

        /// <summary>
        /// 将音频流转写为文本。支持：16k 单声道 PCM/WAV（raw）、MP3（lame，中文普通话/英文）。
        /// </summary>
        public async Task<(string text, string message)> TranscribeAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default)
        {
            if (audioStream == null || !audioStream.CanRead)
                return ("", "音频流无效");
            if (string.IsNullOrEmpty(AppId) || string.IsNullOrEmpty(ApiKey) || string.IsNullOrEmpty(ApiSecret))
                return ("", "未配置讯飞语音听写(IFlytekIat 或 IFlytekAsr 中的 AppId/APIKey/APISecret)");

            var debugLog = string.Equals(_configuration["IFlytekIat:Debug"], "true", StringComparison.OrdinalIgnoreCase);

            using var ms = new MemoryStream();
            await audioStream.CopyToAsync(ms, cancellationToken);
            var audioBytes = ms.ToArray();
            if (audioBytes.Length == 0)
                return ("", "音频为空");

            var ext = (Path.GetExtension(fileName) ?? "").ToLowerInvariant();
            var isM4aOrMp4 = ext == ".m4a" || ext == ".mp4" || ext == ".aac" || LooksLikeMp4M4a(audioBytes);
            if (isM4aOrMp4)
            {
                return ("", "当前语音格式为 m4a/aac，暂不支持直接识别，请改用 mp3 或 wav(16k 单声道)");
            }
            // WAV 头解析：校验录音是否真的为 16k/16bit/单声道
            var isRiffWav = audioBytes.Length > 12
                            && audioBytes[0] == 'R' && audioBytes[1] == 'I' && audioBytes[2] == 'F' && audioBytes[3] == 'F'
                            && audioBytes[8] == 'W' && audioBytes[9] == 'A' && audioBytes[10] == 'V' && audioBytes[11] == 'E';
            if (ext == ".wav" || isRiffWav)
            {
                if (TryExtractWavMeta(audioBytes, out var sampleRate, out var channels, out var bitsPerSample))
                {
                    _logger.LogInformation("讯飞听写 WAV 头: sampleRate={SampleRate}, channels={Channels}, bits={Bits}", sampleRate, channels, bitsPerSample);
                    if (sampleRate != 16000)
                        return ("", $"WAV 采样率为 {sampleRate}，讯飞流式 raw 目前仅支持 16000");
                    if (channels != 1)
                        return ("", $"WAV 声道数为 {channels}，讯飞流式 raw 目前仅支持单声道(1)");
                    if (bitsPerSample != 16)
                        return ("", $"WAV 位深为 {bitsPerSample}，讯飞流式 raw 目前仅支持 16bit");
                }
            }
            var (pcmOrMp3, encoding, format) = PrepareAudio(audioBytes, ext);
            if (pcmOrMp3 == null || pcmOrMp3.Length == 0)
                return ("", "无法解析音频格式，请上传 WAV/PCM(16k 16bit 单声道) 或 MP3");
            if (pcmOrMp3.Length < 1024)
                return ("", "录音时长过短或音频数据异常，请重新录制后再试");

            _logger.LogInformation("讯飞听写: 文件={FileName}, 格式={Encoding}, 字节数={Size}", fileName, encoding, pcmOrMp3.Length);

            var authUrl = BuildAuthUrl(WsUrl, ApiKey, ApiSecret);

            using var ws = new ClientWebSocket();
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            await ws.ConnectAsync(new Uri(authUrl), cancellationToken);

            try
            {
                var textBuilder = new StringBuilder();

                const int mp3ChunkSize = 4096;
                var chunkSize = encoding == "raw" ? PcmFrameSize : mp3ChunkSize;
                for (var offset = 0; offset < pcmOrMp3.Length; offset += chunkSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ws.State != WebSocketState.Open)
                        break;
                    if (offset > 0)
                        await Task.Delay(40, cancellationToken); // 讯飞建议每帧间隔约 40ms
                    var len = Math.Min(chunkSize, pcmOrMp3.Length - offset);
                    var chunk = new byte[len];
                    Array.Copy(pcmOrMp3, offset, chunk, 0, len);
                    var isFirst = offset == 0;
                    var json = isFirst
                        ? BuildFrameJson(true, 0, format, encoding, chunk)
                        : BuildFrameJson(false, 1, format, encoding, chunk);
                    await SendTextAsync(ws, json, cancellationToken);
                    var err = await ReceiveAndAppendAsync(ws, textBuilder, debugLog, cancellationToken);
                    if (err != null)
                        return ("", err);
                }

                if (ws.State == WebSocketState.Open)
                {
                    await SendTextAsync(ws, "{\"data\":{\"status\":2}}", cancellationToken);
                    // 持续接收直到服务端关闭连接，避免漏掉最后的识别结果
                    while (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                    {
                        var endErr = await ReceiveAndAppendAsync(ws, textBuilder, debugLog, cancellationToken);
                        if (endErr != null)
                            return ("", endErr);
                        if (ws.State != WebSocketState.Open)
                            break;
                    }
                }

                var resultText = textBuilder.ToString().Trim();
                if (string.IsNullOrEmpty(resultText))
                    _logger.LogWarning("讯飞听写返回空结果，请检查: 1)音频是否16k/8k单声道 2)控制台是否已添加「语音听写流式版」服务 3)开启 IFlytekIat:Debug 查看原始响应");
                return (resultText, "");
            }
            finally
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                    else if (ws.State == WebSocketState.CloseReceived)
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                }
                catch { /* 忽略关闭时的异常 */ }
            }
        }

        private static (byte[] data, string encoding, string format) PrepareAudio(byte[] raw, string ext)
        {
            const string fmt16k = "audio/L16;rate=16000";

            if (ext == ".mp3" || ext == ".mpeg")
                return (raw, "lame", fmt16k);

            if (ext == ".pcm")
                return (raw, "raw", fmt16k);

            // WAV / RIFF
            if (ext == ".wav" || (raw.Length > 12 && raw[0] == 'R' && raw[1] == 'I' && raw[2] == 'F' && raw[3] == 'F'))
            {
                var pcm = ExtractWavPcm(raw);
                return pcm.Length > 0 ? (pcm, "raw", fmt16k) : (null, null, null);
            }

            // 无扩展名或未知扩展名：按文件头识别 MP3 / WAV
            if (raw.Length >= 4)
            {
                if (raw[0] == 0xFF && (raw[1] & 0xE0) == 0xE0) return (raw, "lame", fmt16k); // MP3 frame
                if (raw[0] == 'I' && raw[1] == 'D' && raw[2] == '3') return (raw, "lame", fmt16k); // ID3
                if (raw[0] == 'R' && raw[1] == 'I' && raw[2] == 'F' && raw[3] == 'F')
                {
                    var pcm = ExtractWavPcm(raw);
                    if (pcm.Length > 0) return (pcm, "raw", fmt16k);
                }
            }

            return (null, null, null);
        }

        private static bool LooksLikeMp4M4a(byte[] raw)
        {
            if (raw == null || raw.Length < 12) return false;
            // MP4/M4A 常见 ftyp box：xxxx ftyp...
            for (var i = 0; i <= Math.Min(raw.Length - 8, 64); i++)
            {
                if (raw[i] == 'f' && raw[i + 1] == 't' && raw[i + 2] == 'y' && raw[i + 3] == 'p')
                    return true;
            }
            return false;
        }

        private static byte[] ExtractWavPcm(byte[] wav)
        {
            if (wav.Length < 44)
                return Array.Empty<byte>();
            var i = 12;
            while (i + 8 <= wav.Length)
            {
                var id = Encoding.ASCII.GetString(wav, i, 4);
                var size = BitConverter.ToInt32(wav, i + 4);
                if (id == "data")
                {
                    if (i + 8 + size > wav.Length)
                        size = wav.Length - i - 8;
                    if (size <= 0)
                        return Array.Empty<byte>();
                    var pcm = new byte[size];
                    Array.Copy(wav, i + 8, pcm, 0, size);
                    return pcm;
                }
                i += 8 + size;
                if (size < 0)
                    break;
            }
            if (wav.Length > 44)
            {
                var fallback = new byte[wav.Length - 44];
                Array.Copy(wav, 44, fallback, 0, fallback.Length);
                return fallback;
            }
            return Array.Empty<byte>();
        }

        private static bool TryExtractWavMeta(byte[] wav, out int sampleRate, out int channels, out int bitsPerSample)
        {
            sampleRate = 0;
            channels = 0;
            bitsPerSample = 0;
            if (wav == null || wav.Length < 44)
                return false;
            if (!(wav[0] == 'R' && wav[1] == 'I' && wav[2] == 'F' && wav[3] == 'F' &&
                  wav[8] == 'W' && wav[9] == 'A' && wav[10] == 'V' && wav[11] == 'E'))
                return false;

            // 解析 RIFF/WAVE 的 chunk
            var offset = 12;
            while (offset + 8 <= wav.Length)
            {
                var id = Encoding.ASCII.GetString(wav, offset, 4);
                var size = BitConverter.ToInt32(wav, offset + 4);
                if (size < 0) break;
                var dataOffset = offset + 8;

                if (id == "fmt ")
                {
                    // fmt chunk: AudioFormat(2), NumChannels(2), SampleRate(4), ByteRate(4), BlockAlign(2), BitsPerSample(2)
                    if (dataOffset + 16 <= wav.Length)
                    {
                        channels = BitConverter.ToUInt16(wav, dataOffset + 2);
                        sampleRate = BitConverter.ToInt32(wav, dataOffset + 4);
                        bitsPerSample = BitConverter.ToUInt16(wav, dataOffset + 14);
                        return true;
                    }
                    return false;
                }

                offset = dataOffset + size;
                // chunk size 为奇数时需要 1 字节对齐
                if ((size & 1) == 1) offset += 1;
            }

            return false;
        }

        private static string BuildAuthUrl(string hostUrl, string apiKey, string apiSecret)
        {
            var uri = new Uri(hostUrl);
            var host = uri.Host;
            var path = uri.AbsolutePath;
            if (string.IsNullOrEmpty(path))
                path = "/v2/iat";

            var date = DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture);
            var requestLine = $"GET {path} HTTP/1.1";
            var signatureOrigin = $"host: {host}\ndate: {date}\n{requestLine}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
            var signatureSha = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureOrigin)));
            var authOrigin = $"api_key=\"{apiKey}\", algorithm=\"hmac-sha256\", headers=\"host date request-line\", signature=\"{signatureSha}\"";
            var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authOrigin));

            return $"{hostUrl}?authorization={Uri.EscapeDataString(authorization)}&date={Uri.EscapeDataString(date)}&host={Uri.EscapeDataString(host)}";
        }

        private string BuildFrameJson(bool withCommonBusiness, int status, string format, string encoding, byte[] audioChunk)
        {
            var audioB64 = audioChunk != null && audioChunk.Length > 0
                ? Convert.ToBase64String(audioChunk)
                : "";

            if (withCommonBusiness)
            {
                var jo = new JObject
                {
                    ["common"] = new JObject { ["app_id"] = AppId },
                    ["business"] = new JObject
                    {
                        ["language"] = "zh_cn",
                        ["domain"] = "iat",
                        ["accent"] = "mandarin"
                    },
                    ["data"] = new JObject
                    {
                        ["status"] = status,
                        ["format"] = format,
                        ["encoding"] = encoding,
                        ["audio"] = audioB64
                    }
                };
                return jo.ToString(Newtonsoft.Json.Formatting.None);
            }

            return new JObject
            {
                ["data"] = new JObject
                {
                    ["status"] = status,
                    ["format"] = format,
                    ["encoding"] = encoding,
                    ["audio"] = audioB64
                }
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static async Task SendTextAsync(ClientWebSocket ws, string json, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        /// <summary>接收一帧 JSON 响应；失败返回错误信息，成功返回 null；收到 Close 帧立即返回 null</summary>
        private async Task<string> ReceiveAndAppendAsync(ClientWebSocket ws, StringBuilder textBuilder, bool debugLog, CancellationToken ct)
        {
            if (ws.State != WebSocketState.Open && ws.State != WebSocketState.CloseReceived)
                return null;
            var buffer = new ArraySegment<byte>(new byte[65536]);
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;
                ms.Write(buffer.Array!, buffer.Offset, result.Count);
            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());
            if (string.IsNullOrWhiteSpace(json))
                return null;

            JObject jo;
            try
            {
                jo = JObject.Parse(json);
            }
            catch
            {
                return null;
            }

            var code = jo["code"]?.Value<int>() ?? -1;
            if (code != 0)
            {
                var msg = jo["message"]?.ToString() ?? json;
                return $"讯飞听写错误({code}): {msg}";
            }

            if (debugLog)
                _logger.LogInformation("讯飞响应: {Json}", json.Length > 500 ? json.Substring(0, 500) + "..." : json);

            AppendResultText(jo, textBuilder);
            return null;
        }

        private static void AppendResultText(JObject jo, StringBuilder sb)
        {
            var data = jo["data"] as JObject;
            var result = data?["result"] as JObject;
            if (result == null)
                return;

            var ws = result["ws"] as JArray;
            if (ws == null)
                return;

            foreach (var wItem in ws)
            {
                var cw = wItem["cw"] as JArray;
                if (cw == null || cw.Count == 0)
                    continue;
                // 拼接本片段所有非空词（避免 cw[0] 为空/仅标点导致结果为空）
                foreach (var cwItem in cw)
                {
                    var w = cwItem?["w"]?.ToString();
                    if (!string.IsNullOrEmpty(w))
                        sb.Append(w);
                }
            }
        }
    }
}
