using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels.Drill
{
    [Table("drill_event")]
    public class DrillEvent : BaseEntity
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
        public string Stage { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [Required]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string EventType { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Title { get; set; }

        [Column(TypeName = "text")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Content { get; set; }

        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? OccurAt { get; set; }

        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int? UserId { get; set; }

        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string UserName { get; set; }

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string RoleName { get; set; }

        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? CreateDate { get; set; }
    }
}

