using System.Net.Http.Json;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

// Sends email via the Brevo (formerly Sendinblue) HTTP API. Unlike Resend's sandbox mode, Brevo only
// requires verifying a single sender email address (no DNS/domain ownership needed) to send to any
// recipient — a better fit than Resend for a demo where the sender isn't a domain owner.
public class BrevoEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(HttpClient http, IConfiguration configuration, ILogger<BrevoEmailService> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendAccountValidatedAsync(Utilisateur user, string loginUrl, CancellationToken cancellationToken = default) =>
        SendAsync(user.Email, "Votre compte a été approuvé — Plateforme de Formation",
            EmailTemplates.BuildValidationHtml(user.Nom, user.Email, loginUrl), "validation", cancellationToken);

    public Task SendAccountRejectedAsync(Utilisateur user, CancellationToken cancellationToken = default) =>
        SendAsync(user.Email, "Mise à jour concernant votre demande d'accès",
            EmailTemplates.BuildRejectionHtml(user.Nom), "rejection", cancellationToken);

    public Task SendPasswordResetAsync(Utilisateur user, string resetUrl, CancellationToken cancellationToken = default) =>
        SendAsync(user.Email, "Réinitialisation de votre mot de passe",
            EmailTemplates.BuildPasswordResetHtml(user.Nom, user.Email, resetUrl), "password reset", cancellationToken);

    private async Task SendAsync(string toEmail, string subject, string htmlBody, string kind, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Brevo:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogInformation("Brevo is not configured. Skipping {Kind} email for {Email}.", kind, toEmail);
            return;
        }

        var fromEmail = _configuration["Email:FromEmail"]
            ?? throw new InvalidOperationException("Email:FromEmail must be set to your verified Brevo sender address.");
        var fromName = _configuration["Email:FromName"] ?? "Plateforme de Formation";

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = fromName, email = fromEmail },
                to = new[] { new { email = toEmail } },
                subject,
                htmlContent = htmlBody,
            }),
        };
        request.Headers.Add("api-key", apiKey);

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Brevo {kind} email request failed ({(int)response.StatusCode}): {body}");
        }

        _logger.LogInformation("{Kind} email sent to {Email} via Brevo.", kind, toEmail);
    }
}
