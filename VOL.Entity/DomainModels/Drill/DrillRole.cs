using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    /// <summary>
    /// 演练全局角色（管理员自定义）
    /// </summary>
    [Table("drill_role")]
    public class DrillRole : BaseEntity
    {
        [Key]
        [Column(TypeName = "bigint")]
        [Required]
        public long Id { get; set; }

        /// <summary>角色编号（可用于展示/排序等，不强绑定成员表）</summary>
        [Column(TypeName = "varchar(50)")]
        [MaxLength(50)]
        public string RoleNo { get; set; }

        /// <summary>角色名（用于成员表 DrillProjectMember.RoleName 做映射）</summary>
        [Column(TypeName = "varchar(50)")]
        [MaxLength(50)]
        [Required]
        public string RoleName { get; set; }

        [Column(TypeName = "varchar(500)")]
        [MaxLength(500)]
        public string Avatar { get; set; }

        [Column(TypeName = "varchar(100)")]
        [MaxLength(100)]
        public string Marker { get; set; }

        /// <summary>
        /// 角色任务书（结构化清单 JSON 字符串）
        /// 建议结构：
        /// {
        ///   "tasks":[
        ///     {"id":"t1","title":"任务1","items":[{"id":"i1","text":"检查1"}]}
        ///   ]
        /// }
        /// </summary>
        [Column(TypeName = "text")]
        public string TaskBookJson { get; set; }

        /// <summary>是否启用</summary>
        [Column(TypeName = "int")]
        public int Enable { get; set; } = 1;

        [Column(TypeName = "datetime")]
        public DateTime? CreateDate { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? ModifyDate { get; set; }
    }
}

