# Chatbot IA (RAG sur documents) — Plan & suivi

## Décisions d'architecture

- **Retrieval** : embeddings vectoriels réels (similarité cosinus calculée en C#, pas de pgvector — MySQL)
- **Provider embeddings** : HuggingFace Inference Providers (router), modèle `microsoft/harrier-oss-v1-0.6b` (gratuit, multilingue FR/EN, vecteurs dim 1024)
  - ⚠️ `sentence-transformers/all-MiniLM-L6-v2` envisagé initialement, mais **non disponible** sur le nouveau système "Inference Providers" (`Model not supported by provider hf-inference`) → remplacé par `harrier-oss-v1-0.6b`, confirmé actif sur le provider `hf-inference`.
  - ⚠️ L'ancien domaine `api-inference.huggingface.co` n'existe plus (DNS mort). Le bon domaine est `router.huggingface.co`, endpoint : `POST https://router.huggingface.co/hf-inference/models/{model}`.
- **Génération** : Groq API (`llama-3.3-70b-versatile`), déjà utilisé dans `AiSuggestionService`
- **Stockage vecteurs** : `DocumentSegment.VecteurId` renommé en `Embedding` (colonne `TEXT`, JSON d'un `float[]`)
- **Extraction PDF** : `UglyToad.PdfPig`, déjà utilisé dans `DocumentController` et `CategorieController`

## Étapes

- [x] **Étape 1** — Découpage en chunks
  Service qui prend le texte extrait d'un PDF et le découpe en chunks (~500-800 tokens, chevauchement ~100). Remplit `DocumentSegment` (ContenuTexte, Ordre) pour un document donné.
  Fichiers : `Helpers/PdfTextExtractor.cs`, `Services/ChunkingService.cs`, `Services/DocumentIndexingService.cs`.
  Test manuel : `POST /api/documents/{id}/chunk` (temporaire, sera remplacé à l'étape 3).

- [x] **Étape 2** — Génération et stockage des embeddings
  `IEmbeddingService`/`EmbeddingService.cs` (appel HuggingFace router, modèle `harrier-oss-v1-0.6b`). `DocumentIndexingService` génère l'embedding de chaque chunk et le stocke dans `DocumentSegment.Embedding` (JSON, 1024 floats).
  Migration EF `AddEmbeddingToDocumentSegments` appliquée (renomme `vecteur_id` → `embedding`, type `TEXT`).
  Clé HuggingFace dans `appsettings.Development.json` → `HuggingFace:ApiKey` (non commité, `.gitignore`).
  Test manuel : `GET /api/documents/{id}/segments` retourne `embeddingDimensions` (doit être 1024).

- [x] **Étape 3** — Déclenchement de l'indexation
  Branché sur l'upload de document (`DocumentController.Upload` appelle `DocumentIndexingService.ChunkDocumentAsync` en try/catch, échec d'indexation n'échoue pas la requête d'upload). `StatutTraitement` suit EN_ATTENTE → EN_COURS → DISPONIBLE/ERREUR.
  Test manuel confirmé : upload d'un document → `statutTraitement: DISPONIBLE`.

- [x] **Étape 4** — Recherche sémantique (retrieval)
  `IRetrievalService`/`RetrievalService.cs` : génère l'embedding de la question, calcule la similarité cosinus en C# contre les segments (`StatutTraitement == DISPONIBLE`), retourne le top-K. Filtres optionnels `documentId`/`categorieId`.
  Test manuel (temporaire) : `POST /api/documents/search` avec `{ query, topK }`.

- [x] **Étape 5** — Génération de réponse (endpoint chatbot)
  `IChatbotService`/`ChatbotService.cs` + `ChatbotController.cs` : `POST /api/chatbot/ask` (question + documentId/catégorie optionnels) → retrieval top-K via `IRetrievalService` → prompt avec contexte (extraits masqués au user) → appel Groq (`llama-3.3-70b-versatile`) → réponse naturelle (sans référence explicite aux "extraits"/"contexte") + `sources` en champ séparé. Filtrage par propriétaire pour les non-admins, comme les autres endpoints documents.
  Test manuel confirmé : réponse cohérente et naturelle, sans fuite du mécanisme RAG dans le texte visible.

- [x] **Étape 6** — Interface chatbot (frontend)
  Bulle de chat flottante (bas-droite), pas de page dédiée. `frontend/src/components/ChatbotWidget.jsx` + `frontend/src/services/chatbotService.js`, montée globalement dans `DashboardLayout.jsx`. Distincte de la messagerie interne existante `Chat.jsx`. Historique de conversation géré côté widget et envoyé à `/api/chatbot/ask`.

- [x] **Étape 7** — Résumé automatique de documents
  Intégré directement dans le chatbot (pas de bouton/page séparée) : `ChatIntentDetector.cs` détecte une intention "résumé" par mots-clés (résum-, synthèse, summar-, "de quoi parle", "en bref") dans la question posée au chatbot. `IDocumentSummaryService`/`DocumentSummaryService.cs` concatène tous les segments du document ciblé (ordre `Ordre`), appelle Groq (`llama-3.3-70b-versatile`) avec un prompt de synthèse (intro + 3-6 points clés), et **met en cache le résultat dans `Document.Resume`** (champ déjà existant en base) pour ne pas régénérer à chaque question.
  Résolution du document ciblé : `ChatbotController.ResolveDocument` — match du titre (sans extension, insensible à la casse/accents) dans la question, sinon overlap de mots-clés du titre, sinon (si l'utilisateur n'a qu'un seul document) sélection implicite ; sinon le chatbot répond en demandant de préciser le titre.
  Test manuel à faire : dans la bulle de chat, poser "résume le document X" (X = titre d'un document déjà indexé), vérifier une réponse cohérente ; reposer la même question et vérifier que la réponse est identique (cache `Resume`).

- [x] **Étape 8** — Identification des compétences pédagogiques
  Également conversationnel, pas de page séparée (la première version avec page admin dédiée avait été retirée le 2026-08-03). `ChatIntentDetector` détecte l'intention "compétences" (compétence, savoir-faire, skill, acquis pédagogique). `IDocumentCompetenceService`/`DocumentCompetenceService.cs` appelle Groq sur le contenu complet du document ciblé et renvoie une liste structurée `{libelle, domaine}` (JSON), pas de persistance (pas de table `Competence`/`DocumentCompetence`) : régénéré à chaque question.
  Le message du chatbot reste court ("Voici les compétences abordées dans le document ...") accompagné d'un bouton "Voir les compétences (n)" sous la bulle — cliquer ouvre une popup (`CompetencesModal` dans `ChatbotWidget.jsx`) qui regroupe les compétences par domaine, chaque domaine ayant une couleur distincte (palette de 8 couleurs assignée par hash du nom de domaine, cohérente à chaque affichage).
  Test manuel à faire : "quelles sont les compétences abordées dans le document X ?" sur un document indexé, cliquer sur le bouton "Voir les compétences" et vérifier le regroupement/couleurs par domaine dans la popup.

- [x] **Étape 9** — Suggestion de contenus, durée, modules/activités/évaluation (plan de formation)
  Contrairement aux étapes 7-8, ceci est une fonctionnalité dédiée (pas conversationnelle) car elle correspond à une entité métier déjà modélisée mais jamais exploitée : `Formation`/`FormationDocument`/`ExportHistorique` existaient dans le schéma (migration initiale) sans aucun controller, service, ni page frontend.
  Flux : l'utilisateur saisit un objectif de formation (+ optionnellement des documents précis) → `FormationController.SelectCandidateDocumentsAsync` réutilise `IRetrievalService` (mêmes embeddings que le chatbot) pour trouver les documents les plus pertinents par rapport à l'objectif, avec un score réel stocké dans `FormationDocument.ScorePertinence` → `IFormationGenerationService`/`FormationGenerationService.cs` envoie à Groq l'objectif + le contenu des documents sélectionnés (résumé en cache si disponible, sinon segments) et reçoit un JSON structuré : titre, objectifs, durée estimée (heures), modules, activités pédagogiques, méthodes d'évaluation → une `Formation` est créée en base avec `Statut = BROUILLON`.
  L'utilisateur relit et édite ensuite tous les champs (rien n'est jamais figé tel que généré par l'IA), peut valider (`Statut = VALIDEE`) ou repasser en brouillon, et supprimer.
  Fichiers : `Dtos/FormationDtos.cs`, `Services/IFormationGenerationService.cs`/`FormationGenerationService.cs`, `Controllers/FormationController.cs` (`GET/PUT/DELETE /api/formations`, `POST /api/formations/generate`, `PUT /api/formations/{id}/statut`). Frontend : `pages/Formations.jsx` (liste + wizard de génération), `pages/FormationDetail.jsx` (édition/validation/suppression), `services/formationService.js`, entrée de navigation "Formations" dans `Sidebar.jsx`, routes `/formations` et `/formations/:id` dans `App.jsx`.
  Pas de migration EF nécessaire (le schéma existait déjà). Aucune régénération auto en base pour l'instant : compilation backend vérifiée (0 erreur CS), build frontend vérifié (`npm run build` OK), **mais rien testé en conditions réelles par l'utilisateur**.
  Test manuel à faire : aller sur "Formations" dans le menu → "Créer une formation" → saisir un objectif concret correspondant à un document déjà indexé → vérifier que le plan généré (modules/activités/évaluation) est cohérent et basé sur le contenu réel → éditer un champ et enregistrer → valider → vérifier le badge et le bouton "Repasser en brouillon".

- [x] **Étape 10** — Export PDF
  `IFormationExportService`/`FormationExportService.cs` génère le PDF avec **PdfSharpCore** (pas QuestPDF — QuestPDF.2026.7.2 échouait systématiquement au téléchargement NuGet, "response ended prematurely", reproduit à la fois en sandbox et sur la machine de l'utilisateur ; PdfSharpCore est un paquet beaucoup plus léger, sans binaires natifs empaquetés, installé sans problème). Layout dessiné manuellement (pas de moteur fluide comme QuestPDF) : titre, méta, objectifs, durée, modules/activités/évaluation (même parsing Markdown-lite que `MarkdownText.jsx`), documents sources — avec retour à la ligne et pagination automatique gérés à la main (`WrapText`/`EnsureSpace`).
  `GET /api/formations/{id}/export` (dans `FormationController.cs`) génère le PDF, l'enregistre sous `wwwroot/uploads/exports/`, logue une ligne `ExportHistorique` (Format=PDF), puis retourne le fichier en téléchargement direct.
  Frontend : bouton "Exporter en PDF" sur `FormationDetail.jsx`, `services/formationService.js#exportFormationPdf` récupère le PDF en `blob` (nécessaire car endpoint authentifié, un lien `<a href>` simple ne porterait pas le JWT) et déclenche le téléchargement navigateur.
  `dotnet build` : 0 erreur, 0 avertissement (build complet cette fois, l'API n'était pas en cours d'exécution). `npm run build` OK.
  Test manuel à faire : sur une formation existante, cliquer "Exporter en PDF", vérifier que le fichier téléchargé est cohérent (titre, sections, pagination si le contenu est long).

## Notes / limites connues
- Les PDF scannés/images ne sont pas traités (pas d'OCR) — limite documentée volontairement, cf. discussion.
- `ChatController`/`ChatHub`/`Chat.jsx` existants = messagerie utilisateur↔utilisateur, **pas** le chatbot IA. Le chatbot IA sera une nouvelle route/page séparée.
