using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.DomainModels;

namespace VOL.System.IRepositories
{
    public partial interface IProjectMemberRepository : IDependency, IRepository<ProjectMember>
    {
    }
}
