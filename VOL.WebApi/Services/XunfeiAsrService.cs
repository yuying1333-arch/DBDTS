using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace VOL.WebApi.Services
{
    /// <summary>
    /// 讯飞录音文件转写大模型 WebAPI 调用服务
    /// 文档：https://www.xfyun.cn/doc/spark/asr_llm/Ifasr_llm.html
    /// </summary>
    public class XunfeiAsrService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<XunfeiAsrService> _logger;
        private const string UploadPath = "/v2/upload";
        private const string GetResultPath = "/v2/getResult";

        public XunfeiAsrService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<XunfeiAsrService> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        private string BaseUrl => _configuration["IFlytekAsr:BaseUrl"]?.TrimEnd('/') ?? "https://office-api-ist-dx.iflyaisol.com";
        private string AppId => _configuration["IFlytekAsr:AppId"] ?? "";
        private string APIKey => _configuration["IFlytekAsr:APIKey"] ?? "";
        private string APISecret => _configuration["IFlytekAsr:APISecret"] ?? "";
        private string Language => _configuration["IFlytekAsr:Language"] ?? "autodialect";

        /// <summary>
        /// 将录音文件转写为文本（上传 -> 轮询结果 -> 解析文本）
        /// </summary>
        public async Task<(string text, string message)> TranscribeAsync(IFormFile audioFile)
        {
            if (audioFile == null || audioFile.Length == 0)
                return ("", "请上传音频文件");
            if (string.IsNullOrEmpty(AppId) || string.IsNullOrEmpty(APIKey) || string.IsNullOrEmpty(APISecret))
                return ("", "未配置讯飞语音识别参数(IFlytekAsr)");

            using var stream = audioFile.OpenReadStream();
            var fileSize = (int)audioFile.Length;
            var fileName = string.IsNullOrEmpty(audioFile.FileName) ? "audio.pcm" : Path.GetFileName(audioFile.FileName);
            if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
                fileName += ".pcm";

            var signatureRandom = Guid.NewGuid().ToString("N").Substring(0, 16);
            var dt = DateTimeOffset.Now;
            var dateTime = dt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + dt.ToString("zzz").Replace(":", ""); // 如 2025-01-30T12:00:00+0800

            var uploadParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["appId"] = AppId,
                ["accessKeyId"] = APIKey,
                ["dateTime"] = dateTime,
                ["signatureRandom"] = signatureRandom,
                ["fileSize"] = fileSize.ToString(),
                ["fileName"] = fileName,
                ["durationCheckDisable"] = "true",
                ["language"] = Language
            };

            var signature = BuildSignature(uploadParams, APISecret);
            var uploadUrl = BuildUrl(BaseUrl + UploadPath, uploadParams);
            string orderId;
            using (var content = new StreamContent(stream))
            {
                content.Headers.ContentType = new global::System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(2);
                client.DefaultRequestHeaders.Remove("signature");
                client.DefaultRequestHeaders.TryAddWithoutValidation("signature", signature);
                var response = await client.PostAsync(uploadUrl, content);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return ("", "上传失败: " + json);
                }
                _logger.LogInformation("录音文件转写 upload 响应: {Json}", json);
                var jo = JObject.Parse(json);
                if (!IsSuccessCode(jo["code"]?.ToString()))
                {
                    return ("", "上传返回错误: " + (jo["descInfo"]?.ToString() ?? json));
                }
                orderId = jo["content"]?["orderId"]?.ToString();
                if (string.IsNullOrEmpty(orderId))
                {
                    return ("", "上传未返回订单号");
                }
            }

            // 轮询获取转写结果
            var (text, errMsg) = await PollResultAsync(orderId, signatureRandom);
            if (!string.IsNullOrEmpty(errMsg))
                return ("", errMsg);
            return (text ?? "", "");
        }

        private async Task<(string text, string error)> PollResultAsync(string orderId, string signatureRandom)
        {
            var maxAttempts = 60;
            var intervalMs = 2000;

            for (var i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(intervalMs);

                var dt = DateTimeOffset.Now;
                var dateTime = dt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + dt.ToString("zzz").Replace(":", "");
                var getParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["accessKeyId"] = APIKey,
                    ["dateTime"] = dateTime,
                    ["signatureRandom"] = signatureRandom,
                    ["orderId"] = orderId,
                    ["resultType"] = "transfer"
                };
                var signature = BuildSignature(getParams, APISecret);
                var url = BuildUrl(BaseUrl + GetResultPath, getParams);
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Remove("signature");
                client.DefaultRequestHeaders.TryAddWithoutValidation("signature", signature);
                var content = new StringContent("{}", Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return (null, "查询结果请求失败: " + json);
                }
                _logger.LogInformation("录音文件转写 getResult 响应: {Json}", json);

                var jo = JObject.Parse(json);
                if (!IsSuccessCode(jo["code"]?.ToString()))
                {
                    return (null, "查询返回错误: " + (jo["descInfo"]?.ToString() ?? json));
                }

                var orderInfo = jo["content"]?["orderInfo"];
                var status = orderInfo?["status"]?.Value<int>() ?? -1;
                // 4=已完成, -1=失败
                if (status == -1)
                {
                    var failType = orderInfo?["failType"]?.Value<int>() ?? 99;
                    return (null, "转写失败, failType: " + failType);
                }
                if (status == 4)
                {
                    var orderResultStr = jo["content"]?["orderResult"]?.ToString();
                    if (string.IsNullOrEmpty(orderResultStr))
                        return ("", "");
                    var text = ParseOrderResult(orderResultStr);
                    return (text, null);
                }
            }

            return (null, "转写超时，请稍后重试");
        }

        private static bool IsSuccessCode(string code)
        {
            var value = (code ?? string.Empty).Trim();
            return value == "000000" || value == "0";
        }

        /// <summary>
        /// 解析 orderResult JSON 字符串，提取 lattice -> json_1best -> st.rt[].ws[].cw[].w 拼接成文本
        /// </summary>
        private static string ParseOrderResult(string orderResultStr)
        {
            try
            {
                var sb = new StringBuilder();
                var root = JObject.Parse(orderResultStr);
                var lattice = root["lattice"] as JArray;
                if (lattice == null) return "";

                foreach (var item in lattice)
                {
                    var json1best = item["json_1best"]?.ToString();
                    if (string.IsNullOrEmpty(json1best)) continue;
                    try
                    {
                        var best = JObject.Parse(json1best);
                        var st = best["st"];
                        if (st == null) continue;
                        var rt = st["rt"] as JArray;
                        if (rt == null) continue;
                        foreach (var rtItem in rt)
                        {
                            var ws = rtItem["ws"] as JArray;
                            if (ws == null) continue;
                            foreach (var wsItem in ws)
                            {
                                var cw = wsItem["cw"] as JArray;
                                if (cw == null) continue;
                                foreach (var cwItem in cw)
                                {
                                    var w = cwItem["w"]?.ToString();
                                    if (!string.IsNullOrEmpty(w))
                                        sb.Append(w);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 单条 json_1best 解析失败则跳过
                    }
                }

                return sb.ToString().Trim();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 讯飞签名：参数排序后 key=UrlEncode(value) 用 & 连接，HMAC-SHA1(baseString, APISecret)，再 Base64
        /// 空值不参与签名
        /// </summary>
        private static string BuildSignature(SortedDictionary<string, string> sortedParams, string apiSecret)
        {
            var sb = new StringBuilder();
            foreach (var kv in sortedParams)
            {
                if (kv.Key == "signature") continue;
                if (string.IsNullOrEmpty(kv.Value)) continue;
                var encodedKey = Uri.EscapeDataString(kv.Key);
                var encodedValue = Uri.EscapeDataString(kv.Value);
                if (sb.Length > 0) sb.Append("&");
                sb.Append(encodedKey).Append("=").Append(encodedValue);
            }
            var baseString = sb.ToString();
            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(apiSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
            return Convert.ToBase64String(hash);
        }

        private static string BuildUrl(string basePath, SortedDictionary<string, string> queryParams)
        {
            var sb = new StringBuilder(basePath);
            sb.Append("?");
            var first = true;
            foreach (var kv in queryParams)
            {
                if (!first) sb.Append("&");
                first = false;
                sb.Append(Uri.EscapeDataString(kv.Key)).Append("=").Append(Uri.EscapeDataString(kv.Value));
            }
            return sb.ToString();
        }
    }
}
