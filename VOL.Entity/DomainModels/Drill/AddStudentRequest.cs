using System.ComponentModel.DataAnnotations;

namespace VOL.Entity.DomainModels.Drill
{
    /// <summary>
    /// 后台添加学生参数（项目管理-添加学生）
    /// </summary>
    public class AddStudentRequest
    {
        [Display(Name = "项目ID")]
        [Required]
        public int ProjectId { get; set; }

        [Display(Name = "角色")]
        [MaxLength(50)]
        [Required(ErrorMessage = "请选择角色")]
        public string RoleName { get; set; }

        [Display(Name = "用户名")]
        [MaxLength(50)]
        [Required(ErrorMessage = "用户名不能为空")]
        public string UserName { get; set; }

        [Display(Name = "密码")]
        [MaxLength(50)]
        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }

        [Display(Name = "真实姓名")]
        [MaxLength(50)]
        [Required(ErrorMessage = "真实姓名不能为空")]
        public string UserTrueName { get; set; }

        [Display(Name = "所在单位")]
        [MaxLength(200)]
        public string Org { get; set; }

        [Display(Name = "职务")]
        [MaxLength(100)]
        public string JobTitle { get; set; }

        [Display(Name = "联系方式")]
        [MaxLength(100)]
        public string Contact { get; set; }
    }
}
