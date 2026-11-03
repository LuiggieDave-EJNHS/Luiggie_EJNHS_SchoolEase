using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.ViewModels;

public class EmailerViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Recipient Email")]
    public string Recipient { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Message Body")]
    public string Body { get; set; } = string.Empty;

    public bool SendCopy { get; set; } = false;
}
