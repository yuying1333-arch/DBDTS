using System.Linq;
using VOL.Entity.DomainModels;

namespace VOL.System.Services
{
    public partial class ProjectService
    {
        public Project GetById(int id)
        {
            return repository.FindFirst(x => x.Id == id);
        }
    }
}
