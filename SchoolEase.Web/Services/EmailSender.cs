using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using SchoolEase.Web.Models;

namespace SchoolEase.Web.Services;

public class EmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<EmailSettings> settings, ILogger<EmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrEmpty(_settings.SmtpServer))
        {
            _logger.LogWarning("Email sender is not configured. Email to {Email} with subject '{Subject}' was NOT sent.", email, subject);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(new MailboxAddress(email, email));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            // 30 second timeout + ignore certificate revocation hangs
            client.Timeout = 30000;
            client.CheckCertificateRevocation = false;

            // FORCE IPv4: Many cloud hosts have broken IPv6 routing to Google
            client.LocalDomain = "localhost"; 

            var port = _settings.SmtpPort == 0 ? 465 : _settings.SmtpPort;
            var options = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            _logger.LogInformation("SMTP: Connecting to {Server}:{Port} ({Options})...", _settings.SmtpServer, port, options);
            
            await client.ConnectAsync(_settings.SmtpServer, port, options);

            if (!string.IsNullOrEmpty(_settings.SmtpUser))
            {
                await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email to {Email} sent successfully.", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}.", email);
            throw; // Rethrow to let Identity handle it or show error
        }
    }
}
