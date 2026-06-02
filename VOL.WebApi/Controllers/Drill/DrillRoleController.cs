using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using VOL.Core.Controllers.Basic;
using VOL.Core.Filters;
using VOL.Core.EFDbContext;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels.Drill;

namespace VOL.WebApi.Controllers.Drill
{
    /// <summary>
    /// 管理员：演练全局角色管理（角色名/编号/标识/任务书）
    /// </summary>
    [Route("api/Drill/Roles")]
    public class DrillRoleController : VolController
    {
        private readonly VOLContext _db;

        public DrillRoleController(VOLContext db)
        {
            _db = db;
        }

        private bool IsAdmin()
        {
            return UserContext.Current.IsSuperAdmin;
        }

        [HttpGet]
        public IActionResult GetRoles()
        {
            if (!IsAdmin()) return Json(new WebResponseContent().Error("无权限"));

            var list = _db.Set<DrillRole>()
                .OrderBy(x => x.RoleNo ?? x.RoleName)
                .Select(x => new
                {
                    id = x.Id,
                    roleNo = x.RoleNo,
                    roleName = x.RoleName,
                    marker = x.Marker,
                    taskBookJson = x.TaskBookJson,
                    enable = x.Enable
                })
                .ToList();

            return Json(new WebResponseContent(true) { Data = list });
        }

        [HttpPost("Add")]
        public IActionResult Add([FromBody] DrillRoleSaveRequest req)
        {
            if (!IsAdmin()) return Json(new WebResponseContent().Error("无权限"));
            if (req == null) return Json(new WebResponseContent().Error("参数不能为空"));
            if (string.IsNullOrWhiteSpace(req.RoleName)) return Json(new WebResponseContent().Error("角色名不能为空"));
            if (string.IsNullOrWhiteSpace(req.RoleNo)) return Json(new WebResponseContent().Error("角色编号不能为空"));

            var exist = _db.Set<DrillRole>().Any(x => x.RoleName == req.RoleName.Trim());
            if (exist) return Json(new WebResponseContent().Error("角色名已存在"));

            var role = new DrillRole
            {
                RoleNo = req.RoleNo?.Trim(),
                RoleName = req.RoleName?.Trim(),
                Marker = req.Marker?.Trim(),
                TaskBookJson = req.TaskBookJson,
                Enable = req.Enable ?? 1,
                CreateDate = DateTime.Now
            };
            _db.Add(role);
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "添加成功", Data = role });
        }

        [HttpPost("Update")]
        public IActionResult Update(long id, [FromBody] DrillRoleSaveRequest req)
        {
            if (!IsAdmin()) return Json(new WebResponseContent().Error("无权限"));
            if (id <= 0) return Json(new WebResponseContent().Error("参数不完整"));
            if (req == null) return Json(new WebResponseContent().Error("参数不能为空"));

            var role = _db.Set<DrillRole>().FirstOrDefault(x => x.Id == id);
            if (role == null) return Json(new WebResponseContent().Error("未找到角色"));

            if (!string.IsNullOrWhiteSpace(req.RoleName))
            {
                var newName = req.RoleName.Trim();
                var conflict = _db.Set<DrillRole>().Any(x => x.Id != id && x.RoleName == newName);
                if (conflict) return Json(new WebResponseContent().Error("角色名已存在"));
                role.RoleName = newName;
            }

            role.RoleNo = req.RoleNo?.Trim();
            role.Marker = req.Marker?.Trim();
            role.TaskBookJson = req.TaskBookJson;
            role.Enable = req.Enable ?? role.Enable;
            role.ModifyDate = DateTime.Now;

            _db.Update(role);
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "更新成功", Data = role });
        }

        [HttpPost("Delete")]
        public IActionResult Delete(long id)
        {
            if (!IsAdmin()) return Json(new WebResponseContent().Error("无权限"));
            if (id <= 0) return Json(new WebResponseContent().Error("参数不完整"));

            var role = _db.Set<DrillRole>().FirstOrDefault(x => x.Id == id);
            if (role == null) return Json(new WebResponseContent().Error("未找到角色"));

            _db.Remove(role);
            _db.SaveChanges();

            return Json(new WebResponseContent(true) { Message = "删除成功" });
        }

    }

    public class DrillRoleSaveRequest
    {
        public string RoleNo { get; set; }
        public string RoleName { get; set; }
        public string Marker { get; set; }
        public string TaskBookJson { get; set; }
        public int? Enable { get; set; } = 1;
    }
}

