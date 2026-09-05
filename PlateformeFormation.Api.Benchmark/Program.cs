using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlateformeFormation.Api.Services;

// Standalone reliability benchmark — deliberately outside the xUnit/CI suite. Makes real Groq calls,
// costs real quota, and takes minutes to run. Purpose: measure how often GenerateAsync's blocking
// generate -> validate -> correct loop (max 3 attempts) actually converges on real content, and
// whether any specific rule fails systematically — the gate the user set before starting Step 4
// (PDF template): if failure-after-3-attempts exceeds ~10-15%, or a rule fails systematically, the
// system prompt gets adjusted first.

var configPath = Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "PlateformeFormation.Api", "appsettings.Development.json");
configPath = Path.GetFullPath(configPath);

var config = new ConfigurationBuilder()
    .AddJsonFile(configPath, optional: false)
    .Build();

var services = new ServiceCollection();
services.AddHttpClient();
services.AddSingleton<IConfiguration>(config);
services.AddSingleton<IFormationQualityService, FormationQualityService>();
services.AddSingleton<IFormationGenerationService, FormationGenerationService>();
var provider = services.BuildServiceProvider();

var generation = provider.GetRequiredService<IFormationGenerationService>();

var fixtures = Fixtures.All;
var results = new List<FormationRunResult>();

Console.WriteLine($"Benchmark de fiabilité — {fixtures.Count} formations, max 3 tentatives chacune.\n");

// Groq free tier caps at 12000 tokens/minute, and a single generation call already requests
// 6500-8500 (system prompt + RAG source + reserved output) — over half the budget in one shot. Two
// calls inside the same rolling 60s window collide no matter how short the gap between them, so
// every wait (retry or inter-formation) is floored at just over a minute — not the server's often-
// optimistic "try again in Xs", which can still land inside the same window as the previous attempt.
const int MaxRateLimitRetries = 12;
const double MinRateLimitWaitSeconds = 65;
var rateLimitDelayRegex = new Regex(@"try again in (?:(\d+)h)?(?:(\d+)m)?([\d.]+)s", RegexOptions.IgnoreCase);

foreach (var (index, fixture) in fixtures.Select((f, i) => (i + 1, f)))
{
    Console.WriteLine($"[{index}/{fixtures.Count}] {fixture.Objectif[..Math.Min(60, fixture.Objectif.Length)]}...");

    var attempts = new List<GenerationAttemptLog>();
    void OnAttempt(GenerationAttemptLog log) => attempts.Add(log);

    for (var rateLimitRetry = 0; ; rateLimitRetry++)
    {
        try
        {
            await generation.GenerateAsync(fixture.Objectif, fixture.Sources, OnAttempt);
            results.Add(new FormationRunResult(fixture.Objectif, true, attempts.Count, attempts, null));
            Console.WriteLine($"  -> OK en {attempts.Count} tentative(s)");
            break;
        }
        catch (FormationValidationFailedException ex)
        {
            results.Add(new FormationRunResult(fixture.Objectif, false, attempts.Count, attempts, ex.Message));
            Console.WriteLine($"  -> ÉCHEC après {attempts.Count} tentative(s)");
            break;
        }
        catch (Exception ex) when (ex.Message.Contains("429") && rateLimitRetry < MaxRateLimitRetries)
        {
            var match = rateLimitDelayRegex.Match(ex.Message);
            var parsedSeconds = match.Success
                ? (match.Groups[1].Success ? double.Parse(match.Groups[1].Value) * 3600 : 0)
                  + (match.Groups[2].Success ? double.Parse(match.Groups[2].Value) * 60 : 0)
                  + double.Parse(match.Groups[3].Value)
                : MinRateLimitWaitSeconds;
            var waitSeconds = Math.Max(parsedSeconds + 2, MinRateLimitWaitSeconds);
            Console.WriteLine($"  -> rate limit Groq, nouvelle tentative dans {waitSeconds:F0}s...");
            attempts.Clear();
            await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
        }
        catch (Exception ex)
        {
            results.Add(new FormationRunResult(fixture.Objectif, false, attempts.Count, attempts, $"Erreur technique : {ex.Message}"));
            Console.WriteLine($"  -> ERREUR TECHNIQUE : {ex.Message}");
            break;
        }
    }

    // Space out formations too, not just retries — a single call already uses over half the per-minute
    // budget, so anything less than a full window risks colliding with the previous formation's usage.
    await Task.Delay(TimeSpan.FromSeconds(MinRateLimitWaitSeconds));
}

// --- Agrégation ---
var total = results.Count;
var failed = results.Count(r => !r.Success);
var failureRate = total == 0 ? 0 : 100.0 * failed / total;

var attemptsDistribution = results
    .GroupBy(r => r.AttemptsUsed)
    .OrderBy(g => g.Key)
    .Select(g => $"{g.Key} tentative(s) : {g.Count()} formation(s)");

// Which rule labels (before the " : detail" split) show up in ECHEC lists across attempts, counted
// once per formation (not once per attempt) so a rule failing repeatedly on the same formation across
// its 3 attempts doesn't inflate the "systematic" signal.
var ruleFailureCounts = results
    .SelectMany(r => r.Attempts.SelectMany(a => a.ErreursEchec).Select(e => e.Split(" : ")[0]).Distinct())
    .GroupBy(label => label)
    .OrderByDescending(g => g.Count())
    .Select(g => $"{g.Key} : échoue dans {g.Count()}/{total} formations ({100.0 * g.Count() / total:F0}%)");

Console.WriteLine("\n=== RÉSULTATS ===");
Console.WriteLine($"Total : {total} formations");
Console.WriteLine($"Échecs après 3 tentatives : {failed} ({failureRate:F1}%)");
Console.WriteLine("\nRépartition du nombre de tentatives nécessaires :");
foreach (var line in attemptsDistribution) Console.WriteLine($"  {line}");

Console.WriteLine("\nRègles en échec, toutes tentatives confondues (candidates à un échec systématique) :");
foreach (var line in ruleFailureCounts) Console.WriteLine($"  {line}");

if (failed > 0)
{
    Console.WriteLine("\nDétail des formations en échec après 3 tentatives :");
    foreach (var r in results.Where(r => !r.Success))
    {
        Console.WriteLine($"\n- \"{r.Objectif[..Math.Min(70, r.Objectif.Length)]}\"");
        foreach (var a in r.Attempts)
        {
            Console.WriteLine($"    Tentative {a.Attempt} — {(a.EstValide ? "OK" : "ÉCHEC")}");
            foreach (var e in a.ErreursEchec) Console.WriteLine($"      * {e}");
        }
    }
}

var verdict = failureRate <= 15
    ? "Sous le seuil de 15% — le prompt système est considéré fiable, Étape 4 peut démarrer."
    : "AU-DESSUS du seuil de 10-15% — ajuster le prompt système avant l'Étape 4.";
Console.WriteLine($"\n=== VERDICT === {verdict}");

var reportPath = Path.Combine(AppContext.BaseDirectory, "benchmark-report.json");
File.WriteAllText(reportPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"\nRapport détaillé écrit dans : {reportPath}");

record FormationRunResult(string Objectif, bool Success, int AttemptsUsed, List<GenerationAttemptLog> Attempts, string? FailureMessage);
