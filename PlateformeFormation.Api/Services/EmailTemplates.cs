using System.Net;

namespace PlateformeFormation.Api.Services;

// Shared HTML bodies for account-lifecycle emails, used by every IEmailService implementation
// (SmtpEmailService for local dev via Mailpit, ResendEmailService for production) so the two never
// drift out of sync with each other.
public static class EmailTemplates
{
    // ─── Shared layout wrapper ───────────────────────────────────────────────

    private static string Wrap(string headerAccentColor, string headerIcon, string headerTitle, string headerSubtitle, string bodyContent) => $@"<!DOCTYPE html>
<html lang=""fr"" xmlns=""http://www.w3.org/1999/xhtml"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
  <title>{headerTitle}</title>
</head>
<body style=""margin:0;padding:0;background-color:#f0f0f4;font-family:'Inter','Segoe UI',Helvetica,Arial,sans-serif;"">

<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f0f0f4;padding:48px 16px;"">
<tr><td align=""center"">

  <!-- Outer card -->
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:580px;width:100%;"">

    <!-- Brand bar -->
    <tr>
      <td style=""padding-bottom:20px;text-align:center;"">
        <span style=""font-size:11px;font-weight:700;letter-spacing:0.14em;text-transform:uppercase;color:#6b7280;"">
          Plateforme de Formation
        </span>
      </td>
    </tr>

    <!-- Header block -->
    <tr>
      <td style=""background-color:#111827;border-radius:16px 16px 0 0;padding:0;overflow:hidden;"">
        <!-- Crimson top stripe -->
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
          <tr><td style=""background:{headerAccentColor};height:4px;font-size:0;line-height:0;"">&nbsp;</td></tr>
        </table>
        <!-- Header content -->
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
          <tr>
            <td style=""padding:40px 48px 44px;text-align:left;"">
              <!-- Icon badge -->
              <div style=""display:inline-block;width:48px;height:48px;background:{headerAccentColor};border-radius:12px;text-align:center;line-height:48px;font-size:22px;margin-bottom:28px;"">
                {headerIcon}
              </div>
              <h1 style=""margin:0 0 10px;font-size:26px;font-weight:700;color:#ffffff;letter-spacing:-0.025em;line-height:1.25;"">
                {headerTitle}
              </h1>
              <p style=""margin:0;font-size:15px;color:rgba(255,255,255,0.55);line-height:1.6;"">
                {headerSubtitle}
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>

    <!-- Body -->
    <tr>
      <td style=""background-color:#ffffff;border:1px solid #e5e7eb;border-top:none;border-radius:0 0 16px 16px;padding:40px 48px;"">
        {bodyContent}

        <!-- Footer -->
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-top:36px;border-top:1px solid #f3f4f6;padding-top:24px;"">
          <tr>
            <td style=""text-align:center;"">
              <p style=""margin:0 0 6px;font-size:12px;font-weight:600;color:#9ca3af;letter-spacing:0.08em;text-transform:uppercase;"">
                Plateforme de Formation
              </p>
              <p style=""margin:0;font-size:12px;color:#d1d5db;"">
                &copy; 2026 — Tous droits réservés
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>

    <!-- Bottom spacer -->
    <tr><td style=""height:32px;""></td></tr>

  </table>
</td></tr>
</table>
</body>
</html>";

    // ─── Email: Account Validated ────────────────────────────────────────────

    // userName/userEmail come from Utilisateur.Nom/Email — free text a user chose at signup, not
    // server-controlled — so they're HTML-encoded before interpolation into a raw HTML email body.
    // Without this, a Nom containing markup would inject arbitrary HTML/script into an email sent to
    // that same user's inbox (a phishing/tracking vector, and a red flag to their mail client's spam
    // filter). loginUrl/resetUrl are server-generated, not user input, so they're left as-is.
    public static string BuildValidationHtml(string userName, string userEmail, string loginUrl) => Wrap(
        headerAccentColor: "#9b111e",
        headerIcon: "&#10003;",
        headerTitle: "Votre compte est actif",
        headerSubtitle: "Votre demande d'inscription a été approuvée par l'administrateur.",
        bodyContent: $@"
        <p style=""margin:0 0 8px;font-size:16px;color:#111827;font-weight:600;"">Bonjour {WebUtility.HtmlEncode(userName)},</p>
        <p style=""margin:0 0 28px;font-size:15px;color:#4b5563;line-height:1.7;"">
          Nous avons le plaisir de vous confirmer que votre accès à la plateforme de formation a été validé.
          Vous pouvez désormais vous connecter et accéder à l'ensemble des ressources disponibles.
        </p>

        <!-- Info tile -->
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:28px;"">
          <tr>
            <td style=""background-color:#f9fafb;border:1px solid #e5e7eb;border-left:3px solid #9b111e;border-radius:8px;padding:18px 20px;"">
              <p style=""margin:0 0 2px;font-size:11px;font-weight:700;color:#9b111e;letter-spacing:0.1em;text-transform:uppercase;"">Identifiant de connexion</p>
              <p style=""margin:0;font-size:15px;color:#111827;font-weight:500;"">{WebUtility.HtmlEncode(userEmail)}</p>
            </td>
          </tr>
        </table>

        <!-- CTA -->
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:28px;"">
          <tr>
            <td style=""background-color:#9b111e;border-radius:8px;"">
              <a href=""{loginUrl}"" style=""display:inline-block;padding:14px 36px;font-size:14px;font-weight:600;color:#ffffff;text-decoration:none;letter-spacing:0.02em;"">
                Accéder à la plateforme &rarr;
              </a>
            </td>
          </tr>
        </table>

        <p style=""margin:0;font-size:13px;color:#9ca3af;line-height:1.6;"">
          Si vous avez des questions, n'hésitez pas à contacter l'administration.
        </p>"
    );

