using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    [Table("drill_project_video_resource")]
    public class DrillProjectVideoResource : BaseEntity
    {
        [Key]
        [Column(TypeName = "bigint")]
        [Required]
        public long Id { get; set; }

        [Column(TypeName = "int")]
        [Required]
        public int ProjectId { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string ResourceName { get; set; }

        [MaxLength(500)]
        [Column(TypeName = "varchar(500)")]
        [Required]
        public string VideoUrl { get; set; }

        [Column(TypeName = "int")]
        [Required]
        public int Enable { get; set; } = 1;

        [Column(TypeName = "datetime")]
        public DateTime? CreateDate { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? ModifyDate { get; set; }
    }
}
