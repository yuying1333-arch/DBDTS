using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.DomainModels;
using VOL.System.IRepositories;

namespace VOL.System.Repositories
{
    public partial class ProjectMemberRepository : RepositoryBase<ProjectMember>, IProjectMemberRepository
    {
        public ProjectMemberRepository(VOLContext dbContext) : base(dbContext) { }

        public static IProjectMemberRepository Instance =>
            AutofacContainerModule.GetService<IProjectMemberRepository>();
    }
}
