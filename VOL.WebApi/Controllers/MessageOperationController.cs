using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.Core.ManageUser;
using VOL.System.IRepositories;
using VOL.WebApi.Services;

namespace VOL.WebApi.Controllers
{
    /// <summary>
    /// 消息操作接口（前后端分离：移动端/Web端消息操作页调用）
    /// </summary>
    [Route("api/MessageOperation")]
    public class MessageOperationController : VolController
    {
        private readonly ISys_UserRepository _userRepository;
        private readonly XunfeiAsrService _xunfeiAsrService;
        private readonly XunfeiOcrService _xunfeiOcrService;

        /// <summary>
        /// 内存消息存储（演示用，可后续改为数据库表）
        /// </summary>
        private static readonly List<MessageItem> _messageStore = new List<MessageItem>();
        private static int _messageIdSeed = 1;

        public MessageOperationController(
            ISys_UserRepository userRepository,
            XunfeiAsrService xunfeiAsrService,
            XunfeiOcrService xunfeiOcrService)
        {
            _userRepository = userRepository;
            _xunfeiAsrService = xunfeiAsrService;
            _xunfeiOcrService = xunfeiOcrService;
        }

        /// <summary>
        /// 获取“接收来自”可选对象列表（单选框数据源）
        /// </summary>
        [HttpPost, Route("getReceiveTargets")]
        public IActionResult GetReceiveTargets()
        {
            var list = GetUserTargetList();
            return JsonNormal(list);
        }

        /// <summary>
        /// 获取“发消息给”可选对象列表（单选框数据源）
        /// </summary>
        [HttpPost, Route("getSendTargets")]
        public IActionResult GetSendTargets()
        {
            var list = GetUserTargetList();
            return JsonNormal(list);
        }

        private List<object> GetUserTargetList()
        {
            var userId = UserContext.Current.UserId;
            var query = _userRepository.FindAsIQueryable(x => x.User_Id != userId)
                .Select(s => new { id = s.User_Id, name = s.UserTrueName ?? s.UserName });
            return query.ToList().Select(x => (object)new { id = x.id, name = x.name }).ToList();
        }

        /// <summary>
        /// 根据选中的发送者获取接收到的消息列表
        /// </summary>
        /// <param name="senderId">发送者用户ID（接收来自谁）</param>
        [HttpPost, Route("getReceivedMessages")]
        public IActionResult GetReceivedMessages([FromBody] GetReceivedMessagesRequest request)
        {
            if (request == null || request.SenderId <= 0)
            {
                return JsonNormal(new List<object>());
            }
            var currentUserId = UserContext.Current.UserId;
            var list = _messageStore
                .Where(m => m.FromUserId == request.SenderId && m.ToUserId == currentUserId)
                .OrderByDescending(m => m.CreateTime)
                .Select(m => new { id = m.Id, content = m.Content, createTime = m.CreateTime })
                .ToList();
            return JsonNormal(list);
        }

        /// <summary>
        /// 语音识别：接收录音文件，调用讯飞录音文件转写大模型，返回转写文本。
        /// 移动端：uni.uploadFile({ url, filePath, name: 'file', header: { Authorization } })。
        /// Web端：FormData.append('file', audioBlob); fetch(url, { method: 'POST', headers: { Authorization }, body: formData })。
        /// </summary>
        [HttpPost, Route("voiceRecognize")]
        public async Task<IActionResult> VoiceRecognize(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return JsonNormal(new { text = "", message = "请上传录音文件" });
            }

            var (text, message) = await _xunfeiAsrService.TranscribeAsync(file);
            if (string.IsNullOrEmpty(message))
            {
                return JsonNormal(new { text = text ?? "", message = "" });
            }
            return JsonNormal(new { text = "", message });
        }

        /// <summary>
        /// 图片文字识别（占位接口，待接入OCR API）
        /// </summary>
        [HttpPost, Route("imageTextRecognize")]
        public async Task<IActionResult> ImageTextRecognize(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return JsonNormal(new { text = "", message = "请上传图片" });
            }
            var (text, message) = await _xunfeiOcrService.RecognizeHandwritingAsync(file);
            if (!string.IsNullOrEmpty(message))
            {
                return JsonNormal(new { text = "", message });
            }
            return JsonNormal(new { text = text ?? "", message = "" });
        }

        /// <summary>
        /// 提交消息：将合并后的文本发送给选中的对象
        /// </summary>
        [HttpPost, Route("submitMessage")]
        public IActionResult SubmitMessage([FromBody] SubmitMessageRequest request)
        {
            if (request == null)
            {
                return JsonNormal(new { status = false, message = "参数无效" });
            }
            if (request.ReceiverId <= 0)
            {
                return JsonNormal(new { status = false, message = "请选择接收人" });
            }
            var currentUserId = UserContext.Current.UserId;
            lock (_messageStore)
            {
                _messageStore.Add(new MessageItem
                {
                    Id = _messageIdSeed++,
                    FromUserId = currentUserId,
                    ToUserId = request.ReceiverId,
                    Content = request.Content ?? "",
                    CreateTime = DateTime.Now
                });
            }
            return JsonNormal(new { status = true, message = "发送成功" });
        }
    }

    /// <summary>
    /// 内存消息项
    /// </summary>
    internal class MessageItem
    {
        public int Id { get; set; }
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public string Content { get; set; }
        public DateTime CreateTime { get; set; }
    }

    /// <summary>
    /// 获取接收消息请求
    /// </summary>
    public class GetReceivedMessagesRequest
    {
        public int SenderId { get; set; }
    }

    /// <summary>
    /// 提交消息请求
    /// </summary>
    public class SubmitMessageRequest
    {
        public int ReceiverId { get; set; }
        public string Content { get; set; }
    }
}
