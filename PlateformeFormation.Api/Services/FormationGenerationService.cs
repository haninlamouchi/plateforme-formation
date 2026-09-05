using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;
using static PlateformeFormation.Api.Helpers.FormationContentParser;
using static PlateformeFormation.Api.Helpers.FormationHarmonizer;

namespace PlateformeFormation.Api.Services;

// Thrown when the formation is still invalid after MaxAttempts — distinct from a generic Groq/HTTP
// failure so FormationController can catch it specifically and return a clear, detailed 422 instead
// of falling through to the generic 500 handler (which would swallow the exact validation errors).
public class FormationValidationFailedException(string message) : Exception(message);

// Generation is now a blocking generate -> validate -> targeted-correct loop (2026-08 v3 spec, Part
// 4): up to 3 total attempts, using FormationQualityReport.EstValide (the same checks the "Corriger"
// button uses, just the strict view) as the gate. If still invalid after 3 attempts, this throws
// rather than ever persisting a formation with known errors — the caller (FormationController) must
// not swallow that into a silent BROUILLON save.
public class FormationGenerationService : IFormationGenerationService
{
    private const int MaxAttempts = 3;
    // Formerly hosted on NVIDIA (to dodge Groq's daily quota) via meta/llama-3.3-70b-instruct, which
    // reached end-of-life on NVIDIA (2026-08-26, HTTP 410 Gone). Its replacement
    // (nvidia/llama-3.1-nemotron-70b-instruct, still listed in GET /v1/models) then 404'd with
    // "Function not found for account" — and so did every other NVIDIA model tried, including with a
    // fresh active API key, pointing at an account-level provisioning issue on NVIDIA's side rather
    // than a model-naming problem. Moved back onto Groq — already proven working in this exact app
    // (ChatbotService, AiSuggestionService both use it successfully) — using the same
    // openai/gpt-oss-120b model and reasoning_effort="low" recipe ChatbotService already validated.
    // The earlier "~4-11+ minutes per generation" latency concern for gpt-oss-120b was specific to
    // NVIDIA's shared free-tier infra, not Groq's.
    private const string Model = "openai/gpt-oss-120b";
    private const int MaxContentCharsPerDoc = 5000;

