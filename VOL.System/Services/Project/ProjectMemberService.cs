using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.DomainModels;
using VOL.System.IRepositories;
using VOL.System.IServices;

namespace VOL.System.Services
{
    public partial class ProjectMemberService : ServiceBase<ProjectMember, IProjectMemberRepository>, IProjectMemberService, IDependency
    {
        public ProjectMemberService(IProjectMemberRepository repository) : base(repository)
        {
            Init(repository);
        }

        public static IProjectMemberService Instance =>
            AutofacContainerModule.GetService<IProjectMemberService>();
    }
}
