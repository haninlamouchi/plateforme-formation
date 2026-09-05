# Plateforme Formation

Plateforme web de gestion documentaire et de conception de formations. Elle permet aux responsables pédagogiques de centraliser leurs ressources, d’en extraire des connaissances avec l’IA et de construire, améliorer puis exporter des parcours de formation.

## Fonctionnalités

### Comptes et sécurité

- Inscription avec demande de validation et choix du rôle **Responsable pédagogique**.
- Connexion par e-mail/mot de passe ou Google, JWT et renouvellement de session par refresh token.
- Réinitialisation de mot de passe par e-mail, modification de mot de passe et gestion du profil (dont photo/avatar).
- Administration des comptes : validation, refus, consultation, modification, désactivation et réactivation.
- Protection des routes par rôle, limitation de débit sur les routes d’authentification et en-têtes HTTP de sécurité.

### Documents et catégories

- Téléversement, consultation, mise à jour, suppression et pagination des documents.
- Organisation par catégories ; création, modification et suppression réservées aux administrateurs.
- Suggestions de catégories, y compris à partir d’un document PDF.
- Extraction de texte PDF et indexation automatique après import.
- Recherche sémantique dans les ressources. Les documents image/scannés ne sont pas encore pris en charge (pas d’OCR).

### Assistant IA documentaire

- Assistant conversationnel RAG : il recherche les passages les plus pertinents dans les documents autorisés avant de répondre.
- Sources retournées avec les réponses, avec filtrage des documents selon le propriétaire pour les non-administrateurs.
- Résumé conversationnel d’un document, mis en cache pour éviter des appels IA inutiles.
- Identification des compétences pédagogiques d’un document, regroupées par domaine dans une fenêtre dédiée.
- Embeddings Hugging Face et génération Groq ; les documents sont découpés en segments pour la recherche.

### Création et suivi des formations

- Génération assistée par IA d’une formation à partir d’un objectif et des documents les plus pertinents.
- Proposition de titre, objectifs, durée, modules, activités pédagogiques et méthodes d’évaluation.
- Édition complète des contenus générés, gestion des brouillons, validation et suppression.
- Rapport qualité, prévisualisation de corrections IA et traçabilité des sources associées aux modules.
- Export PDF (document complet) ou PowerPoint (diaporama synthétique), avec historique des exports.

### Collaboration et expérience utilisateur

- Messagerie temps réel avec salon général et conversations privées via SignalR.
- Notifications de nouveaux messages, compteur de notifications non lues et marquage comme lu.
- Tableau de bord, interface responsive, thèmes clair/sombre et interface française/anglaise.

## Architecture

| Élément | Technologies |
| --- | --- |
| Frontend | React 19, Vite, React Router, Axios, Framer Motion, SignalR |
| API | ASP.NET Core 9, Entity Framework Core, Swagger |
| Base de données | MySQL avec Pomelo EF Core |
| Authentification | JWT, BCrypt, Google OAuth |
| IA | Groq (génération) et Hugging Face Inference Providers (embeddings) |
| Export | PdfPig (lecture PDF), PdfSharpCore (export PDF) et DocumentFormat.OpenXml (export PowerPoint) |
| E-mail local | Mailpit (optionnel) |

## Prérequis

- .NET SDK 9
- Node.js 20+ et npm
- MySQL 8+ accessible localement
- Docker (facultatif, pour Mailpit)
- Des clés Groq, Hugging Face et NVIDIA pour les fonctions IA ; un client Google OAuth est nécessaire pour la connexion Google.

## Installation et lancement

1. Créez une base MySQL nommée `plateforme_formation`.
2. Créez `PlateformeFormation.Api/appsettings.Development.json` (ce fichier est volontairement ignoré par Git) et renseignez vos paramètres. Exemple minimal :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=plateforme_formation;User=root;Password=VOTRE_MOT_DE_PASSE;"
  },
  "Jwt": {
    "Key": "une-cle-secrete-aleatoire-d-au-moins-32-caracteres",
    "Issuer": "PlateformeFormation",
    "Audience": "PlateformeFormationUsers",
    "ExpiryMinutes": 120
  },
  "Google": { "ClientId": "VOTRE_CLIENT_ID" },
  "Groq": { "ApiKey": "VOTRE_CLE_GROQ" },
  "Nvidia": { "ApiKey": "VOTRE_CLE_NVIDIA" },
  "HuggingFace": { "ApiKey": "VOTRE_CLE_HUGGING_FACE" }
}
```

3. Dans un premier terminal, lancez l’API. Les migrations sont appliquées automatiquement au démarrage en environnement `Development` :

```powershell
cd PlateformeFormation.Api
dotnet restore
dotnet run
```

L’API est accessible sur `http://localhost:5092`; Swagger est disponible à `http://localhost:5092/swagger` en développement.

4. Dans un second terminal, installez et lancez le frontend :

```powershell
cd frontend
npm install
npm run dev
```

L’application est alors servie sur `http://localhost:5173`.

Par défaut, le client appelle `http://localhost:5092/api`. Pour utiliser une autre API, créez `frontend/.env.local` :

```env
VITE_API_BASE_URL=http://localhost:5092/api
```

## E-mail de développement (facultatif)

Mailpit permet de visualiser les e-mails de validation et de réinitialisation sans serveur SMTP externe :

```powershell
docker compose -f docker-compose.mailpit.yml up -d
```

Configurez ensuite l’hôte SMTP sur `localhost`, le port sur `1025` et désactivez SSL dans votre configuration de développement. L’interface Mailpit est disponible sur `http://localhost:8025`.

## Commandes utiles

```powershell
# API : compiler et exécuter les tests
dotnet build PlateformeFormation.Api\PlateformeFormation.Api.csproj
dotnet test PlateformeFormation.Api.Tests\PlateformeFormation.Api.Tests.csproj

# Frontend : contrôler le code et générer le build de production
cd frontend
npm run lint
npm run build
```

## Rôles

| Rôle | Accès |
| --- | --- |
| Responsable pédagogique | Documents personnels, assistant IA, formations, profil et messagerie |
| Administrateur | Tous les accès ci-dessus, plus gestion des utilisateurs, validations et catégories |

## Structure du dépôt

```text
PlateformeFormation.Api/        API ASP.NET Core, migrations, services et hub SignalR
PlateformeFormation.Api.Tests/  Tests unitaires
PlateformeFormation.Api.Benchmark/  Projet de benchmarks
frontend/                       Application React/Vite
docs/                           Notes techniques, dont le fonctionnement du chatbot RAG
docker-compose.mailpit.yml      Service e-mail de développement
```

## Notes de sécurité

Ne versionnez jamais `appsettings.Development.json`, les fichiers `.env*`, les clés JWT, Groq, Hugging Face, NVIDIA ou les identifiants SMTP. Avant une mise en production, remplacez les valeurs de démonstration présentes dans `appsettings.json` par une configuration sécurisée injectée via secrets ou variables d’environnement.
