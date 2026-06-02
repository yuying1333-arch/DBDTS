using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    [Table("drill_flow_node")]
    public class DrillFlowNode : BaseEntity
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

        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        [Required]
        public string NodeName { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        public string Stage { get; set; }

        [Column(TypeName = "int")]
        public int OrderNo { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string NextNodeCode { get; set; }

        [Column(TypeName = "text")]
        public string Description { get; set; }

        /// <summary>节点绑定的视频资源ID</summary>
        [Column(TypeName = "bigint")]
        public long? ResourceId { get; set; }

        /// <summary>节点对应的视频开始秒数</summary>
        [Column(TypeName = "int")]
        public int? VideoStartSeconds { get; set; }

        /// <summary>节点对应的视频结束秒数</summary>
        [Column(TypeName = "int")]
        public int? VideoEndSeconds { get; set; }

        [Column(TypeName = "int")]
        public int Enable { get; set; } = 1;

        [Column(TypeName = "datetime")]
        public DateTime? CreateDate { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? ModifyDate { get; set; }
    }
}

