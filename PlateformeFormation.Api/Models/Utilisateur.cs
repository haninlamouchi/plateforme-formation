namespace PlateformeFormation.Api.Models;

public class Utilisateur
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MotDePasseHash { get; set; } = string.Empty;
    public RoleUtilisateur Role { get; set; }
    public StatutCompte StatutCompte { get; set; } = StatutCompte.EN_ATTENTE;
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public DateTime? DateValidation { get; set; }
    public DateTime? DerniereConnexion { get; set; }
    public string? ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordTokenExpiry { get; set; }

    // Security
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutUntil { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public bool IsGoogleUser { get; set; } = false;

    // Informations de profil complementaires
    public string? Discipline { get; set; }
    public string? Departement { get; set; }
    public string? Telephone { get; set; }
    public DateTime? DateNaissance { get; set; }
    public string? PhotoUrl { get; set; }

    // Navigation
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Formation> Formations { get; set; } = new List<Formation>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<JournalActivite> JournalActivites { get; set; } = new List<JournalActivite>();
}