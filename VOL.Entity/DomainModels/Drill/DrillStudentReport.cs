using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    [Table("drill_student_report")]
    public class DrillStudentReport : BaseEntity
    {
        [Key]
        [Column(TypeName = "bigint")]
        [Required]
        public long Id { get; set; }

        [Column(TypeName = "int")]
        [Required]
        public int ProjectId { get; set; }

        [Column(TypeName = "int")]
        public int? UserId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string UserName { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string UserTrueName { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string RoleName { get; set; }

        [MaxLength(30)]
        [Column(TypeName = "varchar(30)")]
        [Required]
        public string ReportType { get; set; } // report/recovery

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string Title { get; set; }

        [Column(TypeName = "text")]
        public string Content { get; set; }

        [Column(TypeName = "text")]
        public string ExtraJson { get; set; }

        [Column(TypeName = "int")]
        [Required]
        public int SubmitStatus { get; set; } // 1已提交 2已批阅

        [Column(TypeName = "int")]
        public int? ReviewScore { get; set; }

        [MaxLength(1000)]
        [Column(TypeName = "varchar(1000)")]
        public string ReviewComment { get; set; }

        [Column(TypeName = "int")]
        public int? ReviewerId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string ReviewerName { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? ReviewAt { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? CreateDate { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? ModifyDate { get; set; }
    }
}
