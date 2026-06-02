using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    [Table("drill_project_state")]
    public class DrillProjectState : BaseEntity
    {
        [Key]
        [Column(TypeName = "bigint")]
        [Required]
        public long Id { get; set; }

        [Column(TypeName = "int")]
        [Required]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int ProjectId { get; set; }

        /// <summary>0未开始 1运行中 2暂停 3已结束</summary>
        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int Status { get; set; }

        /// <summary>scene/report/response/review/end/recovery</summary>
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string CurrentStage { get; set; }

        /// <summary>当前流程节点编码（教学主链）</summary>
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string CurrentNodeCode { get; set; }

        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? StartedAt { get; set; }

        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? PausedAt { get; set; }

        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? EndedAt { get; set; }

        /// <summary>累计已运行秒数（暂停/结束时固化）</summary>
        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int ElapsedSeconds { get; set; }

        /// <summary>最近一次进入运行态的时间（Status=1 时有效）</summary>
        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? LastResumedAt { get; set; }

        [Column(TypeName = "text")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string SettingsJson { get; set; }

        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? CreateDate { get; set; }

        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int? CreateID { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Creator { get; set; }

        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? ModifyDate { get; set; }

        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int? ModifyID { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Modifier { get; set; }
    }
}

