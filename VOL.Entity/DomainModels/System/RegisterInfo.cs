using System.ComponentModel.DataAnnotations;

namespace VOL.Entity.DomainModels
{
    /// <summary>
    /// 注册参数：注册新用户一律为普通用户（教师角色）
    /// </summary>
    public class RegisterInfo
    {
        [Display(Name = "用户名")]
        [MaxLength(50)]
        [Required(ErrorMessage = "用户名不能为空")]
        public string UserName { get; set; }

        [Display(Name = "密码")]
        [MaxLength(50)]
        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }

        [Display(Name = "确认密码")]
        [MaxLength(50)]
        [Required(ErrorMessage = "请确认密码")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "姓名")]
        [MaxLength(20)]
        [Required(ErrorMessage = "姓名不能为空")]
        public string UserTrueName { get; set; }

        [MaxLength(6)]
        [Display(Name = "验证码")]
        [Required(ErrorMessage = "验证码不能为空")]
        public string VerificationCode { get; set; }

        [Required(ErrorMessage = "参数不完整")]
        public string UUID { get; set; }
    }
}
