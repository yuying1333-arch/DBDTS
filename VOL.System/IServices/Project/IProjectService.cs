using VOL.Core.BaseProvider;
using VOL.Entity.DomainModels;

namespace VOL.System.IServices
{
    public partial interface IProjectService : IService<Project>
    {
        VOL.Entity.DomainModels.Project GetById(int id);
    }
}
