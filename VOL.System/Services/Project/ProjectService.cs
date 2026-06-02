using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.DomainModels;
using VOL.System.IRepositories;
using VOL.System.IServices;

namespace VOL.System.Services
{
    public partial class ProjectService : ServiceBase<Project, IProjectRepository>, IProjectService, IDependency
    {
        public ProjectService(IProjectRepository repository) : base(repository)
        {
            Init(repository);
        }

        public static IProjectService Instance =>
            AutofacContainerModule.GetService<IProjectService>();
    }
}
