using System.ComponentModel.DataAnnotations;

namespace VOL.Entity.DomainModels.Drill
{
    /// <summary>
    /// 学员注册参数（小程序端，无需验证码）
    /// </summary>
    public class StudentRegisterInfo
    {
        [Display(Name = "项目编号")]
        [MaxLength(50)]
        [Required(ErrorMessage = "项目编号不能为空")]
        public string ProjectCode { get; set; }

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

        [Display(Name = "联系方式")]
        [MaxLength(100)]
        public string Contact { get; set; }

    }
}
