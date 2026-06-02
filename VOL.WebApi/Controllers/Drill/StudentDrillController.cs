using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VOL.Core.Configuration;
using VOL.Core.Controllers.Basic;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions;
using VOL.Core.Filters;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels;
using VOL.Entity.DomainModels.Drill;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VOL.WebApi.Services;
using System.IO;

namespace VOL.WebApi.Controllers.Drill
{
    /// <summary>
    /// 学员端接口（小程序）：登录、注册、签到等，无需验证码
    /// </summary>
    [Route("api/Drill/Student")]
    public class StudentDrillController : VolController
    {
        private readonly VOLContext _db;
        private readonly XunfeiIatService _iatService;

        public StudentDrillController(VOLContext db, XunfeiIatService iatService)
        {
            _db = db;
            _iatService = iatService;
        }

        /// <summary>
        /// 学员注册。注册后待管理员审核，审核通过后项目可用。
        /// </summary>
        [HttpPost("Register"), AllowAnonymous]
        public IActionResult Register([FromBody] StudentRegisterInfo reg)
        {
            if (reg == null) return Json(new WebResponseContent().Error("参数不能为空"));
            if (string.IsNullOrWhiteSpace(reg.ProjectCode) || string.IsNullOrWhiteSpace(reg.RoleName)
                || string.IsNullOrWhiteSpace(reg.UserName) || string.IsNullOrWhiteSpace(reg.Password)
                || string.IsNullOrWhiteSpace(reg.UserTrueName))
                return Json(new WebResponseContent().Error("项目编号、角色、用户名、密码、真实姓名不能为空"));
            if (reg.Password.Trim().Length < 6)
                return Json(new WebResponseContent().Error("密码长度不能少于6位"));

            var project = _db.Set<Project>().FirstOrDefault(x => x.Code == reg.ProjectCode.Trim());
            if (project == null) return Json(new WebResponseContent().Error("项目编号不存在"));

            var drillRole = _db.Set<DrillRole>()
                .FirstOrDefault(x => x.Enable == 1 && x.RoleName == reg.RoleName.Trim());
            if (drillRole == null) return Json(new WebResponseContent().Error("角色不存在或未启用"));

            var exists = _db.Set<DrillProjectMember>()
                .Any(x => x.ProjectId == project.Id && x.UserName == reg.UserName.Trim());
            if (exists) return Json(new WebResponseContent().Error("该项目下该用户名已注册"));

            // 若 sys_user 中已有该用户名，需校验密码并仅创建成员记录；否则创建 sys_user（学生角色）并关联
            var user = _db.Set<Sys_User>().FirstOrDefault(x => x.UserName == reg.UserName.Trim());
            if (user != null && reg.Password.Trim().EncryptDES(AppSetting.Secret.User) != (user.UserPwd ?? ""))
                return Json(new WebResponseContent().Error("该用户名已存在，密码不正确"));
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
                    UserName = reg.UserName.Trim(),
                    UserPwd = reg.Password.Trim().EncryptDES(AppSetting.Secret.User),
                    UserTrueName = reg.UserTrueName?.Trim(),
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
                ProjectId = project.Id,
                UserId = user.User_Id,
                UserName = reg.UserName.Trim(),
                RoleName = drillRole.RoleName,
                UserTrueName = reg.UserTrueName?.Trim(),
                Contact = reg.Contact?.Trim(),
                AuditStatus = 0,
                CreateDate = DateTime.Now
            };
            _db.Add(member);
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "注册成功，请等待管理员审核" });
        }

        /// <summary>
        /// 学员登录。需项目编号、用户名、密码，且该成员已审核通过。
        /// </summary>
        [HttpPost("Login"), AllowAnonymous]
        public IActionResult Login([FromBody] StudentLoginInfo login)
        {
            if (login == null) return Json(new WebResponseContent().Error("参数不能为空"));
            if (string.IsNullOrWhiteSpace(login.ProjectCode) || string.IsNullOrWhiteSpace(login.UserName)
                || string.IsNullOrWhiteSpace(login.Password))
                return Json(new WebResponseContent().Error("项目编号、用户名、密码不能为空"));

            var project = _db.Set<Project>().FirstOrDefault(x => x.Code == login.ProjectCode.Trim());
            if (project == null) return Json(new WebResponseContent().Error("项目编号不存在"));

            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == project.Id && x.UserName == login.UserName.Trim());
            if (member == null) return Json(new WebResponseContent().Error("该项目下未找到该用户"));
            if (member.AuditStatus != 1) return Json(new WebResponseContent().Error("账号尚未审核通过，请等待管理员审核"));

            Sys_User user;
            if (member.UserId.HasValue)
                user = _db.Set<Sys_User>().FirstOrDefault(x => x.User_Id == member.UserId);
            else
                user = _db.Set<Sys_User>().FirstOrDefault(x => x.UserName == login.UserName.Trim());
            if (user == null) return Json(new WebResponseContent().Error("用户不存在"));
            if (login.Password.Trim().EncryptDES(AppSetting.Secret.User) != (user.UserPwd ?? ""))
                return Json(new WebResponseContent().Error("密码错误"));
            if (user.Enable != 1) return Json(new WebResponseContent().Error("账号已禁用"));

            var token = JwtHelper.IssueJwt(new UserInfo
            {
                User_Id = user.User_Id,
                UserName = user.UserName,
                UserTrueName = member.UserTrueName ?? user.UserTrueName,
                Role_Id = user.Role_Id,
                RoleName = member.RoleName ?? user.RoleName ?? ""
            });

            var data = new
            {
                token,
                userName = member.UserTrueName ?? user.UserTrueName,
                roleName = member.RoleName ?? user.RoleName ?? "",
                projectId = project.Id,
                projectName = project.Name,
                projectCode = project.Code
            };
            return Json(new WebResponseContent(true) { Data = data });
        }

        /// <summary>
        /// 获取项目可选角色列表（供注册/登录时选择）
        /// </summary>
        [HttpGet("Roles"), AllowAnonymous]
        public IActionResult GetRoles()
        {
            var roles = _db.Set<DrillRole>()
                .Where(x => x.Enable == 1)
                .OrderBy(x => x.RoleNo)
                .Select(x => x.RoleName)
                .ToList();
            return Json(new WebResponseContent(true) { Data = roles });
        }

        /// <summary>
        /// 获取当前用户在当前项目的角色任务书信息（全局角色）
        /// </summary>
        [HttpGet("RoleMe")]
        [JWTAuthorize]
        public IActionResult RoleMe(int projectId)
        {
            var u = UserContext.Current.UserInfo;
            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));

            var role = _db.Set<DrillRole>()
                .FirstOrDefault(x => x.Enable == 1 && x.RoleName == member.RoleName);
            if (role == null) return Json(new WebResponseContent().Error("未找到对应角色任务书"));

            var data = new
            {
                roleNo = role.RoleNo,
                roleName = role.RoleName,
                marker = role.Marker,
                taskBookJson = role.TaskBookJson ?? "",
                signedAt = member.SignedAt,
                taskBookVisible = GetTaskBookVisible(projectId),
                customChecklistAlwaysVisible = true
            };
            return Json(new WebResponseContent(true) { Data = data });
        }

        /// <summary>
        /// 角色任务打卡（每个项目+用户+角色仅允许一次）
        /// </summary>
        [HttpPost("RolePunch")]
        [JWTAuthorize]
        public IActionResult RolePunch(int projectId, [FromBody] RolePunchRequest req)
        {
            if (req == null) return Json(new WebResponseContent().Error("参数不能为空"));
            var u = UserContext.Current.UserInfo;

            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));
            if (member.SignedAt == null) return Json(new WebResponseContent().Error("请先完成签到"));
            if (string.IsNullOrWhiteSpace(member.RoleName)) return Json(new WebResponseContent().Error("成员角色为空"));

            var role = _db.Set<DrillRole>()
                .FirstOrDefault(x => x.Enable == 1 && x.RoleName == member.RoleName);
            if (role == null) return Json(new WebResponseContent().Error("未找到对应角色任务书"));

            // 校验：勾选全部条目即完成（A）
            if (string.IsNullOrWhiteSpace(role.TaskBookJson))
                return Json(new WebResponseContent().Error("该角色任务书未配置"));

            HashSet<string> expectedItemIds;
            try
            {
                expectedItemIds = ExtractAllChecklistItemIds(role.TaskBookJson);
            }
            catch
            {
                return Json(new WebResponseContent().Error("任务书 JSON 结构无效，请联系管理员"));
            }
            if (expectedItemIds.Count == 0)
                return Json(new WebResponseContent().Error("任务书未配置任何条目"));

            var checkedToken = req.Answers?["checkedItemIds"];
            HashSet<string> checkedItemIds = new HashSet<string>();
            if (checkedToken != null && checkedToken.Type == JTokenType.Array)
            {
                foreach (var it in checkedToken as JArray)
                {
                    var s = it?.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                        checkedItemIds.Add(s.Trim());
                }
            }

            if (checkedItemIds.Count == 0)
                return Json(new WebResponseContent().Error("请先勾选任务条目"));

            // 必须包含全部，且数量相等（避免只勾选部分）
            if (checkedItemIds.Count != expectedItemIds.Count || !expectedItemIds.IsSubsetOf(checkedItemIds))
                return Json(new WebResponseContent().Error("需要勾选全部任务项后才允许打卡"));

            var exists = _db.Set<DrillRolePunch>()
                .Any(x => x.ProjectId == projectId && x.RoleName == member.RoleName && x.UserId == u.User_Id);
            if (exists) return Json(new WebResponseContent().Error("该角色任务书已打卡（仅允许一次）"));

            var now = DateTime.Now;
            var answersJson = req.Answers?.ToString(Formatting.None) ?? "null";

            _db.Add(new DrillRolePunch
            {
                ProjectId = projectId,
                RoleName = member.RoleName,
                UserId = u.User_Id,
                UserName = member.UserName,
                UserTrueName = member.UserTrueName ?? u.UserTrueName,
                PunchAt = now,
                ContentJson = answersJson,
                CreateDate = now
            });
            _db.SaveChanges();

            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            var stage = state?.CurrentStage ?? "scene";

            var content = string.IsNullOrWhiteSpace(req.Remark)
                ? "完成任务书打卡"
                : req.Remark.Trim();

            _db.Add(new DrillEvent
            {
                ProjectId = projectId,
                Stage = stage,
                EventType = "student",
                Title = "任务打卡",
                Content = $"{content}\n{answersJson}",
                OccurAt = now,
                UserId = u.User_Id,
                UserName = member.UserTrueName ?? u.UserTrueName,
                RoleName = member.RoleName,
                CreateDate = now
            });
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "打卡成功" });
        }

        private static HashSet<string> ExtractAllChecklistItemIds(string taskBookJson)
        {
            // 约定结构：
            // { tasks: [ { title: "...", items: [ { id: "i1", text:"..." } ] } ] }
            var jo = JObject.Parse(taskBookJson);
            var tasks = jo["tasks"] as JArray;
            if (tasks == null) throw new Exception("tasks 缺失");
            var expected = new HashSet<string>();
            foreach (var t in tasks)
            {
                var items = t?["items"] as JArray;
                if (items == null) continue;
                foreach (var it in items)
                {
                    var id = it?["id"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(id))
                        expected.Add(id.Trim());
                }
            }
            return expected;
        }

        /// <summary>
        /// 学员签到（登录后调用，需JWT）
        /// </summary>
        [HttpPost("SignIn")]
        [JWTAuthorize]
        public IActionResult SignIn(int projectId)
        {
            var u = UserContext.Current.UserInfo;
            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));
            if (member.AuditStatus != 1) return Json(new WebResponseContent().Error("账号尚未审核通过"));

            var now = DateTime.Now;
            member.SignedAt = now;
            member.ModifyDate = now;
            _db.Update(member);

            _db.Add(new DrillEvent
            {
                ProjectId = projectId,
                Stage = "scene",
                EventType = "student",
                Title = "签到",
                Content = $"{member.RoleName} {member.UserTrueName ?? u.UserTrueName} 已签到",
                OccurAt = now,
                UserId = u.User_Id,
                UserName = u.UserTrueName,
                RoleName = member.RoleName,
                CreateDate = now
            });
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "签到成功" });
        }

        /// <summary>
        /// 语音听写（讯飞流式版，后端代理）。小程序 uni.uploadFile name=file，需携带 JWT。
        /// 支持 MP3、WAV(16k 单声道 PCM)、裸 PCM。
        /// </summary>
        [HttpPost("VoiceDictation")]
        [JWTAuthorize]
        public async Task<IActionResult> VoiceDictation(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return Json(new WebResponseContent().Error("请上传录音文件"));
            try
            {
                await using var stream = file.OpenReadStream();
                var fileName = NormalizeAudioFileName(file.FileName, file.ContentType);
                Console.WriteLine($"[VoiceDictation] fileName={fileName}, rawName={file.FileName}, contentType={file.ContentType}, length={file.Length}");
                var (text, message) = await _iatService.TranscribeAsync(stream, fileName, cancellationToken);
                if (!string.IsNullOrEmpty(message))
                    return Json(new WebResponseContent().Error($"{message}（文件:{fileName}, 大小:{file.Length}字节, ContentType:{file.ContentType ?? "-"})"));
                return Json(new WebResponseContent(true) { Data = new { text = text ?? "", fileName, fileSize = file.Length } });
            }
            catch (Exception ex)
            {
                return Json(new WebResponseContent().Error($"识别异常: {ex.Message}（文件:{file.FileName ?? "-"}, 大小:{file.Length}字节）"));
            }
        }

        private static string NormalizeAudioFileName(string fileName, string contentType)
        {
            var name = string.IsNullOrWhiteSpace(fileName) ? "audio" : fileName.Trim();
            if (!string.IsNullOrWhiteSpace(global::System.IO.Path.GetExtension(name)))
                return name;

            var ct = (contentType ?? "").ToLowerInvariant();
            if (ct.Contains("wav")) return name + ".wav";
            if (ct.Contains("mpeg") || ct.Contains("mp3")) return name + ".mp3";
            if (ct.Contains("x-m4a") || ct.Contains("mp4") || ct.Contains("aac")) return name + ".m4a";
            // 未知类型不要强行指定后缀，交给后续按文件头自动识别
            return name;
        }

        /// <summary>
        /// 获取当前节点下的角色任务卡（结构化任务）
        /// </summary>
        [HttpGet("NodeTasks")]
        [JWTAuthorize]
        public IActionResult NodeTasks(int projectId, string nodeCode = null)
        {
            var u = UserContext.Current.UserInfo;
            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));

            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            var activeNodeCode = string.IsNullOrWhiteSpace(nodeCode)
                ? (state?.CurrentNodeCode ?? GetCurrentNodeCode(projectId, state?.CurrentStage))
                : nodeCode.Trim();
            var activeNodeName = GetNodeNameByCode(projectId, activeNodeCode);

            var allowedRoles = GetAllowedRolesForNode(projectId, activeNodeCode);
            var roleAllowed = !allowedRoles.Any() || allowedRoles.Contains(member.RoleName, StringComparer.OrdinalIgnoreCase);
            var role = _db.Set<DrillRole>()
                .FirstOrDefault(x => x.Enable == 1 && x.RoleName == member.RoleName);
            var roleTasks = ParseRoleTaskGroups(role?.TaskBookJson);
            var selectedTaskIds = GetSelectedTaskIdsForNodeRole(projectId, activeNodeCode, member.RoleName);
            if (selectedTaskIds.Any())
            {
                var selectedSet = new HashSet<string>(selectedTaskIds, StringComparer.OrdinalIgnoreCase);
                roleTasks = roleTasks.Where(x => selectedSet.Contains(x.Id)).ToList();
            }

            var submittedTitles = _db.Set<DrillTaskAction>()
                .Where(x => x.ProjectId == projectId
                            && x.NodeCode == activeNodeCode
                            && x.UserId == u.User_Id
                            && x.Status >= 1
                            && x.RoleName == member.RoleName)
                .Select(x => x.TaskTitle)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            var hasNodeSubmitted = _db.Set<DrillTaskAction>()
                .Any(x => x.ProjectId == projectId
                          && x.NodeCode == activeNodeCode
                          && x.UserId == u.User_Id
                          && x.RoleName == member.RoleName
                          && x.Status >= 1
                          && x.TaskTitle == "任务提交");
            var submittedSet = new HashSet<string>(
                submittedTitles.Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase
            );

            var data = new List<object>();
            if (roleAllowed)
            {
                for (var i = 0; i < roleTasks.Count; i++)
                {
                    var group = roleTasks[i];
                    var items = group.Items?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
                    if (!items.Any())
                    {
                        items.Add(string.IsNullOrWhiteSpace(group.Title) ? $"角色任务{i + 1}" : group.Title);
                    }
                    for (var j = 0; j < items.Count; j++)
                    {
                        var title = items[j];
                        data.Add(new
                        {
                            id = $"{group.Id}_{j + 1}",
                            nodeCode = activeNodeCode,
                            roleName = member.RoleName,
                            taskTitle = title,
                            taskDetail = "",
                            submitType = "text",
                            evidenceRequired = 0,
                            stepsJson = "[]",
                            done = hasNodeSubmitted || submittedSet.Contains(title)
                        });
                    }
                }
            }

            return Json(new WebResponseContent(true)
            {
                Data = new
                {
                    nodeCode = activeNodeCode,
                    nodeName = activeNodeName,
                    tasks = data,
                    roleAllowed,
                    allowedRoles = allowedRoles.ToList()
                }
            });
        }

        /// <summary>
        /// 学员提交节点任务动作（文本/语音/图片识别结果等）
        /// </summary>
        [HttpPost("NodeTaskSubmit")]
        [JWTAuthorize]
        public IActionResult NodeTaskSubmit(int projectId, [FromBody] StudentNodeTaskSubmitRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.NodeCode))
                return Json(new WebResponseContent().Error("参数不完整"));

            var u = UserContext.Current.UserInfo;
            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));
            if (member.SignedAt == null) return Json(new WebResponseContent().Error("请先完成签到"));

            var taskTitle = req.TaskTitle?.Trim();
            if (string.IsNullOrWhiteSpace(taskTitle))
                return Json(new WebResponseContent().Error("任务标题不能为空"));

            var allowedRoles = GetAllowedRolesForNode(projectId, req.NodeCode?.Trim());
            var roleAllowed = !allowedRoles.Any() || allowedRoles.Contains(member.RoleName, StringComparer.OrdinalIgnoreCase);
            if (!roleAllowed && req.SelfInitiated != true)
                return Json(new WebResponseContent().Error("当前节点未激活该身份组提交权限，请选择自主发言"));
            var existsNodeSubmit = _db.Set<DrillTaskAction>()
                .Any(x => x.ProjectId == projectId
                          && x.NodeCode == req.NodeCode.Trim()
                          && x.UserId == u.User_Id
                          && x.RoleName == member.RoleName
                          && x.Status >= 1
                          && x.TaskTitle == "任务提交");
            if (existsNodeSubmit) return Json(new WebResponseContent().Error("当前节点任务已提交，无需重复提交"));

            var now = DateTime.Now;
            var action = new DrillTaskAction
            {
                ProjectId = projectId,
                NodeCode = req.NodeCode.Trim(),
                AssignmentId = null,
                RoleName = member.RoleName,
                TaskTitle = taskTitle,
                StepResultJson = req.StepResult == null ? null : req.StepResult.ToString(Formatting.None),
                TextContent = req.SelfInitiated == true ? $"[自主发言] {(req.TextContent ?? "").Trim()}" : req.TextContent?.Trim(),
                EvidenceJson = req.Evidence == null ? null : req.Evidence.ToString(Formatting.None),
                Status = 1,
                UserId = u.User_Id,
                UserName = member.UserTrueName ?? u.UserTrueName,
                OccurAt = now,
                CreateDate = now
            };
            _db.Add(action);
            _db.SaveChanges();

            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            _db.Add(new DrillEvent
            {
                ProjectId = projectId,
                Stage = state?.CurrentStage ?? "scene",
                EventType = "student",
                Title = $"任务提交：{taskTitle}",
                Content = req.TextContent ?? "学员提交任务动作",
                OccurAt = now,
                UserId = u.User_Id,
                UserName = member.UserTrueName ?? u.UserTrueName,
                RoleName = member.RoleName,
                CreateDate = now
            });
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "提交成功", Data = action });
        }

        private List<RoleTaskGroupDto> ParseRoleTaskGroups(string taskBookJson)
        {
            var result = new List<RoleTaskGroupDto>();
            if (string.IsNullOrWhiteSpace(taskBookJson)) return result;
            try
            {
                var jo = JObject.Parse(taskBookJson);
                var tasks = jo["tasks"] as JArray;
                if (tasks == null) return result;
                for (var i = 0; i < tasks.Count; i++)
                {
                    var t = tasks[i];
                    var id = t?["id"]?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(id)) id = $"task_{i + 1}";
                    var title = t?["title"]?.ToString()?.Trim();
                    var items = t?["items"] as JArray;
                    var list = new List<string>();
                    if (items != null)
                    {
                        foreach (var it in items)
                        {
                            var txt = it?["text"]?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(txt)) list.Add(txt);
                        }
                    }
                    result.Add(new RoleTaskGroupDto
                    {
                        Id = id,
                        Title = string.IsNullOrWhiteSpace(title) ? $"角色任务{i + 1}" : title,
                        Items = list
                    });
                }
            }
            catch
            {
                // ignore parse errors and return empty list
            }
            return result;
        }

        private List<string> GetSelectedTaskIdsForNodeRole(int projectId, string nodeCode, string roleName)
        {
            if (projectId <= 0 || string.IsNullOrWhiteSpace(nodeCode) || string.IsNullOrWhiteSpace(roleName))
                return new List<string>();
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null || string.IsNullOrWhiteSpace(state.SettingsJson))
                return new List<string>();
            try
            {
                var jo = JObject.Parse(state.SettingsJson);
                var selections = jo["nodeRoleTaskSelections"] as JObject;
                if (selections == null) return new List<string>();
                var nodeMap = selections[nodeCode] as JObject;
                if (nodeMap == null)
                {
                    var nodeKey = selections.Properties()
                        .FirstOrDefault(p => string.Equals(p.Name?.Trim(), nodeCode.Trim(), StringComparison.OrdinalIgnoreCase))
                        ?.Name;
                    nodeMap = string.IsNullOrWhiteSpace(nodeKey) ? null : (selections[nodeKey] as JObject);
                }
                if (nodeMap == null) return new List<string>();
                var roleToken = nodeMap[roleName];
                if (roleToken == null)
                {
                    var roleKey = nodeMap.Properties()
                        .FirstOrDefault(p => string.Equals(p.Name?.Trim(), roleName.Trim(), StringComparison.OrdinalIgnoreCase))
                        ?.Name;
                    roleToken = string.IsNullOrWhiteSpace(roleKey) ? null : nodeMap[roleKey];
                }
                if (!(roleToken is JArray arr)) return new List<string>();
                return arr.Select(x => x?.ToString()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        [HttpPost("Taskbook/Submit")]
        [JWTAuthorize]
        public IActionResult SubmitTaskbook(int projectId, [FromBody] StudentTaskbookSubmitRequest req)
        {
            if (projectId <= 0 || req == null) return Json(new WebResponseContent().Error("参数不完整"));
            var selected = (req.SelectedItems ?? new List<string>())
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            if (!selected.Any()) return Json(new WebResponseContent().Error("请至少选择一项"));

            var u = UserContext.Current.UserInfo;
            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));

            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            var nodeCode = string.IsNullOrWhiteSpace(req.NodeCode) ? state?.CurrentNodeCode : req.NodeCode.Trim();
            if (string.IsNullOrWhiteSpace(nodeCode)) nodeCode = GetCurrentNodeCode(projectId, state?.CurrentStage);
            var existsNodeSubmit = _db.Set<DrillTaskAction>()
                .Any(x => x.ProjectId == projectId
                          && x.NodeCode == nodeCode
                          && x.UserId == u.User_Id
                          && x.RoleName == member.RoleName
                          && x.Status >= 1
                          && x.TaskTitle == "任务提交");
            if (existsNodeSubmit) return Json(new WebResponseContent().Error("当前节点任务已提交，无需重复提交"));

            var now = DateTime.Now;
            var text = string.Join("\n", selected);
            var action = new DrillTaskAction
            {
                ProjectId = projectId,
                NodeCode = nodeCode,
                AssignmentId = null,
                RoleName = member.RoleName,
                TaskTitle = "任务提交",
                TextContent = text,
                EvidenceJson = req.CustomItems == null ? null : JArray.FromObject(req.CustomItems).ToString(Formatting.None),
                Status = 1,
                UserId = u.User_Id,
                UserName = member.UserTrueName ?? u.UserTrueName,
                OccurAt = now,
                CreateDate = now
            };
            _db.Add(action);
            _db.SaveChanges();
            return Json(new WebResponseContent(true) { Message = "提交成功", Data = action });
        }

        /// <summary>
        /// 学员提交事故报告/恢复报告（教师结束演练后）
        /// </summary>
        [HttpPost("Report/UploadImage")]
        [JWTAuthorize]
        public IActionResult UploadReportImage(IFormFile file)
        {
            if (file == null || file.Length == 0) return Json(new WebResponseContent().Error("请上传图片"));

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? ".jpg";
            var allow = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };
            if (!allow.Contains(ext)) return Json(new WebResponseContent().Error("仅支持 jpg/jpeg/png/webp/gif/bmp"));

            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Upload", "DrillReportImage");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            var url = $"/Upload/DrillReportImage/{fileName}";
            return Json(new WebResponseContent(true) { Data = url, Message = "上传成功" });
        }

        /// <summary>
        /// 学员提交事故报告/恢复报告（教师结束演练后）
        /// </summary>
        [HttpPost("Report/Submit")]
        [JWTAuthorize]
        public IActionResult SubmitReport(int projectId, [FromBody] StudentReportSubmitRequest req)
        {
            if (projectId <= 0 || req == null) return Json(new WebResponseContent().Error("参数不完整"));
            if (string.IsNullOrWhiteSpace(req.Content)) return Json(new WebResponseContent().Error("报告内容不能为空"));
            // 统一为单一类型：演练总结（summary），兼容历史传入 report/recovery
            var inputType = string.IsNullOrWhiteSpace(req.ReportType) ? "summary" : req.ReportType.Trim().ToLower();
            if (inputType != "summary" && inputType != "report" && inputType != "recovery")
                return Json(new WebResponseContent().Error("报告类型无效"));
            var reportType = "summary";

            var u = UserContext.Current.UserInfo;
            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));

            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null || state.Status != 3) return Json(new WebResponseContent().Error("演练未结束，暂不可提交总结报告"));

            var now = DateTime.Now;
            var old = _db.Set<DrillStudentReport>()
                .Where(x => x.ProjectId == projectId && x.UserId == u.User_Id
                            && (x.ReportType == "summary" || x.ReportType == "report" || x.ReportType == "recovery"))
                .OrderByDescending(x => x.ModifyDate ?? x.CreateDate)
                .FirstOrDefault();
            if (old != null) return Json(new WebResponseContent().Error("演练总结仅允许提交一次"));

            old = new DrillStudentReport
            {
                ProjectId = projectId,
                UserId = u.User_Id,
                UserName = member.UserName ?? u.UserName,
                UserTrueName = member.UserTrueName ?? u.UserTrueName,
                RoleName = member.RoleName,
                ReportType = reportType,
                Title = string.IsNullOrWhiteSpace(req.Title) ? "演练总结" : req.Title.Trim(),
                Content = req.Content.Trim(),
                ExtraJson = req.ExtraJson,
                SubmitStatus = 1,
                ReviewScore = null,
                ReviewComment = null,
                ReviewerId = null,
                ReviewerName = null,
                ReviewAt = null,
                CreateDate = now,
                ModifyDate = now
            };
            _db.Add(old);
            _db.SaveChanges();

            _db.Add(new DrillEvent
            {
                ProjectId = projectId,
                Stage = "report",
                EventType = "student",
                Title = "提交演练总结",
                Content = $"[{member.RoleName}] {member.UserTrueName ?? member.UserName} 已提交",
                OccurAt = now,
                UserId = u.User_Id,
                UserName = member.UserTrueName ?? u.UserTrueName,
                RoleName = member.RoleName,
                CreateDate = now
            });
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "提交成功", Data = old });
        }

        [HttpGet("Report/My")]
        [JWTAuthorize]
        public IActionResult GetMyReports(int projectId, string reportType = null)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var u = UserContext.Current.UserInfo;
            var q = _db.Set<DrillStudentReport>()
                .Where(x => x.ProjectId == projectId && x.UserId == u.User_Id);
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
            var list = q.OrderByDescending(x => x.CreateDate).ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpGet("Dialog/RoleTargets")]
        [JWTAuthorize]
        public IActionResult GetDialogRoleTargets(int projectId)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var u = UserContext.Current.UserInfo;
            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));

            // 目标身份组统一从 drill_role 读取，避免受项目成员列表波动影响
            var roleList = _db.Set<DrillRole>()
                .Where(x => x.Enable == 1 && !string.IsNullOrWhiteSpace(x.RoleName))
                .OrderBy(x => x.RoleNo).ThenBy(x => x.Id)
                .Select(x => x.RoleName)
                .Distinct()
                .ToList()
                .Where(x => !string.Equals(x, member.RoleName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x)
                .ToList();
            if (!roleList.Any())
            {
                // 兼容兜底：若未配置 drill_role，则退回项目成员身份组
                roleList = _db.Set<DrillProjectMember>()
                    .Where(x => x.ProjectId == projectId && x.AuditStatus == 1 && !string.IsNullOrWhiteSpace(x.RoleName))
                    .Select(x => x.RoleName)
                    .Distinct()
                    .ToList()
                    .Where(x => !string.Equals(x, member.RoleName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x)
                    .ToList();
            }
            return Json(new WebResponseContent(true) { Data = roleList });
        }

        [HttpPost("Dialog/Send")]
        [JWTAuthorize]
        public IActionResult SendDialogMessage(int projectId, [FromBody] StudentDialogSendRequest req)
        {
            if (projectId <= 0 || req == null) return Json(new WebResponseContent().Error("参数不完整"));
            if (string.IsNullOrWhiteSpace(req.ToRoleName) || string.IsNullOrWhiteSpace(req.Content))
                return Json(new WebResponseContent().Error("目标身份组与消息内容不能为空"));

            var u = UserContext.Current.UserInfo;
            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));

            var toRole = req.ToRoleName.Trim();
            var roleExists = _db.Set<DrillRole>()
                .Any(x => x.Enable == 1 && x.RoleName == toRole);
            if (!roleExists)
            {
                // 兼容兜底：旧数据未维护 drill_role 时，允许项目成员中的身份组
                roleExists = _db.Set<DrillProjectMember>()
                    .Any(x => x.ProjectId == projectId && x.AuditStatus == 1 && x.RoleName == toRole);
            }
            if (!roleExists) return Json(new WebResponseContent().Error("目标身份组不存在"));
            if (string.Equals(member.RoleName, toRole, StringComparison.OrdinalIgnoreCase))
                return Json(new WebResponseContent().Error("不能给本身份组发送对话"));

            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            var nodeCode = state?.CurrentNodeCode ?? GetCurrentNodeCode(projectId, state?.CurrentStage);
            var now = DateTime.Now;

            var contentObj = new JObject
            {
                ["toRoleName"] = toRole,
                ["text"] = req.Content.Trim(),
                ["fromRoleName"] = member.RoleName ?? "",
                ["fromUserName"] = member.UserTrueName ?? u.UserTrueName
            };

            var msg = new DrillMessage
            {
                ProjectId = projectId,
                Channel = "speech",
                ParentMessageId = null,
                NodeCode = nodeCode,
                Content = contentObj.ToString(Formatting.None),
                UserId = u.User_Id,
                UserName = member.UserTrueName ?? u.UserTrueName,
                RoleName = member.RoleName,
                CreateDate = now
            };
            _db.Add(msg);
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "发送成功", Data = msg });
        }

        [HttpGet("Dialog/Inbox")]
        [JWTAuthorize]
        public IActionResult GetDialogInbox(int projectId, long lastId = 0)
        {
            if (projectId <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            var u = UserContext.Current.UserInfo;
            var member = _db.Set<DrillProjectMember>()
                .FirstOrDefault(x => x.ProjectId == projectId && (x.UserId == u.User_Id || x.UserName == u.UserName));
            if (member == null) return Json(new WebResponseContent().Error("未找到该项目下的成员信息"));

            var raw = _db.Set<DrillMessage>()
                .Where(x => x.ProjectId == projectId && x.Channel == "speech" && x.Id > lastId)
                .OrderBy(x => x.Id)
                .Take(200)
                .ToList();

            var list = raw
                .Select(ParseSpeechDialogMessage)
                .Where(x => x != null
                            && string.Equals(x.ToRoleName, member.RoleName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Json(new WebResponseContent(true) { Data = list });
        }

        private SpeechDialogDto ParseSpeechDialogMessage(DrillMessage row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.Content)) return null;
            try
            {
                var jo = JObject.Parse(row.Content);
                return new SpeechDialogDto
                {
                    Id = row.Id,
                    ToRoleName = jo["toRoleName"]?.ToString() ?? "",
                    Text = jo["text"]?.ToString() ?? "",
                    FromRoleName = jo["fromRoleName"]?.ToString() ?? row.RoleName ?? "",
                    FromUserName = jo["fromUserName"]?.ToString() ?? row.UserName ?? "",
                    CreateDate = row.CreateDate
                };
            }
            catch
            {
                return null;
            }
        }

        private string GetCurrentNodeCode(int projectId, string stage)
        {
            // 优先使用与阶段匹配的节点
            var code = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == projectId && x.Stage == stage)
                .OrderBy(x => x.OrderNo).Select(x => x.NodeCode).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(code))
            {
                code = _db.Set<DrillFlowNode>()
                    .Where(x => x.Enable == 1 && x.ProjectId == null && x.Stage == stage)
                    .OrderBy(x => x.OrderNo).Select(x => x.NodeCode).FirstOrDefault();
            }
            if (!string.IsNullOrWhiteSpace(code)) return code;

            // 兜底：取第一个可用节点
            return _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && (x.ProjectId == projectId || x.ProjectId == null))
                .OrderBy(x => x.ProjectId == projectId ? 0 : 1)
                .ThenBy(x => x.OrderNo)
                .Select(x => x.NodeCode)
                .FirstOrDefault();
        }

        private string GetNodeNameByCode(int projectId, string nodeCode)
        {
            if (string.IsNullOrWhiteSpace(nodeCode)) return "";
            var name = _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == projectId && x.NodeCode == nodeCode)
                .Select(x => x.NodeName)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return _db.Set<DrillFlowNode>()
                .Where(x => x.Enable == 1 && x.ProjectId == null && x.NodeCode == nodeCode)
                .Select(x => x.NodeName)
                .FirstOrDefault() ?? "";
        }

        private bool GetTaskBookVisible(int projectId)
        {
            if (projectId <= 0) return true;
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null || string.IsNullOrWhiteSpace(state.SettingsJson)) return true;
            try
            {
                var jo = JObject.Parse(state.SettingsJson);
                var token = jo["taskBookVisible"];
                if (token == null) return true;
                return token.Type == JTokenType.Boolean ? token.Value<bool>() : StringComparer.OrdinalIgnoreCase.Equals(token.ToString(), "true");
            }
            catch
            {
                return true;
            }
        }

        private HashSet<string> GetAllowedRolesForNode(int projectId, string nodeCode)
        {
            if (projectId <= 0 || string.IsNullOrWhiteSpace(nodeCode))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var state = _db.Set<DrillProjectState>().FirstOrDefault(x => x.ProjectId == projectId);
            if (state == null || string.IsNullOrWhiteSpace(state.SettingsJson))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var jo = JObject.Parse(state.SettingsJson);
                var permissions = jo["nodeRolePermissions"] as JObject;
                if (permissions == null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var token = permissions[nodeCode];
                if (token is JArray arr)
                {
                    return new HashSet<string>(
                        arr.Select(x => x?.ToString()?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)),
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
    }

    public class StudentNodeTaskSubmitRequest
    {
        public string NodeCode { get; set; }
        public long? AssignmentId { get; set; }
        public string TaskTitle { get; set; }
        public JToken StepResult { get; set; }
        public string TextContent { get; set; }
        public JToken Evidence { get; set; }
        public bool? SelfInitiated { get; set; }
    }

    public class StudentTaskbookSubmitRequest
    {
        public string NodeCode { get; set; }
        public List<string> SelectedItems { get; set; } = new List<string>();
        public List<string> CustomItems { get; set; } = new List<string>();
    }

    public class StudentReportSubmitRequest
    {
        public string ReportType { get; set; } // summary（兼容 report/recovery）
        public string Title { get; set; }
        public string Content { get; set; }
        public string ExtraJson { get; set; }
    }

    public class StudentDialogSendRequest
    {
        public string ToRoleName { get; set; }
        public string Content { get; set; }
    }

    public class SpeechDialogDto
    {
        public long Id { get; set; }
        public string ToRoleName { get; set; }
        public string Text { get; set; }
        public string FromRoleName { get; set; }
        public string FromUserName { get; set; }
        public DateTime? CreateDate { get; set; }
    }

    public class RoleTaskGroupDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public List<string> Items { get; set; } = new List<string>();
    }
}
