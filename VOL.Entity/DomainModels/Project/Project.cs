using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;
using Newtonsoft.Json;

namespace VOL.Entity.DomainModels
{
    [Table("project")]
    [Entity(TableCnName = "项目管理", TableName = "project")]
    public class Project : BaseEntity
    {
        [Key]
        [Display(Name = "主键")]
        [Column(TypeName = "int")]
        [Required]
        public int Id { get; set; }

        [Display(Name = "项目名称")]
        [MaxLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        [Required(AllowEmptyStrings = false)]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Name { get; set; }

        [Display(Name = "项目编号")]
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        [Required(AllowEmptyStrings = false)]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Code { get; set; }

        /// <summary> 0未开始 1进行中 2已结束 </summary>
        [Display(Name = "状态")]
        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int Status { get; set; }

        [Display(Name = "备注")]
        [MaxLength(500)]
        [Column(TypeName = "nvarchar(500)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Remark { get; set; }

        [Display(Name = "创建时间")]
        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? CreateDate { get; set; }

        [Display(Name = "创建人ID")]
        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int? CreateID { get; set; }

        [Display(Name = "创建人")]
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Creator { get; set; }

        [Display(Name = "修改时间")]
        [Column(TypeName = "datetime")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public DateTime? ModifyDate { get; set; }

        [Display(Name = "修改人ID")]
        [Column(TypeName = "int")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public int? ModifyID { get; set; }

        [Display(Name = "修改人")]
        [MaxLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        [System.ComponentModel.DataAnnotations.Editable(true)]
        public string Modifier { get; set; }

        [NotMapped]
        [JsonIgnore]
        public string StatusName => Status == 0 ? "未开始" : Status == 1 ? "进行中" : Status == 2 ? "已结束" : "";
    }
}
