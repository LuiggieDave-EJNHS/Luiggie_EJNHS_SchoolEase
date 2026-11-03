using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SchoolEase.Web.Models;

namespace SchoolEase.Web.Services;

/// <summary>
/// Sends account emails through Brevo's HTTPS transactional API.
/// Direct SMTP cannot be used here: hosts such as Render's free tier block all
/// outbound traffic to ports 25/465/587, so mail has to leave over HTTPS (443).
/// </summary>
public class EmailSender : IEmailSender
{
    private const string Endpoint = "https://api.brevo.com/v3/smtp/email";

    // Single shared client so repeated sends do not exhaust sockets.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly EmailSettings _settings;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<EmailSettings> settings, ILogger<EmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.SenderEmail))
        {
            _logger.LogWarning("Email sender is not configured (Mail__ApiKey / Mail__SenderEmail missing). Email to {Email} was NOT sent.", email);
            return;
        }

        var payload = new
        {
            sender = new { name = _settings.SenderName, email = _settings.SenderEmail },
            to = new[] { new { email } },
            subject,
            htmlContent = htmlMessage
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", _settings.ApiKey);

        try
        {
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Brevo API returned {(int)response.StatusCode}: {body}");
            }

            _logger.LogInformation("Email to {Email} sent successfully via Brevo.", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}.", email);
            throw; // Let the caller decide (registration shows a warning and keeps the account).
        }
    }
}
