using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.DomainModels
{
    [Table("projectmember")]
    [Entity(TableCnName = "项目成员", TableName = "projectmember")]
    public class ProjectMember : BaseEntity
    {
        [Key]
        [Display(Name = "主键")]
        [Column(TypeName = "int")]
        [Required]
        public int Id { get; set; }

        [Display(Name = "项目ID")]
        [Column(TypeName = "int")]
        [Required]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int ProjectId { get; set; }

        [Display(Name = "姓名")]
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        [Required(AllowEmptyStrings = false)]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Name { get; set; }

        [Display(Name = "角色")]
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Role { get; set; }

        [Display(Name = "联系电话")]
        [MaxLength(20)]
        [Column(TypeName = "nvarchar(20)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Phone { get; set; }

        [Display(Name = "创建时间")]
        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? CreateDate { get; set; }
    }
}
