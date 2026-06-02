using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.System.IServices;

namespace VOL.System.Controllers
{
    [Route("api/ProjectMember")]
    public partial class ProjectMemberController : ApiBaseController<IProjectMemberService>
    {
        public ProjectMemberController(IProjectMemberService service)
            : base("System", "Project", "ProjectMember", service)
        {
        }

        [HttpGet("GetByProject")]
        public IActionResult GetByProject(int projectId)
        {
            var list = (Service as IProjectMemberService).GetByProjectId(projectId);
            return Json(new { status = true, data = list });
        }
    }
}
