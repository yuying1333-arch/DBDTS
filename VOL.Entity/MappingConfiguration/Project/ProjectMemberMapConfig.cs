using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VOL.Entity.DomainModels;
using VOL.Entity.MappingConfiguration;

namespace VOL.Entity.MappingConfiguration
{
    public class ProjectMemberMapConfig : EntityMappingConfiguration<ProjectMember>
    {
        public override void Map(EntityTypeBuilder<ProjectMember> builder)
        {
        }
    }
}
