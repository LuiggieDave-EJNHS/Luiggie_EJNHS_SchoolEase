using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "That does not look like a valid email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}

public class AccessDeniedViewModel
{
    public string? ReturnUrl { get; set; }
}

/// <summary>
/// Entered on the recovery page once the Super Administrator account is locked
/// out, in place of a password.
/// </summary>
public class RecoverViewModel
{
    [Required(ErrorMessage = "Please enter the account email address.")]
    [EmailAddress(ErrorMessage = "That does not look like a valid email address.")]
    [Display(Name = "Account email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your recovery code.")]
    [Display(Name = "Recovery code")]
    public string Code { get; set; } = string.Empty;
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your full name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "That does not look like a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select your status.")]
    [Display(Name = "I am a...")]
    public string RegistrationRole { get; set; } = Roles.FormerStudent;
}

public class SearchViewModel
{
    public string Query { get; set; } = string.Empty;
    public List<SearchHit> Hits { get; set; } = new();

    public bool IsEmpty => Hits.Count == 0;
}

public class SearchHit
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-search";
    public string Url { get; set; } = "#";
}
