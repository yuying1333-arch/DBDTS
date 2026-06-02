using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.System.IServices;

namespace VOL.System.Controllers
{
    [Route("api/Project")]
    public partial class ProjectController : ApiBaseController<IProjectService>
    {
        public ProjectController(IProjectService service)
            : base("System", "Project", "Project", service)
        {
        }

        [HttpGet("Get")]
        public IActionResult Get(int id)
        {
            var entity = (Service as IProjectService).GetById(id);
            if (entity == null)
                return Json(new { status = false, message = "未找到" });
            return Json(new { status = true, data = entity });
        }
    }
}
