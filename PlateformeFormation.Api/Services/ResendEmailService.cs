using System.Net.Http.Headers;
using System.Net.Http.Json;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

// Sends email via the Resend HTTP API instead of raw SMTP sockets. Most cloud hosts (Render's free
// tier included) block outbound SMTP on ports 25/465/587 to curb spam abuse, which makes
// SmtpEmailService a dead end in production there — an HTTPS API call on port 443 isn't affected.
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient http, IConfiguration configuration, ILogger<ResendEmailService> logger)
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
        var apiKey = _configuration["Resend:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogInformation("Resend is not configured. Skipping {Kind} email for {Email}.", kind, toEmail);
            return;
        }

        var fromEmail = _configuration["Email:FromEmail"] ?? "onboarding@resend.dev";
        var fromName = _configuration["Email:FromName"] ?? "Plateforme de Formation";

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = $"{fromName} <{fromEmail}>",
                to = new[] { toEmail },
                subject,
                html = htmlBody,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Resend {kind} email request failed ({(int)response.StatusCode}): {body}");
        }

        _logger.LogInformation("{Kind} email sent to {Email} via Resend.", kind, toEmail);
    }
}
