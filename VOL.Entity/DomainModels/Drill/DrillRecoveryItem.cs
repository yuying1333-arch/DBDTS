using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    [Table("drill_recovery_item")]
    public class DrillRecoveryItem : BaseEntity
    {
        [Key]
        [Column(TypeName = "bigint")]
        [Required]
        public long Id { get; set; }

        [Column(TypeName = "int")]
        [Required]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int ProjectId { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Category { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        [Required]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Title { get; set; }

        /// <summary>0未完成 1已完成</summary>
        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int Status { get; set; }

        [MaxLength(500)]
        [Column(TypeName = "varchar(500)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Note { get; set; }

        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int? OrderNo { get; set; }

        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? ModifyDate { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Modifier { get; set; }
    }
}