    // ─── Email: Account Rejected ─────────────────────────────────────────────

    public static string BuildRejectionHtml(string userName) => Wrap(
        headerAccentColor: "#374151",
        headerIcon: "&#8212;",
        headerTitle: "Demande non retenue",
        headerSubtitle: "Suite à l'examen de votre dossier, nous ne pouvons pas donner suite à votre demande.",
        bodyContent: $@"
        <p style=""margin:0 0 8px;font-size:16px;color:#111827;font-weight:600;"">Bonjour {WebUtility.HtmlEncode(userName)},</p>
        <p style=""margin:0 0 24px;font-size:15px;color:#4b5563;line-height:1.7;"">
          Nous vous remercions de l'intérêt que vous portez à notre plateforme. Après examen de votre demande,
          l'administration n'a pas pu y donner suite pour le moment.
        </p>

        <!-- Note tile -->
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:28px;"">
          <tr>
            <td style=""background-color:#f9fafb;border:1px solid #e5e7eb;border-left:3px solid #6b7280;border-radius:8px;padding:18px 20px;"">
              <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.65;"">
                Si vous pensez qu'il s'agit d'une erreur ou si vous souhaitez obtenir des précisions,
                veuillez contacter directement l'administration de l'établissement.
              </p>
            </td>
          </tr>
        </table>

        <p style=""margin:0;font-size:13px;color:#9ca3af;line-height:1.6;"">
          Nous vous souhaitons bonne continuation.
        </p>"
    );

    // ─── Email: Password Reset ───────────────────────────────────────────────

    public static string BuildPasswordResetHtml(string userName, string userEmail, string resetUrl) => Wrap(
        headerAccentColor: "#9b111e",
        headerIcon: "&#128274;",
        headerTitle: "Réinitialisation du mot de passe",
        headerSubtitle: "Une demande de réinitialisation a été effectuée pour votre compte.",
        bodyContent: $@"
        <p style=""margin:0 0 8px;font-size:16px;color:#111827;font-weight:600;"">Bonjour {WebUtility.HtmlEncode(userName)},</p>
        <p style=""margin:0 0 28px;font-size:15px;color:#4b5563;line-height:1.7;"">
          Nous avons reçu une demande pour réinitialiser le mot de passe associé à votre compte.
          Cliquez sur le bouton ci-dessous pour en choisir un nouveau. Ce lien est valable <strong style=""color:#111827;"">1 heure</strong>.
        </p>

        <!-- Info tile -->
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:28px;"">
          <tr>
            <td style=""background-color:#f9fafb;border:1px solid #e5e7eb;border-left:3px solid #9b111e;border-radius:8px;padding:18px 20px;"">
              <p style=""margin:0 0 2px;font-size:11px;font-weight:700;color:#9b111e;letter-spacing:0.1em;text-transform:uppercase;"">Compte concerné</p>
              <p style=""margin:0;font-size:15px;color:#111827;font-weight:500;"">{WebUtility.HtmlEncode(userEmail)}</p>
            </td>
          </tr>
        </table>

        <!-- CTA -->
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:28px;"">
          <tr>
            <td style=""background-color:#9b111e;border-radius:8px;"">
              <a href=""{resetUrl}"" style=""display:inline-block;padding:14px 36px;font-size:14px;font-weight:600;color:#ffffff;text-decoration:none;letter-spacing:0.02em;"">
                Choisir un nouveau mot de passe &rarr;
              </a>
            </td>
          </tr>
        </table>

        <!-- Security note -->
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
          <tr>
            <td style=""background-color:#fffbeb;border:1px solid #fde68a;border-radius:8px;padding:14px 18px;"">
              <p style=""margin:0;font-size:13px;color:#92400e;line-height:1.6;"">
                <strong>Vous n'êtes pas à l'origine de cette demande ?</strong><br>
                Ignorez simplement cet email. Votre mot de passe restera inchangé et le lien expirera automatiquement.
              </p>
            </td>
          </tr>
        </table>"
    );
}
