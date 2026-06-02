using System.Collections.Generic;
using System.Linq;
using VOL.Entity.DomainModels;

namespace VOL.System.Services
{
    public partial class ProjectMemberService
    {
        public List<ProjectMember> GetByProjectId(int projectId)
        {
            return repository.Find(x => x.ProjectId == projectId).OrderBy(x => x.Id).ToList();
        }
    }
}
