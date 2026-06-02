using System.Collections.Generic;
using VOL.Core.BaseProvider;
using VOL.Entity.DomainModels;

namespace VOL.System.IServices
{
    public partial interface IProjectMemberService : IService<ProjectMember>
    {
        List<ProjectMember> GetByProjectId(int projectId);
    }
}
