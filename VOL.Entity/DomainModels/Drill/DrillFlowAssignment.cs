using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    [Table("drill_flow_assignment")]
    public class DrillFlowAssignment : BaseEntity
    {
        [Key]
        [Column(TypeName = "bigint")]
        [Required]
        public long Id { get; set; }

        [Column(TypeName = "int")]
        public int? ProjectId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string NodeCode { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string RoleName { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        [Required]
        public string TaskTitle { get; set; }

        [Column(TypeName = "text")]
        public string TaskDetail { get; set; }

        [Column(TypeName = "text")]
        public string StepsJson { get; set; }

        [MaxLength(30)]
        [Column(TypeName = "varchar(30)")]
        public string SubmitType { get; set; }

        [Column(TypeName = "int")]
        public int EvidenceRequired { get; set; } = 0;

        [Column(TypeName = "int")]
        public int OrderNo { get; set; }

        [Column(TypeName = "int")]
        public int Enable { get; set; } = 1;

        [Column(TypeName = "datetime")]
        public DateTime? CreateDate { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? ModifyDate { get; set; }
    }
}

