using Newtonsoft.Json.Linq;

namespace VOL.WebApi.Controllers.Drill
{
    public class RolePunchRequest
    {
        /// <summary>打卡备注（可选）</summary>
        public string Remark { get; set; }

        /// <summary>结构化回答内容（必填/建议）</summary>
        public JToken Answers { get; set; }
    }
}

