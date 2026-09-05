using Microsoft.EntityFrameworkCore;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<Categorie> Categories => Set<Categorie>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentSegment> DocumentSegments => Set<DocumentSegment>();
    public DbSet<Formation> Formations => Set<Formation>();
    public DbSet<FormationDocument> FormationDocuments => Set<FormationDocument>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<AlerteDoublon> AlertesDoublons => Set<AlerteDoublon>();
    public DbSet<ExportHistorique> ExportsHistorique => Set<ExportHistorique>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<JournalActivite> JournalActivites => Set<JournalActivite>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
modelBuilder.Entity<Utilisateur>(e =>
{
    e.ToTable("utilisateurs");
    e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id");
    e.Property(x => x.Nom).HasColumnName("nom").HasMaxLength(150).IsRequired();
    e.Property(x => x.Email).HasColumnName("email").HasMaxLength(190).IsRequired();
    e.HasIndex(x => x.Email).IsUnique();
    e.HasIndex(x => x.RefreshToken);
    e.HasIndex(x => x.StatutCompte);
    e.Property(x => x.MotDePasseHash).HasColumnName("mot_de_passe_hash").HasMaxLength(255).IsRequired();
    e.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(30).IsRequired();
    e.Property(x => x.StatutCompte).HasColumnName("statut_compte").HasConversion<string>().HasMaxLength(20).HasDefaultValue(StatutCompte.EN_ATTENTE);
    e.Property(x => x.Actif).HasColumnName("actif").HasDefaultValue(true);
    e.Property(x => x.DateCreation).HasColumnName("date_creation");
    e.Property(x => x.DateValidation).HasColumnName("date_validation");
    e.Property(x => x.DerniereConnexion).HasColumnName("derniere_connexion");
    e.Property(x => x.Discipline).HasColumnName("discipline").HasMaxLength(150);
    e.Property(x => x.Departement).HasColumnName("departement").HasMaxLength(150);
    e.Property(x => x.Telephone).HasColumnName("telephone").HasMaxLength(30);
    e.Property(x => x.DateNaissance).HasColumnName("date_naissance");
    e.Property(x => x.PhotoUrl).HasColumnName("photo_url").HasMaxLength(500);
    e.Property(x => x.ResetPasswordToken).HasColumnName("reset_password_token").HasMaxLength(255);
    e.Property(x => x.ResetPasswordTokenExpiry).HasColumnName("reset_password_token_expiry");
    e.Property(x => x.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasDefaultValue(0);
    e.Property(x => x.LockoutUntil).HasColumnName("lockout_until");
    e.Property(x => x.RefreshToken).HasColumnName("refresh_token").HasMaxLength(255);
    e.Property(x => x.RefreshTokenExpiry).HasColumnName("refresh_token_expiry");
    e.Property(x => x.IsGoogleUser).HasColumnName("is_google_user").HasDefaultValue(false);
});

        modelBuilder.Entity<Categorie>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nom).HasColumnName("nom").HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.HasIndex(x => x.Nom).IsUnique();
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Titre).HasColumnName("titre").HasMaxLength(255).IsRequired();
            e.Property(x => x.CheminFichier).HasColumnName("chemin_fichier").HasMaxLength(500).IsRequired();
            e.Property(x => x.TypeDocument).HasColumnName("type_document").HasMaxLength(100);
            e.Property(x => x.CategorieId).HasColumnName("categorie_id");
            e.Property(x => x.UploadedBy).HasColumnName("uploaded_by");
            e.Property(x => x.Resume).HasColumnName("resume");
            e.Property(x => x.Langue).HasColumnName("langue").HasMaxLength(50);
            e.Property(x => x.NombrePages).HasColumnName("nombre_pages");
            e.Property(x => x.TailleFichier).HasColumnName("taille_fichier");
            e.Property(x => x.HashDocument).HasColumnName("hash_document").HasMaxLength(255);
            e.Property(x => x.StatutTraitement).HasColumnName("statut_traitement").HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.OrigineCategorie).HasColumnName("origine_categorie").HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DateAjout).HasColumnName("date_ajout");

            e.HasIndex(x => x.CategorieId);
            e.HasIndex(x => x.UploadedBy);
            e.HasIndex(x => x.DateAjout);

            e.HasOne(x => x.Categorie).WithMany(c => c.Documents)
                .HasForeignKey(x => x.CategorieId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Uploader).WithMany(u => u.Documents)
                .HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentSegment>(e =>
        {
            e.ToTable("document_segments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DocumentId).HasColumnName("document_id");
            e.Property(x => x.ContenuTexte).HasColumnName("contenu_texte").IsRequired();
            e.Property(x => x.Ordre).HasColumnName("ordre");
            e.Property(x => x.NumeroPage).HasColumnName("numero_page");
            e.Property(x => x.Embedding).HasColumnName("embedding").HasColumnType("TEXT");

            e.HasOne(x => x.Document).WithMany(d => d.Segments)
                .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Formation>(e =>
        {
            e.ToTable("formations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Titre).HasColumnName("titre").HasMaxLength(255).IsRequired();
            e.Property(x => x.Objectifs).HasColumnName("objectifs");
            e.Property(x => x.DureeEstimee).HasColumnName("duree_estimee").HasColumnType("decimal(5,2)");
            e.Property(x => x.Modules).HasColumnName("modules");
            e.Property(x => x.Activites).HasColumnName("activites");
            e.Property(x => x.MethodesEvaluation).HasColumnName("methodes_evaluation");
            e.Property(x => x.Statut).HasColumnName("statut").HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreePar).HasColumnName("cree_par");
            e.Property(x => x.DateCreation).HasColumnName("date_creation");
            // Usage-analytics dashboard range-scans by creation date (formations generated this month,
            // 30-day activity timeline) — matches documents.date_ajout already having the same index.
            e.HasIndex(x => x.DateCreation);

            e.HasOne(x => x.Createur).WithMany(u => u.Formations)
                .HasForeignKey(x => x.CreePar).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FormationDocument>(e =>
        {
            e.ToTable("formation_documents");
            e.HasKey(x => new { x.FormationId, x.DocumentId });
            e.Property(x => x.FormationId).HasColumnName("formation_id");
            e.Property(x => x.DocumentId).HasColumnName("document_id");
            e.Property(x => x.ScorePertinence).HasColumnName("score_pertinence").HasColumnType("decimal(5,4)");

            e.HasOne(x => x.Formation).WithMany(f => f.FormationDocuments)
                .HasForeignKey(x => x.FormationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Document).WithMany(d => d.FormationDocuments)
                .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("conversations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UtilisateurId).HasColumnName("utilisateur_id");
            e.Property(x => x.DateCreation).HasColumnName("date_creation");

            e.HasOne(x => x.Utilisateur).WithMany(u => u.Conversations)
                .HasForeignKey(x => x.UtilisateurId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ConversationId).HasColumnName("conversation_id");
            e.Property(x => x.Emetteur).HasColumnName("emetteur").HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Contenu).HasColumnName("contenu").IsRequired();
            e.Property(x => x.SourcesDocuments).HasColumnName("sources_documents");
            e.Property(x => x.DateEnvoi).HasColumnName("date_envoi");

            e.HasOne(x => x.Conversation).WithMany(c => c.Messages)
                .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlerteDoublon>(e =>
        {
            e.ToTable("alertes_doublons");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DocumentId).HasColumnName("document_id");
            e.Property(x => x.DocumentSimilaireId).HasColumnName("document_similaire_id");
            e.Property(x => x.ScoreSimilarite).HasColumnName("score_similarite").HasColumnType("decimal(5,4)");
            e.Property(x => x.Decision).HasColumnName("decision").HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.DateDetection).HasColumnName("date_detection");

            e.HasOne(x => x.Document).WithMany()
                .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DocumentSimilaire).WithMany()
                .HasForeignKey(x => x.DocumentSimilaireId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExportHistorique>(e =>
        {
            e.ToTable("exports_historique");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FormationId).HasColumnName("formation_id");
            e.Property(x => x.UtilisateurId).HasColumnName("utilisateur_id");
            e.Property(x => x.Format).HasColumnName("format").HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.CheminFichier).HasColumnName("chemin_fichier").HasMaxLength(500);
            e.Property(x => x.DateExport).HasColumnName("date_export");

            e.HasOne(x => x.Formation).WithMany(f => f.Exports)
                .HasForeignKey(x => x.FormationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Utilisateur).WithMany()
                .HasForeignKey(x => x.UtilisateurId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UtilisateurId).HasColumnName("utilisateur_id");
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(100);
            e.Property(x => x.Contenu).HasColumnName("contenu").HasMaxLength(500);
            e.Property(x => x.Lien).HasColumnName("lien").HasMaxLength(255);
            e.Property(x => x.Lue).HasColumnName("lue").HasDefaultValue(false);
            e.Property(x => x.DateCreation).HasColumnName("date_creation");

            e.HasOne(x => x.Utilisateur).WithMany(u => u.Notifications)
                .HasForeignKey(x => x.UtilisateurId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JournalActivite>(e =>
        {
            e.ToTable("journal_activite");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UtilisateurId).HasColumnName("utilisateur_id");
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(150).IsRequired();
            e.Property(x => x.EntiteConcernee).HasColumnName("entite_concernee").HasMaxLength(100);
            e.Property(x => x.EntiteId).HasColumnName("entite_id");
            e.Property(x => x.DateAction).HasColumnName("date_action");
            // Usage-analytics dashboard filters this table by action + date range together
            // (e.g. GENERATE_FORMATION this month) — a composite index serves that directly.
            e.HasIndex(x => new { x.Action, x.DateAction });

            e.HasOne(x => x.Utilisateur).WithMany(u => u.JournalActivites)
                .HasForeignKey(x => x.UtilisateurId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.ToTable("chat_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ExpediteurId).HasColumnName("expediteur_id");
            e.Property(x => x.DestinataireId).HasColumnName("destinataire_id");
            e.Property(x => x.Contenu).HasColumnName("contenu").IsRequired();
            e.Property(x => x.DateEnvoi).HasColumnName("date_envoi");
            e.Property(x => x.Lu).HasColumnName("lu").HasDefaultValue(false);

            e.HasIndex(x => x.DestinataireId);
            e.HasIndex(x => new { x.ExpediteurId, x.DestinataireId });
            e.HasIndex(x => x.DateEnvoi);

            e.HasOne(x => x.Expediteur).WithMany()
                .HasForeignKey(x => x.ExpediteurId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Destinataire).WithMany()
                .HasForeignKey(x => x.DestinataireId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