    private static readonly JsonSerializerOptions SnakeCaseOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Tone/register block kept from prior prompt iterations (the structural spec below doesn't cover
    // style) + Part 1 (content-depth requirements) + the full Part 2 schema + Part 3's R1-R13 rules
    // reformulated as positive instructions (how to do it right, not just "avoid X") + Part 4's
    // documentary-grounding requirement.
    private const string SystemPrompt =
        "Tu es un ingénieur pédagogique expert dans un cabinet de conseil en formation professionnelle " +
        "haut de gamme (type Cegos, Orsys, Korn Ferry), chargé de générer une fiche de formation " +
        "professionnelle complète et rigoureuse. Le ton est celui d'un cabinet de conseil : rigoureux, " +
        "crédible, précis — jamais familier, jamais ludique. INTERDIT à vie : \"comprendre les " +
        "fondamentaux\", \"mettre en pratique\", \"s'initier à\", \"découvrir\", \"sensibiliser à\", tout " +
        "vocabulaire ludique/gaming, tout emoji.\n\n" +

        "## ANCRAGE DOCUMENTAIRE (obligatoire)\n" +
        "Avant de générer quoi que ce soit : identifie dans le contexte fourni les compétences, méthodes " +
        "et formats déjà utilisés dans les documents de référence proches du sujet demandé. Réutilise le " +
        "vocabulaire et le niveau de granularité des documents fournis en exemple. Si le contexte ne " +
        "contient aucune information pertinente pour une section, indique-le dans \"lacunes_contexte\" " +
        "plutôt que d'inventer un contenu non fondé. Ne cite jamais un document qui n'apparaît pas " +
        "réellement dans le contexte fourni.\n\n" +

        "## EXIGENCES DE PROFONDEUR (obligatoire pour chaque module)\n" +
        "- \"contenu\" : au moins 4 points, chacun une PHRASE COMPLÈTE qui explique CE QUI est enseigné " +
        "et POURQUOI c'est pertinent pour le public cible — jamais un mot-clé isolé (ex, pas \"Modélisation " +
        "ML\" mais \"Construction d'un modèle de classification supervisée à partir d'un jeu de données " +
        "réel, avec interprétation des métriques de précision et de rappel.\").\n" +
        "- \"exercice_formatif\" : une \"consigne\" complète et actionnable citant un cas concret (jamais " +
        "\"exercice guidé\" seul), au moins 2 \"criteres_reussite\" précis, le \"materiel\" nécessaire, une " +
        "\"duree_min\".\n" +
        "- \"livrable\" : décris le format ET le contenu attendu, jamais un titre seul.\n" +
        "- \"grille_evaluation\" : 2 à 4 critères précis notant ce livrable, chacun avec un \"pct\".\n" +
        "- \"notes_formateur\" : 2 à 3 points d'attention concrets pour l'intervenant (erreurs fréquentes, " +
        "où ralentir, quand accélérer).\n" +
        "- \"contexte\" (niveau formation) : 3 à 4 phrases substantielles reflétant le thème PRÉCIS de " +
        "cette formation, jamais un paragraphe générique.\n" +
        "- \"benefices_entreprise\" : au moins 3, chacun avec une \"justification\" d'une phrase.\n" +
        "- \"prerequis_detailles\" : le niveau exact attendu (ex: \"Savoir écrire une boucle et une " +
        "fonction en Python\"), jamais une généralité.\n\n" +

        "## STRUCTURE DE SORTIE (JSON strict — aucun texte hors JSON)\n" +
        "{\n" +
        "  \"titre\": string, \"sous_titre\": string, \"contexte\": string, \"public_cible\": string,\n" +
        "  \"prerequis_detailles\": string, \"duree_totale_heures\": number,\n" +
        "  \"modalites_pratiques\": { \"format\": string, \"intervenant\": string, \"materiel_requis\": [string] },\n" +
        "  \"benefices_entreprise\": [ { \"titre\": string, \"justification\": string } ],\n" +
        "  \"sources_utilisees\": [string], \"lacunes_contexte\": [string],\n" +
        "  \"test_positionnement\": { \"objectif\": string, \"qcm_questions\": number, \"exercice\": string, " +
        "\"seuil_parcours_standard_pct\": number, \"module_remediation\": number },\n" +
        "  \"modules\": [ {\n" +
        "    \"numero\": number, \"titre\": string, \"duree_heures\": number,\n" +
        "    \"objectif\": string,\n" +
        "    \"contenu\": [string],\n" +
        "    \"methode\": { \"type\": string, \"pct_theorie\": number, \"pct_pratique\": number },\n" +
        "    \"exercice_formatif\": { \"type\": string, \"consigne\": string, \"criteres_reussite\": [string], " +
        "\"materiel\": string, \"duree_min\": number },\n" +
        "    \"livrable\": string, \"reutilise_livrable_module\": number|null,\n" +
        "    \"competences_prerequises\": [number],\n" +
        "    \"grille_evaluation\": [ { \"critere\": string, \"pct\": number } ],\n" +
        "    \"notes_formateur\": [string]\n" +
        "  } ],\n" +
        "  \"module_bonus\": { \"inclus_dans_tronc_commun\": boolean, \"titre\": string, \"duree_heures\": " +
        "number, \"contenu\": [string] } | null,\n" +
        "  \"activites_pedagogiques\": [ { \"nom\": string, \"format\": string, \"description\": string } ],\n" +
        "  \"evaluation\": [ { \"nom\": string, \"pct\": number, \"est_evaluation_continue\": boolean } ],\n" +
        "  \"ressources_pedagogiques\": [string], \"documents_sources\": [string]\n" +
        "}\n\n" +

        "## RÈGLES DE COHÉRENCE (vérifie-les toi-même avant de répondre, elles seront aussi vérifiées en code)\n" +
        "1. La somme des duree_heures de tous les modules DOIT égaler exactement duree_totale_heures. Un " +
        "module_bonus optionnel (inclus_dans_tronc_commun: false) ne compte jamais dans ce total ; s'il " +
        "est inclus (true), ajoute sa durée au total ET fais-le apparaître dans evaluation.\n" +
        "2. Chaque \"objectif\" de module commence UNE SEULE FOIS par \"Être capable de\" ou \"Savoir\" — " +
        "vérifie que tu n'as jamais dupliqué ce préfixe.\n" +
        "3. competences_prerequises d'un module ne référence QUE des numéros strictement inférieurs au " +
        "sien — une compétence est toujours enseignée avant d'être mobilisée.\n" +
        "4. Donne à chaque livrable un texte réellement distinct des autres, même quand " +
        "reutilise_livrable_module est renseigné : formule-le comme une évolution concrète du livrable " +
        "précédent, jamais une répétition proche.\n" +
        "5. Les pondérations d'evaluation totalisent EXACTEMENT 100%.\n" +
        "6. Inclus toujours une entrée d'évaluation continue (est_evaluation_continue: true) pondérée " +
        "entre 20 et 30%.\n" +
        "7. Ne mentionne jamais d'attestation ni de certification dans evaluation — ce sont des " +
        "conséquences de la réussite, jamais des éléments notés.\n" +
        "8. module_remediation doit être le numéro du module dont le contenu correspond réellement à la " +
        "compétence évaluée par test_positionnement.objectif — jamais un numéro choisi arbitrairement.\n" +
        "9. Si module_bonus.inclus_dans_tronc_commun est vrai, donne-lui toujours une duree_heures " +
        "exploitable et fais-le apparaître dans evaluation.\n" +
        "10. ressources_pedagogiques contient au moins 3 éléments, tous distincts de sources_utilisees " +
        "et documents_sources — jamais une reformulation du nom d'un document source.\n" +
        "11. Chaque module.contenu a au moins 4 éléments, chacun une phrase complète de plus de 8 mots.\n" +
        "12. exercice_formatif.consigne fait plus de 15 mots et criteres_reussite contient au moins 2 " +
        "éléments.\n" +
        "13. Ne génère JAMAIS de champ de planning jour par jour — cette répartition est calculée " +
        "automatiquement par le système à partir de tes modules.\n\n" +

        "Réponds UNIQUEMENT avec le JSON, rien d'autre, sans markdown ni commentaire. Réponds en français.";

    // Used for the internal retry loop only (up to 2 targeted correction calls within GenerateAsync) —
    // distinct from FormationCorrectionService's user-triggered "Corriger" prompt, though the intent is
    // the same: fix exactly what's listed, touch nothing else.
    private const string CorrectionSystemPrompt =
        "Tu es ingénieur pédagogique senior. On te donne un programme de formation déjà rédigé (JSON, " +
        "même schéma que celui décrit précédemment, en snake_case) et une liste de PROBLÈMES PRÉCIS " +
        "détectés automatiquement. Corrige UNIQUEMENT les problèmes listés — ne touche à AUCUN autre " +
        "champ, ne renomme aucun module, ne change aucun \"numero\". Garde EXACTEMENT la même structure " +
        "JSON et les mêmes clés (snake_case) que le programme fourni. Réponds UNIQUEMENT avec le JSON " +
        "corrigé, rien d'autre, sans markdown ni commentaire.";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly IFormationQualityService _quality;

    public FormationGenerationService(IHttpClientFactory factory, IConfiguration config, IFormationQualityService quality)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(90);
        _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey not configured.");
        _quality = quality;
    }

