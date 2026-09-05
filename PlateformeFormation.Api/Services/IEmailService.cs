using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public interface IEmailService
{
    Task SendAccountValidatedAsync(Utilisateur user, string loginUrl, CancellationToken cancellationToken = default);
    Task SendAccountRejectedAsync(Utilisateur user, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(Utilisateur user, string resetUrl, CancellationToken cancellationToken = default);
}
