using System.ComponentModel.DataAnnotations;

namespace Tuki.Admin.Models.Auth;

public sealed class LoginRequest
{
    [Required, StringLength(256)]
    public string UserName { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}
