using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    /// <summary>
    /// 角色任务书打卡记录（每个项目+用户+角色仅允许打卡一次，满足 A）
    /// </summary>
    [Table("drill_role_punch")]
    public class DrillRolePunch : BaseEntity
    {
        [Key]
        [Column(TypeName = "bigint")]
        [Required]
        public long Id { get; set; }

        [Column(TypeName = "int")]
        [Required]
        public int ProjectId { get; set; }

        [Column(TypeName = "varchar(50)")]
        [MaxLength(50)]
        [Required]
        public string RoleName { get; set; }

        [Column(TypeName = "int")]
        public int UserId { get; set; }

        [Column(TypeName = "varchar(50)")]
        [MaxLength(50)]
        public string UserName { get; set; }

        [Column(TypeName = "varchar(100)")]
        [MaxLength(100)]
        public string UserTrueName { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime PunchAt { get; set; }

        [Column(TypeName = "text")]
        public string ContentJson { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime CreateDate { get; set; }
    }
}

