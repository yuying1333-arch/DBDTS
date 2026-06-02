using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    [Table("drill_task_action")]
    public class DrillTaskAction : BaseEntity
    {
        [Key]
        [Column(TypeName = "bigint")]
        [Required]
        public long Id { get; set; }

        [Column(TypeName = "int")]
        [Required]
        public int ProjectId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string NodeCode { get; set; }

        [Column(TypeName = "bigint")]
        public long? AssignmentId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string RoleName { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string TaskTitle { get; set; }

        [Column(TypeName = "text")]
        public string StepResultJson { get; set; }

        [Column(TypeName = "text")]
        public string TextContent { get; set; }

        [Column(TypeName = "text")]
        public string EvidenceJson { get; set; }

        [Column(TypeName = "int")]
        public int Status { get; set; } = 1;

        [Column(TypeName = "int")]
        public int? UserId { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string UserName { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? OccurAt { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? CreateDate { get; set; }
    }
}

