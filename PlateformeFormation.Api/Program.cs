using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PlateformeFormation.Api.Data;
using PlateformeFormation.Api.Services;
using PlateformeFormation.Api.Models;
using PlateformeFormation.Api.Hubs;

static async Task MigrateDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await db.Database.MigrateAsync();

    // One-off repair for rows created by a long-fixed bug that left invalid zero dates in older
    // databases. A server running in strict SQL mode (the default on managed hosts like Aiven)
    // rejects the zero-date literal outright before it can even check whether any row matches, so
    // this is wrapped defensively — it's a no-op on any database that never had the bug.
    try
    {
        await db.Database.ExecuteSqlRawAsync("UPDATE utilisateurs SET date_creation = UTC_TIMESTAMP() WHERE date_creation = '0000-00-00 00:00:00';");
        await db.Database.ExecuteSqlRawAsync("UPDATE utilisateurs SET date_validation = date_creation WHERE date_validation = '0000-00-00 00:00:00';");
    }
    catch (MySqlConnector.MySqlException)
    {
        // Strict sql_mode rejected the zero-date literal — nothing to clean up on this database.
    }
}

// Seeds/keeps in sync a bootstrap admin account from configuration (DevelopmentAdmin:Email/Password).
// Not restricted to the Development environment: it's the only way to get a first admin login on a
// freshly deployed instance, and it's a no-op unless those config values are actually set.
static async Task EnsureBootstrapAdminAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var adminEmail = configuration["DevelopmentAdmin:Email"];
    var adminPassword = configuration["DevelopmentAdmin:Password"];
    var adminName = configuration["DevelopmentAdmin:Nom"] ?? "Administrator";

    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword)) return;

    var admin = await db.Utilisateurs.FirstOrDefaultAsync(user => user.Email == adminEmail);

    if (admin is null)
    {
        db.Utilisateurs.Add(new Utilisateur
        {
            Nom = adminName, Email = adminEmail,
            MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            Role = RoleUtilisateur.ADMINISTRATEUR, StatutCompte = StatutCompte.VALIDE,
            Actif = true, DateValidation = DateTime.UtcNow, DateCreation = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return;
    }

    // Only re-hash if the stored hash no longer matches (avoids expensive BCrypt on every startup)
    if (string.IsNullOrEmpty(admin.MotDePasseHash) || !BCrypt.Net.BCrypt.Verify(adminPassword, admin.MotDePasseHash))
        admin.MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);

    if (admin.Role != RoleUtilisateur.ADMINISTRATEUR) admin.Role = RoleUtilisateur.ADMINISTRATEUR;
    if (admin.StatutCompte != StatutCompte.VALIDE) admin.StatutCompte = StatutCompte.VALIDE;
    if (!admin.Actif) admin.Actif = true;
    if (admin.DateValidation is null) admin.DateValidation = DateTime.UtcNow;
    await db.SaveChangesAsync();
}

PdfSharpCore.Fonts.GlobalFontSettings.FontResolver = new PlateformeFormation.Api.Helpers.AppFontResolver();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// CORS — allowed origins come from configuration (Cors:AllowedOrigins, comma-separated) so the
// deployed frontend's real URL doesn't have to be hardcoded; falls back to the local dev server.
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader().AllowAnyMethod()
              .AllowCredentials());
});

// Rate limiting — partitioned per IP so one client cannot exhaust the quota for everyone
builder.Services.AddRateLimiter(options =>
{
    // Login: 5 attempts per IP per minute
    options.AddPolicy<string>("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // Forgot-password: 3 requests per IP per 15 minutes
    options.AddPolicy<string>("forgot-password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            "fp:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // General auth endpoints: 20 per IP per minute
    options.AddPolicy<string>("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            "auth:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many requests. Please try again later." });
    };
});

// Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAiSuggestionService, AiSuggestionService>();
builder.Services.AddScoped<IChunkingService, ChunkingService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IDocumentIndexingService, DocumentIndexingService>();
builder.Services.AddScoped<IRetrievalService, RetrievalService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IDocumentSummaryService, DocumentSummaryService>();
builder.Services.AddScoped<IDocumentCompetenceService, DocumentCompetenceService>();
builder.Services.AddScoped<IFormationGenerationService, FormationGenerationService>();
builder.Services.AddScoped<IFormationExportService, FormationExportService>();
builder.Services.AddScoped<IFormationPptxExportService, FormationPptxExportService>();
builder.Services.AddScoped<IFormationQualityService, FormationQualityService>();
builder.Services.AddScoped<IFormationTraceabilityService, FormationTraceabilityService>();
builder.Services.AddScoped<IFormationCorrectionService, FormationCorrectionService>();
builder.Services.AddScoped<IFormationModuleRegenerationService, FormationModuleRegenerationService>();

// JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
    // SignalR WebSocket connections cannot set the Authorization header, so the
    // client passes the token via query string instead for the hub endpoint.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) &&
                context.HttpContext.Request.Path.StartsWithSegments("/hub/chat"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Global exception handler — returns JSON 500 instead of HTML error pages
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hub/chat");

await MigrateDatabaseAsync(app.Services);
await EnsureBootstrapAdminAsync(app.Services);

app.Run();
