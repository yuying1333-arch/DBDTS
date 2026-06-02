using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VOL.Entity.DomainModels;
using VOL.Entity.MappingConfiguration;

namespace VOL.Entity.MappingConfiguration
{
    public class ProjectMapConfig : EntityMappingConfiguration<Project>
    {
        public override void Map(EntityTypeBuilder<Project> builder)
        {
        }
    }
}
