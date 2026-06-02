using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.DomainModels;
using VOL.System.IRepositories;

namespace VOL.System.Repositories
{
    public partial class ProjectRepository : RepositoryBase<Project>, IProjectRepository
    {
        public ProjectRepository(VOLContext dbContext) : base(dbContext) { }

        public static IProjectRepository Instance =>
            AutofacContainerModule.GetService<IProjectRepository>();
    }
}
