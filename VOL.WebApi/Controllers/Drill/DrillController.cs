using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Collections.Generic;
using VOL.Core.Configuration;
using VOL.Core.Controllers.Basic;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions;
using VOL.Core.Filters;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels.Drill;
using VOL.Entity.DomainModels;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace VOL.WebApi.Controllers.Drill
{
    [Route("api/Drill")]
    [JWTAuthorize]
    public class DrillController : VolController
    {
        private readonly VOLContext _db;
        public DrillController(VOLContext db)
        {
            _db = db;
        }

        [HttpGet("State")]
        public IActionResult GetState(int projectId)
        {
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null) return Json(new WebResponseContent(true) { Data = null });
            var now = DateTime.Now;
            var elapsed = GetCurrentElapsedSeconds(state, now);
            var currentNodeName = GetNodeName(projectId, state.CurrentNodeCode);
            var currentNodeResourceId = GetNodeResourceId(projectId, state.CurrentNodeCode);
            var currentNodeVideoStartSeconds = GetNodeVideoStartSeconds(projectId, state.CurrentNodeCode);
            var currentNodeVideoEndSeconds = GetNodeVideoEndSeconds(projectId, state.CurrentNodeCode);
            var data = new
            {
                state.Id,
                state.ProjectId,
                state.Status,
                state.CurrentStage,
                state.CurrentNodeCode,
                currentNodeName,
                state.StartedAt,
                state.PausedAt,
                state.EndedAt,
                elapsedSeconds = elapsed,
                currentNodeResourceId,
                currentNodeVideoStartSeconds,
                currentNodeVideoEndSeconds,
                state.LastResumedAt,
                state.SettingsJson,
                serverTime = now
            };
            return Json(new WebResponseContent(true) { Data = data });
        }

        [HttpPost("State/Start")]
        public IActionResult Start(int projectId)
        {
            var now = DateTime.Now;
            var u = UserContext.Current.UserInfo;
            // 每次开始演练前，清空该项目历史运行数据，保证从初始态开始
            ResetProjectRuntimeData(projectId);
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            var firstNode = GetFirstNode(projectId);
            if (state == null)
            {
                state = new DrillProjectState
                {
                    ProjectId = projectId,
                    SettingsJson = null,
                    CreateDate = now,
                    CreateID = u.User_Id,
                    Creator = u.UserTrueName
                };
                _db.Add(state);
            }
            state.Status = 1;
            state.CurrentStage = firstNode?.Stage ?? "scene";
            state.CurrentNodeCode = firstNode?.NodeCode ?? "scene_discover";
            state.StartedAt = now;
            state.PausedAt = null;
            state.EndedAt = null;
            state.ElapsedSeconds = 0;
            state.LastResumedAt = now;
            state.ModifyDate = now;
            state.ModifyID = u.User_Id;
            state.Modifier = u.UserTrueName;
            _db.Update(state);
            _db.SaveChanges();

            _db.Add(new DrillEvent
            {
                ProjectId = projectId,
                Stage = "scene",
                EventType = "system",
                Title = "开始演练",
                Content = "教师端点击开始",
                OccurAt = now,
                UserId = u.User_Id,
                UserName = u.UserTrueName,
                RoleName = u.RoleName,
                CreateDate = now
            });
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已开始" });
        }

        private void ResetProjectRuntimeData(int projectId)
        {
            if (projectId <= 0) return;

            var events = _db.Set<DrillEvent>().Where(x => x.ProjectId == projectId).ToList();
            if (events.Any()) _db.RemoveRange(events);

            var messages = _db.Set<DrillMessage>().Where(x => x.ProjectId == projectId).ToList();
            if (messages.Any()) _db.RemoveRange(messages);

            var actions = _db.Set<DrillTaskAction>().Where(x => x.ProjectId == projectId).ToList();
            if (actions.Any()) _db.RemoveRange(actions);

            var punches = _db.Set<DrillRolePunch>().Where(x => x.ProjectId == projectId).ToList();
            if (punches.Any()) _db.RemoveRange(punches);

            var members = _db.Set<DrillProjectMember>().Where(x => x.ProjectId == projectId).ToList();
            if (members.Any())
            {
                foreach (var m in members)
                {
                    m.SignedAt = null;
                    m.ModifyDate = DateTime.Now;
                }
                _db.UpdateRange(members);
            }

            var recoveryItems = _db.Set<DrillRecoveryItem>().Where(x => x.ProjectId == projectId).ToList();
            if (recoveryItems.Any())
            {
                foreach (var item in recoveryItems)
                {
                    item.Status = 0;
                    item.Note = null;
                    item.ModifyDate = null;
                    item.Modifier = null;
                }
                _db.UpdateRange(recoveryItems);
            }

            // 每次重新开始演练时，清空该项目历史学员总结报告
            var reports = _db.Set<DrillStudentReport>().Where(x => x.ProjectId == projectId).ToList();
            if (reports.Any()) _db.RemoveRange(reports);

            _db.SaveChanges();
        }

        [HttpPost("State/Pause")]
        public IActionResult Pause(int projectId)
        {
            var now = DateTime.Now;
            var u = UserContext.Current.UserInfo;
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null) return Json(new WebResponseContent().Error("项目未启动"));
            if (state.Status != 1) return Json(new WebResponseContent().Error("当前非运行状态，无法暂停"));

            state.ElapsedSeconds = GetCurrentElapsedSeconds(state, now);
            state.Status = 2;
            state.PausedAt = now;
            state.LastResumedAt = null;
            state.ModifyDate = now;
            state.ModifyID = u.User_Id;
            state.Modifier = u.UserTrueName;
            _db.Update(state);
            _db.Add(new DrillEvent { ProjectId = projectId, Stage = state.CurrentStage ?? "scene", EventType = "system", Title = "暂停", Content = "教师端点击暂停", OccurAt = now, UserId = u.User_Id, UserName = u.UserTrueName, RoleName = u.RoleName, CreateDate = now });
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已暂停" });
        }

        [HttpPost("State/Resume")]
        public IActionResult Resume(int projectId)
        {
            var now = DateTime.Now;
            var u = UserContext.Current.UserInfo;
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null) return Json(new WebResponseContent().Error("项目未启动"));
            if (state.Status == 3) return Json(new WebResponseContent().Error("项目已结束，无法恢复"));
            if (state.Status == 1) return Json(new WebResponseContent(true) { Message = "已在运行中" });

            state.Status = 1;
            state.PausedAt = null;
            state.LastResumedAt = now;
            state.ModifyDate = now;
            state.ModifyID = u.User_Id;
            state.Modifier = u.UserTrueName;
            _db.Update(state);
            _db.Add(new DrillEvent { ProjectId = projectId, Stage = state.CurrentStage ?? "scene", EventType = "system", Title = "恢复", Content = "教师端点击恢复", OccurAt = now, UserId = u.User_Id, UserName = u.UserTrueName, RoleName = u.RoleName, CreateDate = now });
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已恢复" });
        }

        [HttpPost("State/End")]
        public IActionResult End(int projectId)
        {
            var now = DateTime.Now;
            var u = UserContext.Current.UserInfo;
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null) return Json(new WebResponseContent().Error("项目未启动"));
            if (state.Status == 3) return Json(new WebResponseContent(true) { Message = "已结束" });

            state.ElapsedSeconds = GetCurrentElapsedSeconds(state, now);
            state.Status = 3;
            state.EndedAt = now;
            state.CurrentStage = "end";
            state.LastResumedAt = null;
            state.ModifyDate = now;
            state.ModifyID = u.User_Id;
            state.Modifier = u.UserTrueName;
            _db.Update(state);
            _db.Add(new DrillEvent { ProjectId = projectId, Stage = "end", EventType = "system", Title = "结束", Content = "教师端点击结束", OccurAt = now, UserId = u.User_Id, UserName = u.UserTrueName, RoleName = u.RoleName, CreateDate = now });
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已结束" });
        }

        [HttpPost("State/SetStage")]
        public IActionResult SetStage(int projectId, string stage)
        {
            if (projectId <= 0 || string.IsNullOrWhiteSpace(stage)) return Json(new WebResponseContent().Error("参数不完整"));
            var u = UserContext.Current.UserInfo;
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null) return Json(new WebResponseContent().Error("项目未启动"));
            state.CurrentStage = stage;
            state.ModifyDate = DateTime.Now;
            state.ModifyID = u.User_Id;
            state.Modifier = u.UserTrueName;
            _db.Update(state);
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已更新阶段" });
        }

        [HttpPost("State/NextNode")]
        public IActionResult NextNode(int projectId)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var u = UserContext.Current.UserInfo;
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null) return Json(new WebResponseContent().Error("项目未启动"));
            if (state.Status == 3) return Json(new WebResponseContent().Error("项目已结束"));

            var curNodeCode = state.CurrentNodeCode ?? GetFirstNodeCode(projectId);
            if (string.IsNullOrWhiteSpace(curNodeCode))
                return Json(new WebResponseContent().Error("未配置流程节点"));

            var curNode = _db.Set<DrillFlowNode>()
                .FirstOrDefault(x => x.Enable == 1 && x.ProjectId == projectId && x.NodeCode == curNodeCode)
                ?? _db.Set<DrillFlowNode>()
                    .FirstOrDefault(x => x.Enable == 1 && x.ProjectId == null && x.NodeCode == curNodeCode);
            if (curNode == null) return Json(new WebResponseContent().Error("当前节点不存在"));

            var nextCode = curNode.NextNodeCode;
            if (string.IsNullOrWhiteSpace(nextCode))
            {
                // 最后一个节点点击“下一节点”时，自动进入事故报告阶段
                var nowLast = DateTime.Now;
                state.CurrentStage = "report";
                state.ModifyDate = nowLast;
                state.ModifyID = u.User_Id;
                state.Modifier = u.UserTrueName;
                _db.Update(state);
                _db.Add(new DrillEvent
                {
                    ProjectId = projectId,
                    Stage = "report",
                    EventType = "teacher",
                    Title = "进入事故报告阶段",
                    Content = $"最后节点：{curNode.NodeName}",
                    OccurAt = nowLast,
                    UserId = u.User_Id,
                    UserName = u.UserTrueName,
                    RoleName = u.RoleName,
                    CreateDate = nowLast
                });
                _db.SaveChanges();

                return Json(new WebResponseContent(true)
                {
                    Message = "已进入事故报告阶段",
                    Data = new
                    {
                        currentNodeCode = state.CurrentNodeCode,
                        currentStage = state.CurrentStage,
                        currentNodeResourceId = curNode.ResourceId,
                        currentNodeVideoStartSeconds = curNode.VideoStartSeconds,
                        currentNodeVideoEndSeconds = curNode.VideoEndSeconds
                    }
                });
            }

            var nextNode = _db.Set<DrillFlowNode>()
                .FirstOrDefault(x => x.Enable == 1 && x.ProjectId == projectId && x.NodeCode == nextCode)
                ?? _db.Set<DrillFlowNode>()
                    .FirstOrDefault(x => x.Enable == 1 && x.ProjectId == null && x.NodeCode == nextCode);
            if (nextNode == null) return Json(new WebResponseContent().Error("下一节点不存在"));

            var now = DateTime.Now;
            state.CurrentNodeCode = nextNode.NodeCode;
            state.CurrentStage = nextNode.Stage ?? state.CurrentStage;
            state.ModifyDate = now;
            state.ModifyID = u.User_Id;
            state.Modifier = u.UserTrueName;
            _db.Update(state);
            _db.Add(new DrillEvent
            {
                ProjectId = projectId,
                Stage = state.CurrentStage ?? "scene",
                EventType = "teacher",
                Title = "进入下一节点",
                Content = $"{curNode.NodeName} -> {nextNode.NodeName}",
                OccurAt = now,
                UserId = u.User_Id,
                UserName = u.UserTrueName,
                RoleName = u.RoleName,
                CreateDate = now
            });
            _db.SaveChanges();

            return Json(new WebResponseContent(true)
            {
                Message = "已进入下一节点",
                Data = new
                {
                    currentNodeCode = state.CurrentNodeCode,
                    currentStage = state.CurrentStage,
                    currentNodeResourceId = nextNode.ResourceId,
                    currentNodeVideoStartSeconds = nextNode.VideoStartSeconds,
                    currentNodeVideoEndSeconds = nextNode.VideoEndSeconds
                }
            });
        }

        [HttpPost("State/SetNode")]
        public IActionResult SetNode(int projectId, string nodeCode)
        {
            if (projectId <= 0 || string.IsNullOrWhiteSpace(nodeCode))
                return Json(new WebResponseContent().Error("参数不完整"));

            var u = UserContext.Current.UserInfo;
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null) return Json(new WebResponseContent().Error("项目未启动"));
            if (state.Status == 3) return Json(new WebResponseContent().Error("项目已结束"));

            var code = nodeCode.Trim();
            var targetNode = _db.Set<DrillFlowNode>()
                .FirstOrDefault(x => x.Enable == 1 && x.ProjectId == projectId && x.NodeCode == code)
                ?? _db.Set<DrillFlowNode>()
                    .FirstOrDefault(x => x.Enable == 1 && x.ProjectId == null && x.NodeCode == code);
            if (targetNode == null) return Json(new WebResponseContent().Error("目标节点不存在"));

            var oldNodeName = GetNodeName(projectId, state.CurrentNodeCode) ?? state.CurrentNodeCode ?? "-";
            var now = DateTime.Now;
            state.CurrentNodeCode = targetNode.NodeCode;
            state.CurrentStage = targetNode.Stage ?? state.CurrentStage;
            state.ModifyDate = now;
            state.ModifyID = u.User_Id;
            state.Modifier = u.UserTrueName;
            _db.Update(state);

            _db.Add(new DrillEvent
            {
                ProjectId = projectId,
                Stage = state.CurrentStage ?? "scene",
                EventType = "teacher",
                Title = "切换节点",
                Content = $"{oldNodeName} -> {targetNode.NodeName}",
                OccurAt = now,
                UserId = u.User_Id,
                UserName = u.UserTrueName,
                RoleName = u.RoleName,
                CreateDate = now
            });
            _db.SaveChanges();

            return Json(new WebResponseContent(true)
            {
                Message = "节点已切换",
                Data = new
                {
                    currentNodeCode = state.CurrentNodeCode,
                    currentStage = state.CurrentStage,
                    currentNodeResourceId = targetNode.ResourceId,
                    currentNodeVideoStartSeconds = targetNode.VideoStartSeconds,
                    currentNodeVideoEndSeconds = targetNode.VideoEndSeconds
                }
            });
        }

        [HttpPost("State/Settings")]
        public IActionResult UpdateSettings(int projectId, [FromBody] object settings)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var u = UserContext.Current.UserInfo;
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null)
            {
                state = new DrillProjectState
                {
                    ProjectId = projectId,
                    Status = 0,
                    CurrentStage = "scene",
                    CurrentNodeCode = GetFirstNodeCode(projectId),
                    StartedAt = null,
                    PausedAt = null,
                    EndedAt = null,
                    ElapsedSeconds = 0,
                    LastResumedAt = null,
                    SettingsJson = null,
                    CreateDate = DateTime.Now,
                    CreateID = u.User_Id,
                    Creator = u.UserTrueName
                };
                _db.Add(state);
            }
            state.SettingsJson = settings?.ToString();
            state.ModifyDate = DateTime.Now;
            state.ModifyID = u.User_Id;
            state.Modifier = u.UserTrueName;
            _db.Update(state);
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已保存设置" });
        }

        private static int GetCurrentElapsedSeconds(DrillProjectState state, DateTime now)
        {
            var baseSeconds = state.ElapsedSeconds < 0 ? 0 : state.ElapsedSeconds;
            if (state.Status == 1 && state.LastResumedAt.HasValue)
            {
                var delta = (int)Math.Max(0, (now - state.LastResumedAt.Value).TotalSeconds);
                return baseSeconds + delta;
            }
            return baseSeconds;
        }

        private string GetFirstNodeCode(int projectId)
        {
            return GetFirstNode(projectId)?.NodeCode;
        }

        private DrillFlowNode GetFirstNode(int projectId)
        {
            var node = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == projectId)
                .OrderBy(x => x.OrderNo).ThenBy(x => x.Id)
                .FirstOrDefault();
            if (node != null) return node;

            return _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == null)
                .OrderBy(x => x.OrderNo).ThenBy(x => x.Id)
                .FirstOrDefault();
        }

        private string GetNodeName(int projectId, string nodeCode)
        {
            if (string.IsNullOrWhiteSpace(nodeCode)) return null;
            var name = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == projectId && x.NodeCode == nodeCode)
                .Select(x => x.NodeName)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == null && x.NodeCode == nodeCode)
                .Select(x => x.NodeName)
                .FirstOrDefault();
        }

        private int? GetNodeVideoStartSeconds(int projectId, string nodeCode)
        {
            if (string.IsNullOrWhiteSpace(nodeCode)) return null;
            var seconds = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == projectId && x.NodeCode == nodeCode)
                .Select(x => x.VideoStartSeconds)
                .FirstOrDefault();
            if (seconds.HasValue) return seconds;
            return _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == null && x.NodeCode == nodeCode)
                .Select(x => x.VideoStartSeconds)
                .FirstOrDefault();
        }

        private int? GetNodeVideoEndSeconds(int projectId, string nodeCode)
        {
            if (string.IsNullOrWhiteSpace(nodeCode)) return null;
            var seconds = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == projectId && x.NodeCode == nodeCode)
                .Select(x => x.VideoEndSeconds)
                .FirstOrDefault();
            if (seconds.HasValue) return seconds;
            return _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == null && x.NodeCode == nodeCode)
                .Select(x => x.VideoEndSeconds)
                .FirstOrDefault();
        }

        private long? GetNodeResourceId(int projectId, string nodeCode)
        {
            if (string.IsNullOrWhiteSpace(nodeCode)) return null;
            var resourceId = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == projectId && x.NodeCode == nodeCode)
                .Select(x => x.ResourceId)
                .FirstOrDefault();
            if (resourceId.HasValue) return resourceId;
            return _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == null && x.NodeCode == nodeCode)
                .Select(x => x.ResourceId)
                .FirstOrDefault();
        }

        [HttpGet("Events")]
        public IActionResult GetEvents(int projectId, string stage = null)
        {
            var q = _db.Set<DrillEvent>().Where(x => x.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(x => x.Stage == stage);
            var list = q.OrderBy(x => x.OccurAt ?? x.CreateDate).ThenBy(x => x.Id).Take(2000).ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpPost("Events/Add")]
        public IActionResult AddEvent([FromBody] DrillEvent input)
        {
            if (input == null || input.ProjectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var now = DateTime.Now;
            var u = UserContext.Current.UserInfo;
            input.Id = 0;
            input.UserId = u.User_Id;
            input.UserName = u.UserTrueName;
            input.RoleName = u.RoleName;
            input.CreateDate = now;
            input.OccurAt ??= now;
            _db.Add(input);
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已添加", Data = input });
        }

        [HttpGet("Messages")]
        public IActionResult GetMessages(int projectId, string channel = "discussion", string nodeCode = null)
        {
            var q = _db.Set<DrillMessage>()
                .Where(x => x.ProjectId == projectId && x.Channel == channel);
            if (!string.IsNullOrWhiteSpace(nodeCode))
            {
                var code = nodeCode.Trim();
                q = q.Where(x => x.NodeCode == code);
            }
            var list = q
                .OrderByDescending(x => x.CreateDate ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id)
                .Take(500)
                .ToList();

            var userIds = list
                .Select(x => x.UserId)
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x.Value)
                .Distinct()
                .ToList();
            var userRoleIdMap = new Dictionary<int, int?>();
            var userLoginNameMap = new Dictionary<int, string>();
            var userTrueNameMap = new Dictionary<int, string>();
            var memberRoleMap = new Dictionary<int, string>();
            if (userIds.Any())
            {
                var users = _db.Set<Sys_User>()
                    .Where(x => userIds.Contains(x.User_Id))
                    .Select(x => new { x.User_Id, x.Role_Id, x.UserName, x.UserTrueName })
                    .ToList();
                foreach (var u in users)
                {
                    userRoleIdMap[u.User_Id] = u.Role_Id;
                    userLoginNameMap[u.User_Id] = u.UserName;
                    userTrueNameMap[u.User_Id] = u.UserTrueName;
                }

                var members = _db.Set<DrillProjectMember>()
                    .Where(x => x.ProjectId == projectId && x.UserId.HasValue && userIds.Contains(x.UserId.Value))
                    .Select(x => new { UserId = x.UserId.Value, x.RoleName })
                    .ToList()
                    .GroupBy(x => x.UserId)
                    .Select(g => g.First())
                    .ToList();
                foreach (var m in members)
                {
                    memberRoleMap[m.UserId] = m.RoleName;
                }
            }

            foreach (var msg in list)
            {
                if (!msg.UserId.HasValue || msg.UserId.Value <= 0) continue;
                if (!userRoleIdMap.TryGetValue(msg.UserId.Value, out var roleId)) continue;

                // 按 sys_user.Role_Id 识别用户身份：3=学生，5=教师
                if (roleId == 5)
                {
                    msg.RoleName = "教师";
                }
                else if (roleId == 3)
                {
                    if (memberRoleMap.TryGetValue(msg.UserId.Value, out var memberRole) && !string.IsNullOrWhiteSpace(memberRole))
                    {
                        msg.RoleName = memberRole;
                    }
                    else if (string.IsNullOrWhiteSpace(msg.RoleName))
                    {
                        msg.RoleName = "学生";
                    }
                }

                userTrueNameMap.TryGetValue(msg.UserId.Value, out var userTrueName);
                userLoginNameMap.TryGetValue(msg.UserId.Value, out var loginName);
                if (string.IsNullOrWhiteSpace(msg.UserName) && !string.IsNullOrWhiteSpace(userTrueName))
                {
                    msg.UserName = userTrueName;
                }
                else if (string.IsNullOrWhiteSpace(msg.UserName) && !string.IsNullOrWhiteSpace(loginName))
                {
                    msg.UserName = loginName;
                }
            }
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpPost("Messages/Send")]
        public IActionResult SendMessage(int projectId, string channel, [FromBody] SendMessageBody body)
        {
            string content = body?.content;
            if (projectId <= 0 || string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(content))
                return Json(new WebResponseContent().Error("参数不完整"));
            var now = DateTime.Now;
            var u = UserContext.Current.UserInfo;
            var messageNodeCode = (body?.nodeCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(messageNodeCode))
            {
                messageNodeCode = GetCurrentNodeCode(projectId);
            }

            long? parentMessageId = body?.parentMessageId;
            if (parentMessageId.HasValue && parentMessageId.Value > 0)
            {
                var parent = _db.Set<DrillMessage>().FirstOrDefault(x =>
                    x.Id == parentMessageId.Value &&
                    x.ProjectId == projectId &&
                    x.Channel == channel);
                if (parent == null)
                    return Json(new WebResponseContent().Error("被点评消息不存在"));
                parentMessageId = parent.Id;
                if (string.IsNullOrWhiteSpace(messageNodeCode))
                {
                    messageNodeCode = parent.NodeCode;
                }
            }

            if (string.Equals(channel, "discussion", StringComparison.OrdinalIgnoreCase))
            {
                var member = _db.Set<DrillProjectMember>()
                    .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
                if (member != null && !string.IsNullOrWhiteSpace(member.RoleName))
                {
                    var effectiveNodeCode = string.IsNullOrWhiteSpace(messageNodeCode) ? GetCurrentNodeCode(projectId) : messageNodeCode;
                    var allowedRoles = GetAllowedRolesForNode(projectId, effectiveNodeCode);
                    if (allowedRoles.Any() && !allowedRoles.Contains(member.RoleName, StringComparer.OrdinalIgnoreCase) && body?.selfInitiated != true)
                    {
                        return Json(new WebResponseContent().Error("当前节点未激活该身份组讨论权限，请选择自主发言"));
                    }
                    if (body?.selfInitiated == true)
                    {
                        content = $"[自主发言] {content.Trim()}";
                    }
                }
            }

            var msg = new DrillMessage
            {
                ProjectId = projectId,
                Channel = channel,
                ParentMessageId = parentMessageId,
                NodeCode = string.IsNullOrWhiteSpace(messageNodeCode) ? null : messageNodeCode,
                Content = content,
                UserId = u.User_Id,
                UserName = u.UserTrueName,
                RoleName = u.RoleName,
                CreateDate = now
            };
            _db.Add(msg);
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已发送", Data = msg });
        }

        private string GetCurrentNodeCode(int projectId)
        {
            if (projectId <= 0) return null;
            return _db.Set<DrillProjectState>()
                .Where(x => x.ProjectId == projectId)
                .Select(x => x.CurrentNodeCode)
                .FirstOrDefault();
        }

        private HashSet<string> GetAllowedRolesForNode(int projectId, string nodeCode)
        {
            if (projectId <= 0 || string.IsNullOrWhiteSpace(nodeCode)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null || string.IsNullOrWhiteSpace(state.SettingsJson))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var jo = JObject.Parse(state.SettingsJson);
                var permissions = jo["nodeRolePermissions"] as JObject;
                if (permissions == null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var token = permissions[nodeCode];
                if (token == null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (token is JArray arr)
                {
                    return new HashSet<string>(
                        arr.Select(x => x?.ToString()?.Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x)),
                        StringComparer.OrdinalIgnoreCase
                    );
                }
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private List<string> ParseRoleTaskItems(string taskBookJson)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(taskBookJson)) return result;
            try
            {
                var jo = JObject.Parse(taskBookJson);
                var tasks = jo["tasks"] as JArray;
                if (tasks == null) return result;
                foreach (var t in tasks)
                {
                    var title = t?["title"]?.ToString()?.Trim();
                    var items = t?["items"] as JArray;
                    if (items != null && items.Count > 0)
                    {
                        foreach (var it in items)
                        {
                            var txt = it?["text"]?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(txt)) result.Add(txt);
                        }
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(title)) result.Add(title);
                }
            }
            catch
            {
                // ignore parse errors and return empty list
            }
            return result;
        }

        [HttpGet("Recovery")]
        public IActionResult GetRecovery(int projectId)
        {
            var list = _db.Set<DrillRecoveryItem>()
                .Where(x => x.ProjectId == projectId)
                .OrderBy(x => x.OrderNo ?? 9999)
                .ThenBy(x => x.Id)
                .ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpPost("Recovery/Update")]
        public IActionResult UpdateRecovery([FromBody] DrillRecoveryItem item)
        {
            if (item == null || item.Id <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var u = UserContext.Current.UserInfo;
            item.ModifyDate = DateTime.Now;
            item.Modifier = u.UserTrueName;
            _db.Update(item);
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已更新" });
        }

        [HttpGet("Students")]
        public IActionResult GetStudents()
        {
            // 学生侧将来由小程序注册，这里给教师端查看学生身份
            var list = _db.Set<Sys_User>()
                .Where(x => x.Enable == 1 && (x.RoleName == "学生" || x.Role_Id == 3))
                .Select(x => new { x.User_Id, x.UserName, x.UserTrueName, x.Role_Id, x.RoleName, x.PhoneNo, x.CreateDate })
                .OrderByDescending(x => x.User_Id)
                .Take(2000)
                .ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpGet("Projects")]
        public IActionResult GetProjects()
        {
            // 教师端选择项目：按项目序号（Id）正序返回，便于缺省选择最早项目
            var list = _db.Set<Project>()
                .OrderBy(x => x.Id)
                .Take(2000)
                .Select(x => new { x.Id, x.Name, x.Code, x.Status, x.CreateDate })
                .ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        /// <summary>
        /// 获取项目待审核/已审核成员列表（教师端）
        /// 项目归属来自 drill_project_member，账号/姓名/联系方式/注册时间优先关联 sys_user
        /// </summary>
        [HttpGet("Members")]
        public IActionResult GetMembers(int projectId, int? auditStatus = null)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var list = QueryProjectMembers(projectId, auditStatus);
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpGet("Members/ExportSimple")]
        public IActionResult ExportMembersSimple(int projectId)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var list = QueryProjectMembers(projectId, null)
                .Select(x => new
                {
                    userName = x.UserName,
                    userTrueName = x.UserTrueName
                })
                .ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpPost("Members/ImportSimple")]
        public IActionResult ImportMembersSimple(int projectId, [FromBody] ImportMembersSimpleRequest req)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var items = (req?.Items ?? new List<ImportMembersSimpleItem>())
                .Where(x => !string.IsNullOrWhiteSpace(x.UserName) && !string.IsNullOrWhiteSpace(x.UserTrueName))
                .Select(x => new ImportMembersSimpleItem
                {
                    UserName = x.UserName.Trim(),
                    UserTrueName = x.UserTrueName.Trim()
                })
                .GroupBy(x => x.UserName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            if (!items.Any()) return Json(new WebResponseContent().Error("请至少导入一条账号和姓名"));

            var project = _db.Set<Project>().FirstOrDefault(x => x.Id == projectId);
            if (project == null) return Json(new WebResponseContent().Error("项目不存在"));

            var drillRole = _db.Set<DrillRole>()
                .Where(x => x.Enable == 1)
                .OrderBy(x => x.Id)
                .FirstOrDefault();
            if (drillRole == null) return Json(new WebResponseContent().Error("未配置可用身份组"));

            var studentRole = _db.Set<Sys_Role>()
                .Where(x => (x.RoleName == "学生" || x.Role_Id == 3) && (x.Enable == null || x.Enable == 1))
                .OrderBy(x => x.Role_Id)
                .Select(x => new { x.Role_Id, x.RoleName })
                .FirstOrDefault();
            if (studentRole == null)
            {
                studentRole = _db.Set<Sys_Role>()
                    .Where(x => x.Role_Id != 1 && (x.Enable == null || x.Enable == 1))
                    .OrderBy(x => x.Role_Id)
                    .Select(x => new { x.Role_Id, x.RoleName })
                    .FirstOrDefault();
            }
            if (studentRole == null) return Json(new WebResponseContent().Error("系统未配置学生角色"));

            var existingMembers = _db.Set<DrillProjectMember>()
                .Where(x => x.ProjectId == projectId)
                .ToList();
            var memberMap = existingMembers.ToDictionary(x => x.UserName ?? "", StringComparer.OrdinalIgnoreCase);

            var now = DateTime.Now;
            var createCount = 0;
            var updateCount = 0;
            foreach (var item in items)
            {
                var user = _db.Set<Sys_User>().FirstOrDefault(x => x.UserName == item.UserName);
                if (user == null)
                {
                    user = new Sys_User
                    {
                        UserName = item.UserName,
                        UserPwd = "123456".EncryptDES(AppSetting.Secret.User),
                        UserTrueName = item.UserTrueName,
                        Role_Id = studentRole.Role_Id,
                        RoleName = studentRole.RoleName ?? "学生",
                        Enable = 1,
                        CreateDate = now
                    };
                    _db.Add(user);
                    _db.SaveChanges();
                }
                else if (string.IsNullOrWhiteSpace(user.UserTrueName))
                {
                    user.UserTrueName = item.UserTrueName;
                    _db.Update(user);
                    _db.SaveChanges();
                }

                if (memberMap.TryGetValue(item.UserName, out var old))
                {
                    old.UserId = user.User_Id;
                    old.UserTrueName = item.UserTrueName;
                    old.ModifyDate = now;
                    _db.Update(old);
                    updateCount++;
                    continue;
                }

                var member = new DrillProjectMember
                {
                    ProjectId = projectId,
                    UserId = user.User_Id,
                    UserName = item.UserName,
                    UserTrueName = item.UserTrueName,
                    RoleName = drillRole.RoleName,
                    AuditStatus = 1,
                    CreateDate = now
                };
                _db.Add(member);
                createCount++;
            }
            _db.SaveChanges();
            return Json(new WebResponseContent(true)
            {
                Message = $"导入完成，新增{createCount}人，更新{updateCount}人",
                Data = new { createCount, updateCount }
            });
        }

        /// <summary>
        /// 添加学生（项目管理-添加学生，管理员/教师操作，默认审核通过）
        /// </summary>
        [HttpPost("Members/Add")]
        public IActionResult AddMember([FromBody] AddStudentRequest req)
        {
            if (req == null || req.ProjectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password)
                || string.IsNullOrWhiteSpace(req.RoleName) || string.IsNullOrWhiteSpace(req.UserTrueName))
                return Json(new WebResponseContent().Error("用户名、密码、角色、真实姓名不能为空"));
            if (req.Password.Trim().Length < 6)
                return Json(new WebResponseContent().Error("密码长度不能少于6位"));

            var project = _db.Set<Project>().FirstOrDefault(x => x.Id == req.ProjectId);
            if (project == null) return Json(new WebResponseContent().Error("项目不存在"));

            var drillRole = _db.Set<DrillRole>()
                .FirstOrDefault(x => x.Enable == 1 && x.RoleName == req.RoleName.Trim());
            if (drillRole == null) return Json(new WebResponseContent().Error("角色不存在或未启用"));

            var exists = _db.Set<DrillProjectMember>()
                .Any(x => x.ProjectId == req.ProjectId && x.UserName == req.UserName.Trim());
            if (exists) return Json(new WebResponseContent().Error("该项目下该用户名已存在"));

            var user = _db.Set<Sys_User>().FirstOrDefault(x => x.UserName == req.UserName.Trim());
            var studentRole = _db.Set<Sys_Role>()
                .Where(x => (x.RoleName == "学生" || x.Role_Id == 3) && (x.Enable == null || x.Enable == 1))
                .OrderBy(x => x.Role_Id).Select(x => new { x.Role_Id, x.RoleName }).FirstOrDefault();
            if (studentRole == null)
                studentRole = _db.Set<Sys_Role>()
                    .Where(x => x.Role_Id != 1 && (x.Enable == null || x.Enable == 1))
                    .OrderBy(x => x.Role_Id).Select(x => new { x.Role_Id, x.RoleName }).FirstOrDefault();

            if (user == null)
            {
                if (studentRole == null) return Json(new WebResponseContent().Error("系统未配置学生角色"));
                user = new Sys_User
                {
                    UserName = req.UserName.Trim(),
                    UserPwd = req.Password.Trim().EncryptDES(AppSetting.Secret.User),
                    UserTrueName = req.UserTrueName?.Trim(),
                    Role_Id = studentRole.Role_Id,
                    RoleName = studentRole.RoleName ?? "学生",
                    Enable = 1,
                    CreateDate = DateTime.Now
                };
                _db.Add(user);
                _db.SaveChanges();
            }

            var member = new DrillProjectMember
            {
                ProjectId = req.ProjectId,
                UserId = user.User_Id,
                UserName = req.UserName.Trim(),
                RoleName = drillRole.RoleName,
                UserTrueName = req.UserTrueName?.Trim(),
                Org = req.Org?.Trim(),
                JobTitle = req.JobTitle?.Trim(),
                Contact = req.Contact?.Trim(),
                AuditStatus = 1,
                CreateDate = DateTime.Now
            };
            _db.Add(member);
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "添加成功，学生可使用项目编号和账号密码登录小程序" });
        }

        /// <summary>
        /// 审核通过项目成员（教师/管理员）
        /// </summary>
        [HttpPost("Members/Approve")]
        public IActionResult ApproveMember(long id)
        {
            var member = _db.Set<DrillProjectMember>().FirstOrDefault(x => x.Id == id);
            if (member == null) return Json(new WebResponseContent().Error("未找到记录"));
            member.AuditStatus = 1;
            member.ModifyDate = DateTime.Now;
            _db.Update(member);
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "已审核通过" });
        }

        /// <summary>
        /// 更新项目成员身份组角色（教师/管理员）
        /// </summary>
        [HttpPost("Members/UpdateRole")]
        public IActionResult UpdateMemberRole(long id, [FromBody] UpdateMemberRoleRequest req)
        {
            if (id <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            if (req == null || string.IsNullOrWhiteSpace(req.RoleName))
                return Json(new WebResponseContent().Error("角色不能为空"));

            var member = _db.Set<DrillProjectMember>().FirstOrDefault(x => x.Id == id);
            if (member == null) return Json(new WebResponseContent().Error("未找到记录"));

            var roleName = req.RoleName.Trim();
            var drillRole = _db.Set<DrillRole>()
                .FirstOrDefault(x => x.Enable == 1 && x.RoleName == roleName);
            if (drillRole == null) return Json(new WebResponseContent().Error("角色不存在或未启用"));

            if (string.Equals(member.RoleName, roleName, StringComparison.OrdinalIgnoreCase))
                return Json(new WebResponseContent(true) { Message = "角色未变化" });

            member.RoleName = drillRole.RoleName;
            member.ModifyDate = DateTime.Now;
            _db.Update(member);
            _db.SaveChanges();

            return Json(new WebResponseContent(true)
            {
                Message = "角色更新成功",
                Data = new { member.Id, member.ProjectId, member.UserId, member.UserName, member.RoleName }
            });
        }

        /// <summary>
        /// 批量更新项目成员身份组角色（教师/管理员）
        /// </summary>
        [HttpPost("Members/BatchUpdateRole")]
        public IActionResult BatchUpdateMemberRole([FromBody] BatchUpdateMemberRoleRequest req)
        {
            if (req == null) return Json(new WebResponseContent().Error("参数不能为空"));
            if (string.IsNullOrWhiteSpace(req.RoleName)) return Json(new WebResponseContent().Error("角色不能为空"));
            var ids = (req.Ids ?? new List<long>()).Where(x => x > 0).Distinct().ToList();
            if (!ids.Any()) return Json(new WebResponseContent().Error("请选择要修改的学生"));

            var roleName = req.RoleName.Trim();
            var drillRole = _db.Set<DrillRole>().FirstOrDefault(x => x.Enable == 1 && x.RoleName == roleName);
            if (drillRole == null) return Json(new WebResponseContent().Error("角色不存在或未启用"));

            var q = _db.Set<DrillProjectMember>().Where(x => ids.Contains(x.Id));
            if (req.ProjectId.HasValue && req.ProjectId.Value > 0)
                q = q.Where(x => x.ProjectId == req.ProjectId.Value);
            var members = q.ToList();
            if (!members.Any()) return Json(new WebResponseContent().Error("未找到可更新的项目成员"));

            var now = DateTime.Now;
            foreach (var member in members)
            {
                member.RoleName = drillRole.RoleName;
                member.ModifyDate = now;
            }
            _db.UpdateRange(members);
            _db.SaveChanges();

            return Json(new WebResponseContent(true)
            {
                Message = $"批量更新成功，共 {members.Count} 人",
                Data = new { count = members.Count, roleName = drillRole.RoleName }
            });
        }

        [HttpGet("Flow/Nodes")]
        public IActionResult GetFlowNodes(int projectId)
        {
            var projectNodes = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == projectId)
                .OrderBy(x => x.OrderNo).ThenBy(x => x.Id).ToList();
            var nodes = projectNodes.Any()
                ? projectNodes
                : _db.Set<DrillFlowNode>()
                    .Where(x => x.Enable == 1 && x.ProjectId == null)
                    .OrderBy(x => x.OrderNo).ThenBy(x => x.Id).ToList();
            return Json(new WebResponseContent(true) { Data = nodes });
        }

        [HttpPost("Flow/Nodes/Save")]
        public IActionResult SaveFlowNodes(int projectId, [FromBody] SaveFlowNodesRequest req)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var items = req?.Items ?? new List<SaveFlowNodeItem>();
            if (!items.Any()) return Json(new WebResponseContent().Error("请至少配置一个节点"));

            var now = DateTime.Now;
            var validResourceIds = _db.Set<DrillProjectVideoResource>()
                .Where(x => x.ProjectId == projectId)
                .Select(x => x.Id)
                .ToHashSet();
            var oldProjectNodes = _db.Set<DrillFlowNode>()
                .Where(x => x.ProjectId == projectId)
                .ToList();
            if (oldProjectNodes.Any()) _db.RemoveRange(oldProjectNodes);

            var nodes = new List<DrillFlowNode>();
            var codeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 1;
            foreach (var item in items)
            {
                var name = (item.NodeName ?? "").Trim();
                if (string.IsNullOrWhiteSpace(name)) name = $"节点{index}";

                var code = (item.NodeCode ?? "").Trim();
                if (string.IsNullOrWhiteSpace(code)) code = $"node_{index:00}";
                if (codeSet.Contains(code)) code = $"{code}_{index:00}";
                codeSet.Add(code);

                var start = item.VideoStartSeconds.HasValue && item.VideoStartSeconds.Value >= 0
                    ? item.VideoStartSeconds.Value
                    : 0;
                int? end = null;
                if (item.VideoEndSeconds.HasValue && item.VideoEndSeconds.Value >= 0)
                {
                    end = item.VideoEndSeconds.Value < start ? start : item.VideoEndSeconds.Value;
                }

                nodes.Add(new DrillFlowNode
                {
                    ProjectId = projectId,
                    NodeCode = code,
                    NodeName = name,
                    Stage = string.IsNullOrWhiteSpace(item.Stage) ? "scene" : item.Stage.Trim(),
                    OrderNo = index * 10,
                    Description = item.Description,
                    ResourceId = item.ResourceId.HasValue && validResourceIds.Contains(item.ResourceId.Value) ? item.ResourceId.Value : null,
                    VideoStartSeconds = start,
                    VideoEndSeconds = end,
                    Enable = 1,
                    CreateDate = now,
                    ModifyDate = now
                });
                index++;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].NextNodeCode = (i < nodes.Count - 1) ? nodes[i + 1].NodeCode : null;
            }

            _db.AddRange(nodes);
            _db.SaveChanges();

            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state != null)
            {
                var existsCurrent = nodes.Any(x => x.NodeCode == state.CurrentNodeCode);
                if (!existsCurrent)
                {
                    var first = nodes.First();
                    state.CurrentNodeCode = first.NodeCode;
                    state.CurrentStage = first.Stage ?? state.CurrentStage;
                    state.ModifyDate = now;
                    _db.Update(state);
                    _db.SaveChanges();
                }
            }

            return Json(new WebResponseContent(true)
            {
                Message = "节点配置已保存",
                Data = nodes.Select(x => new { x.NodeCode, x.NodeName, x.OrderNo, x.ResourceId, x.VideoStartSeconds, x.VideoEndSeconds }).ToList()
            });
        }

        [HttpPost("Flow/NodeVideoTimes")]
        public IActionResult SaveNodeVideoTimes(int projectId, [FromBody] SaveNodeVideoTimesRequest req)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            if (req?.Items == null || req.Items.Count == 0) return Json(new WebResponseContent().Error("未提交节点时间点"));

            var now = DateTime.Now;
            var projectNodes = EnsureProjectNodesForVideoConfig(projectId);

            foreach (var item in req.Items)
            {
                if (string.IsNullOrWhiteSpace(item.NodeCode)) continue;
                var code = item.NodeCode.Trim();
                var node = projectNodes.FirstOrDefault(x => x.NodeCode == code);
                if (node == null) continue;
                var start = item.VideoStartSeconds.HasValue && item.VideoStartSeconds.Value >= 0
                    ? item.VideoStartSeconds
                    : null;
                var end = item.VideoEndSeconds.HasValue && item.VideoEndSeconds.Value >= 0
                    ? item.VideoEndSeconds
                    : null;
                if (start.HasValue && end.HasValue && end.Value < start.Value) end = start;
                node.VideoStartSeconds = start;
                node.VideoEndSeconds = end;
                node.ModifyDate = now;
            }

            _db.UpdateRange(projectNodes);
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "节点时间点已保存" });
        }

        [HttpGet("ProjectResources")]
        public IActionResult GetProjectResources(int projectId)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var list = _db.Set<DrillProjectVideoResource>()
                .Where(x => x.ProjectId == projectId)
                .OrderByDescending(x => x.Enable)
                .ThenByDescending(x => x.Id)
                .Take(20)
                .ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpGet("StudentReports")]
        public IActionResult GetStudentReports(int projectId, string reportType = null)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var q = _db.Set<DrillStudentReport>().Where(x => x.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(reportType))
            {
                var t = reportType.Trim().ToLower();
                if (t == "summary" || t == "report" || t == "recovery")
                {
                    q = q.Where(x => x.ReportType == "summary" || x.ReportType == "report" || x.ReportType == "recovery");
                }
                else
                {
                    q = q.Where(x => x.ReportType == t);
                }
            }
            var list = q
                .OrderBy(x => x.RoleName)
                .ThenBy(x => x.UserTrueName)
                .ThenByDescending(x => x.CreateDate)
                .ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpPost("StudentReports/Review")]
        public IActionResult ReviewStudentReport(long id, [FromBody] ReviewStudentReportRequest req)
        {
            if (id <= 0 || req == null) return Json(new WebResponseContent().Error("参数不完整"));
            var row = _db.Set<DrillStudentReport>().FirstOrDefault(x => x.Id == id);
            if (row == null) return Json(new WebResponseContent().Error("记录不存在"));

            var u = UserContext.Current.UserInfo;
            row.ReviewScore = req.ReviewScore;
            row.ReviewComment = (req.ReviewComment ?? "").Trim();
            row.SubmitStatus = 2;
            row.ReviewerId = u.User_Id;
            row.ReviewerName = u.UserTrueName;
            row.ReviewAt = DateTime.Now;
            row.ModifyDate = DateTime.Now;
            _db.Update(row);
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "批阅完成", Data = row });
        }

        [HttpPost("ProjectResources/Save")]
        public IActionResult SaveProjectResource(int projectId, [FromBody] SaveProjectResourceRequest req)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            if (req == null || string.IsNullOrWhiteSpace(req.VideoUrl))
                return Json(new WebResponseContent().Error("视频地址不能为空"));
            var url = req.VideoUrl.Trim();
            var now = DateTime.Now;

            var current = _db.Set<DrillProjectVideoResource>()
                .FirstOrDefault(x => x.ProjectId == projectId && x.Enable == 1);
            if (current == null)
            {
                current = new DrillProjectVideoResource
                {
                    ProjectId = projectId,
                    ResourceName = string.IsNullOrWhiteSpace(req.ResourceName) ? "项目视频资源" : req.ResourceName.Trim(),
                    VideoUrl = url,
                    Enable = 1,
                    CreateDate = now,
                    ModifyDate = now
                };
                _db.Add(current);
            }
            else
            {
                current.ResourceName = string.IsNullOrWhiteSpace(req.ResourceName) ? current.ResourceName : req.ResourceName.Trim();
                current.VideoUrl = url;
                current.Enable = req.Enable ?? 1;
                current.ModifyDate = now;
                _db.Update(current);
            }
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "项目资源已保存", Data = current });
        }

        [HttpPost("ProjectResources/SaveBatch")]
        public IActionResult SaveProjectResourcesBatch(int projectId, [FromBody] SaveProjectResourcesRequest req)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var items = (req?.Items ?? new List<SaveProjectResourceItem>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.VideoUrl))
                .ToList();
            if (!items.Any()) return Json(new WebResponseContent().Error("请至少配置一个视频资源"));

            var now = DateTime.Now;
            var old = _db.Set<DrillProjectVideoResource>().Where(x => x.ProjectId == projectId).ToList();
            if (old.Any()) _db.RemoveRange(old);
            _db.SaveChanges();

            var rows = new List<DrillProjectVideoResource>();
            foreach (var item in items)
            {
                rows.Add(new DrillProjectVideoResource
                {
                    ProjectId = projectId,
                    ResourceName = string.IsNullOrWhiteSpace(item.ResourceName) ? "项目视频资源" : item.ResourceName.Trim(),
                    VideoUrl = item.VideoUrl.Trim(),
                    Enable = item.Enable.HasValue ? (item.Enable.Value == 1 ? 1 : 0) : 1,
                    CreateDate = now,
                    ModifyDate = now
                });
            }

            if (!rows.Any(x => x.Enable == 1)) rows[0].Enable = 1;

            _db.AddRange(rows);
            _db.SaveChanges();

            var result = rows
                .Select((x, idx) => new
                {
                    id = x.Id,
                    resourceName = x.ResourceName,
                    videoUrl = x.VideoUrl,
                    enable = x.Enable,
                    clientKey = items[idx].ClientKey
                })
                .ToList();
            return Json(new WebResponseContent(true) { Message = "项目资源已保存", Data = result });
        }

        [HttpPost("ProjectResources/UploadVideo")]
        [JWTAuthorize]
        public IActionResult UploadProjectVideo(IFormFile file)
        {
            if (file == null || file.Length == 0) return Json(new WebResponseContent().Error("请上传视频文件"));

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? ".mp4";
            var allow = new[] { ".mp4", ".webm", ".ogg", ".mov", ".m4v" };
            if (!allow.Contains(ext)) return Json(new WebResponseContent().Error("仅支持 mp4/webm/ogg/mov/m4v"));

            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Upload", "DrillProjectVideo");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            var url = $"/Upload/DrillProjectVideo/{fileName}";
            return Json(new WebResponseContent(true) { Data = url, Message = "上传成功" });
        }

        private List<DrillFlowNode> EnsureProjectNodesForVideoConfig(int projectId)
        {
            var projectNodes = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == projectId)
                .OrderBy(x => x.OrderNo).ThenBy(x => x.Id)
                .ToList();
            if (projectNodes.Any()) return projectNodes;

            var globalNodes = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == null)
                .OrderBy(x => x.OrderNo).ThenBy(x => x.Id)
                .ToList();
            if (!globalNodes.Any()) return projectNodes;

            var now = DateTime.Now;
            projectNodes = globalNodes.Select(x => new DrillFlowNode
            {
                ProjectId = projectId,
                NodeCode = x.NodeCode,
                NodeName = x.NodeName,
                Stage = x.Stage,
                OrderNo = x.OrderNo,
                NextNodeCode = x.NextNodeCode,
                Description = x.Description,
                ResourceId = x.ResourceId,
                VideoStartSeconds = x.VideoStartSeconds,
                VideoEndSeconds = x.VideoEndSeconds,
                Enable = 1,
                CreateDate = now
            }).ToList();

            _db.AddRange(projectNodes);
            _db.SaveChanges();
            return projectNodes;
        }

        [HttpGet("Flow/Assignments")]
        public IActionResult GetFlowAssignments(int projectId, string nodeCode = null)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var activeNodeCode = string.IsNullOrWhiteSpace(nodeCode) ? GetCurrentNodeCode(projectId) : nodeCode.Trim();
            if (string.IsNullOrWhiteSpace(activeNodeCode))
                return Json(new WebResponseContent(true) { Data = new List<object>() });

            var roleTaskMap = _db.Set<DrillRole>()
                .Where(x => x.Enable == 1 && !string.IsNullOrWhiteSpace(x.RoleName))
                .OrderBy(x => x.RoleNo).ThenBy(x => x.Id)
                .Select(x => new
                {
                    roleName = x.RoleName,
                    taskBookJson = x.TaskBookJson
                })
                .ToList();
            var roleTaskTextMap = roleTaskMap.ToDictionary(
                x => x.roleName ?? "",
                x => ParseRoleTaskItems(x.taskBookJson),
                StringComparer.OrdinalIgnoreCase
            );

            var roles = GetAllowedRolesForNode(projectId, activeNodeCode).ToList();
            var rows = new List<object>();
            var index = 1L;
            foreach (var role in roles)
            {
                var taskTexts = roleTaskTextMap.ContainsKey(role) ? roleTaskTextMap[role] : new List<string>();
                if (taskTexts.Count > 0)
                {
                    foreach (var taskText in taskTexts)
                    {
                        rows.Add(new
                        {
                            Id = index++,
                            ProjectId = projectId,
                            NodeCode = activeNodeCode,
                            RoleName = role,
                            TaskTitle = taskText,
                            TaskDetail = "",
                            StepsJson = "[]",
                            SubmitType = "text",
                            EvidenceRequired = 0,
                            OrderNo = index,
                            Enable = 1
                        });
                    }
                    continue;
                }
                rows.Add(new
                {
                    Id = index++,
                    ProjectId = projectId,
                    NodeCode = activeNodeCode,
                    RoleName = role,
                    TaskTitle = "未配置任务书内容",
                    TaskDetail = "",
                    StepsJson = "[]",
                    SubmitType = "text",
                    EvidenceRequired = 0,
                    OrderNo = index,
                    Enable = 1
                });
            }
            return Json(new WebResponseContent(true) { Data = rows });
        }

        [HttpGet("Flow/Progress")]
        public IActionResult GetFlowProgress(int projectId, string nodeCode = null)
        {
            var q = _db.Set<DrillTaskAction>().Where(x => x.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(nodeCode)) q = q.Where(x => x.NodeCode == nodeCode);
            var actions = q.OrderByDescending(x => x.Id).Take(1000).ToList();
            var grouped = actions
                .GroupBy(x => new { x.NodeCode, x.RoleName, x.AssignmentId, x.TaskTitle })
                .Select(g => new
                {
                    nodeCode = g.Key.NodeCode,
                    roleName = g.Key.RoleName,
                    assignmentId = g.Key.AssignmentId,
                    taskTitle = g.Key.TaskTitle,
                    lastStatus = g.First().Status,
                    lastUser = g.First().UserName,
                    lastOccurAt = g.First().OccurAt,
                    submitCount = g.Count()
                })
                .OrderBy(x => x.nodeCode)
                .ThenBy(x => x.roleName)
                .ThenBy(x => x.assignmentId)
                .ToList();
            return Json(new WebResponseContent(true) { Data = grouped });
        }

        [HttpGet("Flow/Actions")]
        public IActionResult GetFlowActions(int projectId, string nodeCode = null, int? userId = null)
        {
            var q = _db.Set<DrillTaskAction>().Where(x => x.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(nodeCode)) q = q.Where(x => x.NodeCode == nodeCode);
            if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
            var list = q.OrderByDescending(x => x.Id).Take(500).ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpPost("Flow/Action/Submit")]
        public IActionResult SubmitFlowAction([FromBody] DrillTaskActionSubmitRequest req)
        {
            if (req == null || req.ProjectId <= 0 || string.IsNullOrWhiteSpace(req.NodeCode))
                return Json(new WebResponseContent().Error("参数不完整"));

            var now = DateTime.Now;
            var u = UserContext.Current.UserInfo;
            var action = new DrillTaskAction
            {
                ProjectId = req.ProjectId,
                NodeCode = req.NodeCode?.Trim(),
                AssignmentId = req.AssignmentId,
                RoleName = string.IsNullOrWhiteSpace(req.RoleName) ? u.RoleName : req.RoleName.Trim(),
                TaskTitle = req.TaskTitle?.Trim(),
                StepResultJson = req.StepResult == null ? null : req.StepResult.ToString(Formatting.None),
                TextContent = req.TextContent?.Trim(),
                EvidenceJson = req.Evidence == null ? null : req.Evidence.ToString(Formatting.None),
                Status = req.Status <= 0 ? 1 : req.Status,
                UserId = u.User_Id,
                UserName = u.UserTrueName,
                OccurAt = now,
                CreateDate = now
            };
            _db.Add(action);
            _db.SaveChanges();

            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == req.ProjectId);
            _db.Add(new DrillEvent
            {
                ProjectId = req.ProjectId,
                Stage = state?.CurrentStage ?? "scene",
                EventType = "teacher",
                Title = req.TaskTitle ?? "任务动作提交",
                Content = req.TextContent ?? "教师提交任务动作",
                OccurAt = now,
                UserId = u.User_Id,
                UserName = u.UserTrueName,
                RoleName = action.RoleName,
                CreateDate = now
            });
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "提交成功", Data = action });
        }

        private List<ProjectMemberViewDto> QueryProjectMembers(int projectId, int? auditStatus)
        {
            var memberQuery = _db.Set<DrillProjectMember>().Where(x => x.ProjectId == projectId);
            if (auditStatus.HasValue) memberQuery = memberQuery.Where(x => x.AuditStatus == auditStatus.Value);
            var members = memberQuery.OrderBy(x => x.UserName).ThenBy(x => x.Id).Take(2000).ToList();
            if (!members.Any()) return new List<ProjectMemberViewDto>();

            var userIds = members.Where(x => x.UserId.HasValue).Select(x => x.UserId.Value).Distinct().ToList();
            var userNames = members
                .Select(x => x.UserName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var users = _db.Set<Sys_User>()
                .Where(u => userIds.Contains(u.User_Id) || userNames.Contains(u.UserName))
                .ToList();
            var userById = users.ToDictionary(u => u.User_Id);
            var userByName = users
                .Where(u => !string.IsNullOrWhiteSpace(u.UserName))
                .GroupBy(u => u.UserName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            return members.Select(m => MapProjectMemberView(m, userById, userByName)).ToList();
        }

        private static ProjectMemberViewDto MapProjectMemberView(
            DrillProjectMember member,
            Dictionary<int, Sys_User> userById,
            Dictionary<string, Sys_User> userByName)
        {
            Sys_User user = null;
            if (member.UserId.HasValue && userById.TryGetValue(member.UserId.Value, out var byId)) user = byId;
            else if (!string.IsNullOrWhiteSpace(member.UserName)
                     && userByName.TryGetValue(member.UserName.Trim(), out var byName)) user = byName;

            var contact = member.Contact;
            if (string.IsNullOrWhiteSpace(contact) && user != null)
            {
                contact = !string.IsNullOrWhiteSpace(user.Mobile) ? user.Mobile
                    : !string.IsNullOrWhiteSpace(user.PhoneNo) ? user.PhoneNo
                    : user.Tel;
            }

            return new ProjectMemberViewDto
            {
                Id = member.Id,
                ProjectId = member.ProjectId,
                UserId = user?.User_Id ?? member.UserId,
                UserName = user?.UserName ?? member.UserName,
                UserTrueName = user?.UserTrueName ?? member.UserTrueName,
                SysRoleId = user?.Role_Id,
                SysRoleName = user?.RoleName,
                RoleName = member.RoleName,
                Org = member.Org,
                JobTitle = member.JobTitle,
                Contact = contact,
                Photo = member.Photo,
                AuditStatus = member.AuditStatus,
                SignedAt = member.SignedAt,
                CreateDate = user?.CreateDate ?? member.CreateDate,
                Enable = user?.Enable
            };
        }
    }

    public class SendMessageBody
    {
        public string content { get; set; }
        public long? parentMessageId { get; set; }
        public string nodeCode { get; set; }
        public bool? selfInitiated { get; set; }
    }

    public class ProjectMemberViewDto
    {
        public long Id { get; set; }
        public int ProjectId { get; set; }
        public int? UserId { get; set; }
        public string UserName { get; set; }
        public string UserTrueName { get; set; }
        public int? SysRoleId { get; set; }
        public string SysRoleName { get; set; }
        public string RoleName { get; set; }
        public string Org { get; set; }
        public string JobTitle { get; set; }
        public string Contact { get; set; }
        public string Photo { get; set; }
        public int AuditStatus { get; set; }
        public DateTime? SignedAt { get; set; }
        public DateTime? CreateDate { get; set; }
        public byte? Enable { get; set; }
    }

    public class DrillTaskActionSubmitRequest
    {
        public int ProjectId { get; set; }
        public string NodeCode { get; set; }
        public long? AssignmentId { get; set; }
        public string RoleName { get; set; }
        public string TaskTitle { get; set; }
        public JToken StepResult { get; set; }
        public string TextContent { get; set; }
        public JToken Evidence { get; set; }
        public int Status { get; set; } = 1;
    }

    public class UpdateMemberRoleRequest
    {
        public string RoleName { get; set; }
    }

    public class BatchUpdateMemberRoleRequest
    {
        public int? ProjectId { get; set; }
        public List<long> Ids { get; set; }
        public string RoleName { get; set; }
    }

    public class ImportMembersSimpleRequest
    {
        public List<ImportMembersSimpleItem> Items { get; set; } = new List<ImportMembersSimpleItem>();
    }

    public class ImportMembersSimpleItem
    {
        public string UserName { get; set; }
        public string UserTrueName { get; set; }
    }

    public class SaveNodeVideoTimesRequest
    {
        public List<NodeVideoTimeItem> Items { get; set; } = new List<NodeVideoTimeItem>();
    }

    public class NodeVideoTimeItem
    {
        public string NodeCode { get; set; }
        public int? VideoStartSeconds { get; set; }
        public int? VideoEndSeconds { get; set; }
    }

    public class SaveProjectResourceRequest
    {
        public string ResourceName { get; set; }
        public string VideoUrl { get; set; }
        public int? Enable { get; set; } = 1;
    }

    public class SaveProjectResourcesRequest
    {
        public List<SaveProjectResourceItem> Items { get; set; } = new List<SaveProjectResourceItem>();
    }

    public class SaveProjectResourceItem
    {
        public string ClientKey { get; set; }
        public string ResourceName { get; set; }
        public string VideoUrl { get; set; }
        public int? Enable { get; set; } = 1;
        public int? OrderNo { get; set; }
    }

    public class ReviewStudentReportRequest
    {
        public int? ReviewScore { get; set; }
        public string ReviewComment { get; set; }
    }

    public class SaveFlowNodesRequest
    {
        public List<SaveFlowNodeItem> Items { get; set; } = new List<SaveFlowNodeItem>();
    }

    public class SaveFlowNodeItem
    {
        public string NodeCode { get; set; }
        public string NodeName { get; set; }
        public string Stage { get; set; }
        public string Description { get; set; }
        public long? ResourceId { get; set; }
        public int? VideoStartSeconds { get; set; }
        public int? VideoEndSeconds { get; set; }
    }
}

