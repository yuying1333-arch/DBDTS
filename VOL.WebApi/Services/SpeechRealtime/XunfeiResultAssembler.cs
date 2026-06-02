using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace VOL.WebApi.Services.SpeechRealtime
{
    public class XunfeiResultAssembler
    {
        private readonly SortedDictionary<int, string> _segments = new SortedDictionary<int, string>();

        public (string text, bool hasUpdate, string error) AppendResponse(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch
            {
                return (BuildText(), false, null);
            }

            var code = root["code"]?.Value<int>() ?? -1;
            if (code != 0)
            {
                var message = root["message"]?.ToString() ?? json;
                return (BuildText(), false, $"讯飞实时听写错误({code}): {message}");
            }

            var result = root["data"]?["result"] as JObject;
            if (result == null)
            {
                return (BuildText(), false, null);
            }

            var sn = result["sn"]?.Value<int>() ?? _segments.Count;
            var text = ExtractText(result);
            if (string.IsNullOrEmpty(text))
            {
                return (BuildText(), false, null);
            }

            _segments[sn] = text;
            return (BuildText(), true, null);
        }

        private static string ExtractText(JObject result)
        {
            var ws = result["ws"] as JArray;
            if (ws == null || ws.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var wItem in ws)
            {
                var cw = wItem["cw"] as JArray;
                if (cw == null || cw.Count == 0)
                {
                    continue;
                }

                foreach (var c in cw)
                {
                    var w = c?["w"]?.ToString();
                    if (!string.IsNullOrEmpty(w))
                    {
                        sb.Append(w);
                    }
                }
            }
            return sb.ToString();
        }

        private string BuildText()
        {
            var sb = new StringBuilder();
            foreach (var pair in _segments.OrderBy(x => x.Key))
            {
                sb.Append(pair.Value);
            }
            return sb.ToString();
        }
    }
}