    public async Task<FormationDraft> GenerateAsync(
        string objectif, List<FormationSourceDocument> sources,
        Action<GenerationAttemptLog>? onAttempt = null, CancellationToken ct = default)
    {
        var sourcesText = string.Join("\n\n", sources.Select(s =>
        {
            var content = s.Content.Length > MaxContentCharsPerDoc ? s.Content[..MaxContentCharsPerDoc] : s.Content;
            return $"[Document : \"{s.Titre}\"]\n{content}";
        }));
        var initialUserPrompt = $"## DONNÉES D'ENTRÉE\n\nContexte documentaire (RAG) :\n{sourcesText}\n\n" +
                                 $"Demande du responsable pédagogique :\n{objectif}";

        FormationDraft? draft = null;
        FormationQualityReport? report = null;
        var errorDetails = new List<string>();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            string responseText;
            // The correction branch needs a draft to correct — draft is only ever null on attempt 1,
            // EXCEPT when a previous attempt's response wasn't parseable JSON at all (empty
            // ExtractJson, or a JsonException below), which `continue`s without ever assigning it.
            // The old `BuildCorrectionPayload(draft!)` null-forgiving operator asserted that couldn't
            // happen but never actually prevented it — a genuinely unparseable first response crashed
            // this with a NullReferenceException instead of just retrying. Falling back to the
            // original prompt here is exactly what attempt 1 already does, so an unparseable response
            // now degrades into an ordinary retry instead of a 500.
            if (attempt == 1 || draft is null)
            {
                responseText = await CallLlm(SystemPrompt, initialUserPrompt, ct);
            }
            else
            {
                var problemsText = string.Join("\n", errorDetails.Select(e => $"- {e}"));
                var correctionPrompt = $"Problèmes détectés à corriger (UNIQUEMENT ceux-ci) :\n{problemsText}\n\n" +
                                        $"Programme actuel à corriger :\n{BuildCorrectionPayload(draft)}";
                responseText = await CallLlm(CorrectionSystemPrompt, correctionPrompt, ct);
            }

            var jsonText = ExtractJson(responseText);
            if (jsonText.Length == 0)
            {
                errorDetails = ["La réponse précédente n'était pas un JSON exploitable — réponds uniquement avec le JSON demandé."];
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                draft = BuildDraft(doc.RootElement);
            }
            catch (JsonException)
            {
                errorDetails = ["Le JSON précédent était mal formé — réponds uniquement avec un JSON valide respectant le schéma demandé."];
                continue;
            }

            report = _quality.Evaluate(new Formation
            {
                Objectifs = draft.Objectifs, Modules = draft.Modules,
                MethodesEvaluation = draft.MethodesEvaluation, DureeEstimee = draft.DureeEstimeeHeures,
            });

            errorDetails = report.Checks.Where(c => c.Statut == "ECHEC").Select(c => $"{c.Libelle} : {c.Detail}").ToList();
            onAttempt?.Invoke(new GenerationAttemptLog(attempt, report.EstValide, errorDetails));

            if (report.EstValide) return draft;
        }

