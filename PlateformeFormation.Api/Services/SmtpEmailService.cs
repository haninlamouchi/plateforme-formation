using System.Net;
using System.Net.Mail;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

// Raw-SMTP email delivery — works against a local Mailpit instance for development, but most cloud
// hosts (Render included) block outbound SMTP on free/starter tiers, so this is not viable in
// production there. See ResendEmailService for the HTTP-API-based alternative used in that case.
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAccountValidatedAsync(Utilisateur user, string loginUrl, CancellationToken cancellationToken = default)
    {
        await SendAsync(
            user.Email,
            "Votre compte a été approuvé — Plateforme de Formation",
            EmailTemplates.BuildValidationHtml(user.Nom, user.Email, loginUrl),
            "validation",
            cancellationToken);
    }

    public async Task SendAccountRejectedAsync(Utilisateur user, CancellationToken cancellationToken = default)
    {
        await SendAsync(
            user.Email,
            "Mise à jour concernant votre demande d'accès",
            EmailTemplates.BuildRejectionHtml(user.Nom),
            "rejection",
            cancellationToken);
    }

    public async Task SendPasswordResetAsync(Utilisateur user, string resetUrl, CancellationToken cancellationToken = default)
    {
        await SendAsync(
            user.Email,
            "Réinitialisation de votre mot de passe",
            EmailTemplates.BuildPasswordResetHtml(user.Nom, user.Email, resetUrl),
            "password reset",
            cancellationToken);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody, string kind, CancellationToken cancellationToken)
    {
        var smtpHost = _configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogInformation("SMTP is not configured. Skipping {Kind} email for {Email}.", kind, toEmail);
            return;
        }

        var smtpPort = int.TryParse(_configuration["Email:Smtp:Port"], out var port) ? port : 587;
        var smtpUser = _configuration["Email:Smtp:Username"];
        var smtpPassword = _configuration["Email:Smtp:Password"];
        var smtpEnableSsl = bool.TryParse(_configuration["Email:Smtp:EnableSsl"], out var enableSsl) ? enableSsl : true;
        var fromEmail = _configuration["Email:FromEmail"] ?? smtpUser ?? "no-reply@plateformeformation.local";
        var fromName = _configuration["Email:FromName"] ?? "Plateforme de Formation";

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = smtpEnableSsl,
            Credentials = string.IsNullOrWhiteSpace(smtpUser)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(smtpUser, smtpPassword)
        };

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("{Kind} email sent to {Email}.", kind, toEmail);
    }
}
