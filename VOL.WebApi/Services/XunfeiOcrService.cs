using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace VOL.WebApi.Services
{
    /// <summary>
    /// 讯飞文字识别（手写文字识别 WebAPI）
    /// 文档：https://www.xfyun.cn/doc/words/wordRecg/API.html
    /// </summary>
    public class XunfeiOcrService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public XunfeiOcrService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        private string BaseUrl => _configuration["IFlytekOcr:BaseUrl"]?.TrimEnd('/')
            ?? "https://webapi.xfyun.cn/v1/service/v1/ocr/handwriting";
        private string AppId => _configuration["IFlytekOcr:AppId"] ?? "";
        private string APIKey => _configuration["IFlytekOcr:APIKey"] ?? "";
        private string Language => _configuration["IFlytekOcr:Language"] ?? "cn|en";
        private string Location => _configuration["IFlytekOcr:Location"] ?? "false";

        public async Task<(string text, string message)> RecognizeHandwritingAsync(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return ("", "请上传图片");
            if (string.IsNullOrEmpty(AppId) || string.IsNullOrEmpty(APIKey))
                return ("", "未配置讯飞图片文字识别参数(IFlytekOcr)");

            var ext = (Path.GetExtension(imageFile.FileName) ?? "").ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp")
                return ("", "仅支持 jpg/jpeg/png/bmp");

            string base64;
            using (var ms = new MemoryStream())
            {
                await imageFile.CopyToAsync(ms);
                base64 = Convert.ToBase64String(ms.ToArray());
            }

            var xParamJson = new JObject
            {
                ["language"] = Language,
                ["location"] = Location
            }.ToString(Newtonsoft.Json.Formatting.None);

            var xParam = Convert.ToBase64String(Encoding.UTF8.GetBytes(xParamJson));
            var xCurTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var checkSum = Md5HexLower(APIKey + xCurTime + xParam);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            req.Headers.TryAddWithoutValidation("X-Appid", AppId);
            req.Headers.TryAddWithoutValidation("X-CurTime", xCurTime);
            req.Headers.TryAddWithoutValidation("X-Param", xParam);
            req.Headers.TryAddWithoutValidation("X-CheckSum", checkSum);

            // 文档要求：image=base64 后再 urlencode，表单提交
            var body = "image=" + Uri.EscapeDataString(base64);
            req.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

            var resp = await client.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return ("", "OCR 请求失败: " + json);

            try
            {
                var jo = JObject.Parse(json);
                var code = jo["code"]?.ToString();
                if (code != "0")
                    return ("", jo["desc"]?.ToString() ?? "OCR 识别失败");

                var sb = new StringBuilder();
                var blocks = jo["data"]?["block"] as JArray;
                if (blocks != null)
                {
                    foreach (var b in blocks)
                    {
                        var lines = b["line"] as JArray;
                        if (lines == null) continue;
                        foreach (var ln in lines)
                        {
                            var words = ln["word"] as JArray;
                            if (words == null) continue;
                            foreach (var w in words)
                            {
                                var c = w["content"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(c))
                                    sb.Append(c);
                            }
                            sb.AppendLine();
                        }
                    }
                }
                return (sb.ToString().Trim(), "");
            }
            catch
            {
                return ("", "OCR 响应解析失败");
            }
        }

        private static string Md5HexLower(string s)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}