        // Never persist a formation with known validation errors — an explicit, detailed failure beats
        // a silent BROUILLON the user would have to discover is broken later.
        throw new FormationValidationFailedException(
            $"La formation générée reste invalide après {MaxAttempts} tentatives : {string.Join(" ; ", errorDetails)}");
    }

    private static FormationDraft BuildDraft(JsonElement root)
    {
        var titre = root.TryGetProperty("titre", out var titreEl) && titreEl.ValueKind == JsonValueKind.String
            ? titreEl.GetString() ?? "" : "";
        decimal? duree = root.TryGetProperty("duree_totale_heures", out var d) && d.ValueKind is JsonValueKind.Number
            ? d.GetDecimal() : null;

        var modulesJson = SanitizeCompetencesPrerequises(GetModulesJson(root));
        var evaluationJson = RescaleEvaluationWeights(RemoveCertificationEntries(GetEvaluationJson(root)));
        var objectifsJson = GetObjectifsJson(root);
        var harmonizedDuree = HarmonizeDureeFromModules(modulesJson, objectifsJson, duree);
        modulesJson = ApplyElisionToObjectifs(EnforceMeasurableObjectivePrefix(modulesJson));
        var activitesJson = GetActivitesJson(root);

        return new FormationDraft(
            Titre: titre.Length > 0 ? titre : "Formation générée",
            Objectifs: objectifsJson,
            DureeEstimeeHeures: harmonizedDuree,
            Modules: modulesJson,
            Activites: activitesJson,
            MethodesEvaluation: evaluationJson
        );
    }

    // Round-trips the current draft (stored as camelCase, via typed records) back into the exact
    // snake_case shape the prompt/schema describes, so the correction call can be parsed by the same
    // Get*Json functions used for the initial generation — one schema, read the same way everywhere.
    private static string BuildCorrectionPayload(FormationDraft draft)
    {
        var objectifs = ParseObject<ObjectifsData>(draft.Objectifs);
        var modules = ParseCards<ModuleCard>(draft.Modules, () => new ModuleCard(
            0, draft.Modules, null, null, null, null, null, null, null, null, null, null, null));
        var evaluation = ParseCards<EvaluationItem>(draft.MethodesEvaluation, () => new EvaluationItem(draft.MethodesEvaluation, null, false));
        var activites = ParseCards<ActivitePedagogique>(draft.Activites, () => new ActivitePedagogique(draft.Activites, null, null));

        var flat = new
        {
            Titre = draft.Titre,
            SousTitre = objectifs?.SousTitre,
            Contexte = objectifs?.Contexte,
            PublicCible = objectifs?.PublicCible,
            PrerequisDetailles = objectifs?.PrerequisDetailles,
            DureeTotaleHeures = draft.DureeEstimeeHeures,
            ModalitesPratiques = objectifs?.ModalitesPratiques,
            BeneficesEntreprise = objectifs?.BeneficesEntreprise,
            SourcesUtilisees = objectifs?.SourcesUtilisees,
            LacunesContexte = objectifs?.LacunesContexte,
            TestPositionnement = objectifs?.TestPositionnement,
            Modules = modules,
            ModuleBonus = objectifs?.ModuleBonus,
            ActivitesPedagogiques = activites,
            Evaluation = evaluation,
            RessourcesPedagogiques = objectifs?.RessourcesPedagogiques,
            DocumentsSources = objectifs?.DocumentsSources,
        };

        return JsonSerializer.Serialize(flat, SnakeCaseOpts);
    }

    private const int MaxTimeoutRetries = 1;

    // Shared free-tier infra (this was originally written for NVIDIA, applies just as well to Groq)
    // has no reserved capacity — under load, a request can occasionally queue with nothing sent back
    // yet. One retry gives it a chance to land on a less-contended moment rather than failing the
    // whole 3-attempt validation loop on pure infra flakiness. Only retries our own HttpClient.Timeout
    // firing — a real caller-initiated cancellation
    // (ct) is never retried, it propagates immediately.
    private async Task<string> CallLlm(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await CallLlmOnce(systemPrompt, userPrompt, ct);
            }
            catch (TaskCanceledException) when (attempt < MaxTimeoutRetries && !ct.IsCancellationRequested)
            {
                // The HttpClient.Timeout fired, not the caller's token — retry once.
            }
        }
    }

    // Streamed rather than a single blocking response — a non-streaming request sits completely idle
    // (zero bytes) for the full generation window, exactly the shape of connection a NAT/proxy on the
    // client's network path is prone to silently kill, which then surfaces as a confusing client-side
    // timeout with no real server-side failure. Streaming keeps bytes flowing continuously as each
    // token arrives, so the connection is never mistaken for idle.
    private async Task<string> CallLlmOnce(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_tokens = 4600,
            temperature = 0.45,
            stream = true,
            // gpt-oss models spend part of max_tokens on internal reasoning before ever writing
            // content — without this, a request can exhaust its token budget mid-reasoning and come
            // back empty. See ChatbotService for the same fix applied there first.
            reasoning_effort = "low"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorJson = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Groq formation request failed ({(int)response.StatusCode}): {errorJson}");
        }

        var sb = new StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:")) continue;
            var payload = line["data:".Length..].Trim();
            if (payload.Length == 0 || payload == "[DONE]") continue;

            // A malformed/truncated SSE chunk (dropped bytes, a proxy cutting the connection mid-line)
            // used to throw JsonException straight out of CallLlmOnce — CallLlm's retry loop only
            // catches TaskCanceledException, so this propagated all the way past the outer
            // generation-attempt loop instead of being treated as a retryable failure like a timeout
            // is. Skipping just this one chunk keeps the stream going; if it corrupts the accumulated
            // JSON badly enough to matter, the existing "malformed final JSON" retry-with-correction-
            // prompt path already handles that gracefully.
            JsonDocument chunk;
            try { chunk = JsonDocument.Parse(payload); }
            catch (JsonException) { continue; }
            using (chunk)
            {
                var choices = chunk.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0) continue;

                if (choices[0].GetProperty("delta").TryGetProperty("content", out var contentEl)
                    && contentEl.ValueKind == JsonValueKind.String)
                {
                    sb.Append(contentEl.GetString());
                }
            }
        }

        return sb.ToString().Trim();
    }
}
