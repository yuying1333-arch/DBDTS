using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    [Table("drill_project_member")]
    public class DrillProjectMember : BaseEntity
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
        [Required]
        public string UserName { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string RoleName { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string UserTrueName { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string Org { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string JobTitle { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Contact { get; set; }

        [MaxLength(500)]
        [Column(TypeName = "varchar(500)")]
        public string Photo { get; set; }

        /// <summary>0待审核 1通过 2拒绝</summary>
        [Column(TypeName = "int")]
        [Required]
        public int AuditStatus { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? SignedAt { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? CreateDate { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? ModifyDate { get; set; }
    }
}
