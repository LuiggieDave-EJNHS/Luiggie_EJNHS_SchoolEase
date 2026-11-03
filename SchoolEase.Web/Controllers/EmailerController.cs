using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

/// <summary>Administrative tool for sending direct emails to students or other schools.</summary>
[Authorize(Roles = $"{Roles.SuperAdministrator},{Roles.SchoolAdministrator},{Roles.IctHeadCoordinator},{Roles.Registrar}")]
public class EmailerController : Controller
{
    private readonly IEmailSender _emailSender;
    private readonly IAuditService _audit;
    private readonly ILogger<EmailerController> _logger;

    public EmailerController(
        IEmailSender emailSender,
        IAuditService audit,
        ILogger<EmailerController> logger)
    {
        _emailSender = emailSender;
        _audit = audit;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index(string? to)
    {
        var model = new EmailerViewModel
        {
            Recipient = to ?? string.Empty
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(EmailerViewModel model)
    {
        if (!ModelState.IsValid) return View("Index", model);

        try
        {
            // Use the professional HTML shell for the custom message
            // We can add a simpler template to EmailTemplates later if needed, 
            // but for now we'll send the raw body wrapped in a basic structure.
            var logoUrl = Url.Content("~/images/logo.jpg");
            if (logoUrl.StartsWith("~")) logoUrl = logoUrl.Substring(1);
            var fullLogoUrl = $"{Request.Scheme}://{Request.Host}{logoUrl}";

            // For now, we'll just send the text. We could wrap it in a template if desired.
            await _emailSender.SendEmailAsync(model.Recipient, model.Subject, model.Body);

            await _audit.LogAsync("Email Sent", "Emailer", null, $"Email sent to {model.Recipient} regarding '{model.Subject}'.");
            
            TempData["Success"] = $"Email successfully sent to {model.Recipient}.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send manual email to {Recipient}", model.Recipient);
            ModelState.AddModelError(string.Empty, "Failed to send email. Please check your SMTP/API configuration and try again.");
            return View("Index", model);
        }
    }
}
