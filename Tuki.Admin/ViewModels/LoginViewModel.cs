using System.ComponentModel.DataAnnotations;

namespace Tuki.Admin.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
