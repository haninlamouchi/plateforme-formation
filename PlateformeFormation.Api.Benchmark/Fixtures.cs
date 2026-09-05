using PlateformeFormation.Api.Services;

// 10 fixtures spanning unrelated professional domains, each with one synthetic-but-concrete source
// document (specific tools/techniques/facts, not generic filler) — grounding content is what the
// system prompt's "ANCRAGE DOCUMENTAIRE" rule expects. Deliberately testing generation/validation
// reliability, not RAG retrieval quality, so sources are handed directly rather than retrieved.
//
// Trimmed down from an original 26-fixture set: Groq's free tier gives 100k tokens/day, and each
// generation call already runs 6500-8500 tokens (system prompt + RAG source + reserved output) — a
// formation needing all 3 attempts can burn ~20-25k tokens on its own. 26 fixtures could need up to
// ~600k tokens in the worst case, several days' worth of free-tier budget even with perfect pacing.
// 10 fixtures fits inside a single day's budget even if every one needs a correction retry.
public static class Fixtures
{
    private static FormationSourceDocument Doc(string titre, string content) => new(1, titre, content);

    public record Fixture(string Objectif, List<FormationSourceDocument> Sources);

    public static readonly List<Fixture> All =
    [
        new("Former les équipes développement à la détection et la remédiation des failles OWASP Top 10",
        [
            Doc("Guide interne sécurité applicative",
                "L'injection SQL reste exploitable dès qu'une requête concatène une entrée utilisateur sans " +
                "requête préparée (PreparedStatement en Java, paramètres nommés en .NET). Le XSS stocké touche " +
                "les champs de commentaires non échappés côté rendu ; la mitigation standard est l'encodage " +
                "contextuel (HTML, JS, URL) via une bibliothèque comme OWASP Java Encoder. L'audit de code doit " +
                "vérifier systématiquement : validation d'entrée, gestion des secrets (jamais en dur dans le " +
                "code), et les en-têtes CSP. Outils utilisés en interne : SonarQube pour l'analyse statique, " +
                "OWASP ZAP pour les tests dynamiques en pré-production.")
        ]),
        new("Former les data engineers à la construction de pipelines ETL robustes avec Apache Airflow",
        [
            Doc("Standards pipeline data — équipe Data Platform",
                "Chaque DAG Airflow doit définir un retry policy explicite (max 3 tentatives, backoff " +
                "exponentiel) et un SLA par tâche. Les transformations utilisent dbt pour la couche de " +
                "modélisation SQL, avec des tests dbt (unique, not_null, relationships) exécutés avant " +
                "publication en production. Le monitoring passe par des alertes Slack sur échec de tâche " +
                "critique. Anti-pattern courant : DAG monolithique de plus de 20 tâches sans découpage par " +
                "domaine fonctionnel, qui rend le debugging quasi impossible en cas d'échec partiel.")
        ]),
        new("Former les nouveaux product managers à la priorisation produit avec le framework RICE",
        [
            Doc("Playbook Produit",
                "Le score RICE (Reach, Impact, Confidence, Effort) sert de filtre initial, jamais de décision " +
                "finale — il doit être complété par un arbitrage stratégique en comité produit mensuel. Reach " +
                "se mesure en nombre d'utilisateurs actifs impactés sur un trimestre, Impact sur une échelle " +
                "de 0,25 (minimal) à 3 (massif). Erreur fréquente chez les nouveaux PM : surestimer la " +
                "Confidence sans données d'usage réelles. Les tickets Jira liés à une feature doivent " +
                "référencer le score RICE dans leur description pour tracer la décision.")
        ]),
        new("Former les commerciaux terrain à la méthode de vente SPIN Selling",
        [
            Doc("Manuel vente B2B",
                "SPIN structure l'entretien en quatre types de questions : Situation (contexte client), " +
                "Problème (douleur identifiée), Implication (conséquences si non résolu), Need-payoff " +
                "(bénéfice de la solution). Erreur classique : enchaîner les questions de Situation sans " +
                "jamais atteindre l'Implication, ce qui laisse le prospect sans urgence perçue. Le CRM " +
                "Salesforce de l'entreprise impose un champ obligatoire \"douleur identifiée\" avant de faire " +
                "passer une opportunité au statut \"qualifiée\".")
        ]),
        new("Former les ingénieurs DevOps à la mise en place d'une stack d'observabilité avec Prometheus et Grafana",
        [
            Doc("Runbook observabilité",
                "Prometheus scrape les métriques exposées par chaque service via un endpoint /metrics au " +
                "format texte, à intervalle de 15 secondes par défaut. Les alertes critiques (latence p99 > " +
                "500ms, taux d'erreur 5xx > 1%) sont définies en PromQL et routées vers Alertmanager puis " +
                "PagerDuty. Grafana centralise les dashboards par service, avec un dashboard \"golden signals\" " +
                "(latence, trafic, erreurs, saturation) obligatoire pour tout service en production.")
        ]),
        new("Former les managers de proximité à la conduite d'entretiens annuels d'évaluation",
        [
            Doc("Guide RH — campagne d'entretiens",
                "L'entretien s'appuie sur des faits observés sur les 12 derniers mois, jamais des impressions " +
                "générales — le manager doit préparer 3 exemples concrets par axe évalué. Le modèle SBI " +
                "(Situation, Behavior, Impact) structure le feedback constructif. Piège fréquent : réserver " +
                "tout le feedback négatif pour l'entretien annuel au lieu de feedbacks continus — ce que la " +
                "politique RH qualifie explicitement de dysfonctionnement à corriger.")
        ]),
        new("Former les contrôleurs de gestion à l'analyse d'écarts budgétaires (méthode des écarts sur coûts)",
        [
            Doc("Note méthodologique contrôle de gestion",
                "L'écart total se décompose en écart sur volume, écart sur prix et écart sur rendement. " +
                "L'écart sur prix se calcule comme (prix réel - prix standard) × quantité réelle. L'outil " +
                "interne (module SAP CO-PA) génère automatiquement ces décompositions mensuelles par centre " +
                "de coût. Un écart supérieur à 5% du budget alloué déclenche une revue obligatoire en comité " +
                "de direction, avec plan d'action correctif documenté sous 15 jours.")
        ]),
        new("Former les chargés de marketing digital à l'optimisation des campagnes Google Ads",
        [
            Doc("Guide acquisition payante",
                "Le Quality Score dépend de trois facteurs : taux de clic attendu, pertinence de l'annonce, " +
                "expérience de la landing page. Un Quality Score sous 5/10 fait grimper le CPC de 20 à 40% " +
                "pour un même classement. La structure de compte recommandée est un ad group par intention de " +
                "recherche (pas par produit), avec des extensions d'annonce (sitelinks, callouts) activées " +
                "systématiquement, ce qui augmente le CTR mesuré en interne de 10 à 15%.")
        ]),
        new("Former les juristes d'entreprise à la mise en conformité RGPD des traitements de données clients",
        [
            Doc("Référentiel conformité RGPD",
                "Tout nouveau traitement de données personnelles nécessite une Analyse d'Impact (AIPD) dès " +
                "lors qu'il implique un profilage ou des données sensibles, selon la grille de la CNIL. Le " +
                "registre des traitements doit lister la base légale (consentement, intérêt légitime, " +
                "obligation contractuelle) pour chaque finalité. Délai légal de réponse à une demande de droit " +
                "d'accès : un mois, extensible à trois en cas de complexité, avec notification motivée au " +
                "demandeur.")
        ]),
        new("Former les managers à la conduite de réunions efficaces et à la prise de décision en équipe",
        [
            Doc("Charte réunions internes",
                "Toute réunion de plus de 30 minutes doit avoir un ordre du jour distribué 24h avant et un " +
                "propriétaire de décision clairement identifié (méthode RACI). Le format \"lecture silencieuse " +
                "de 5 minutes\" (inspiré d'Amazon) remplace les tours de table de présentation quand un " +
                "document a été préparé en amont. Un compte-rendu avec actions et échéances est publié dans " +
                "les 24h suivant la réunion, sans quoi la réunion est considérée comme non conclusive.")
        ]),
    ];
}
