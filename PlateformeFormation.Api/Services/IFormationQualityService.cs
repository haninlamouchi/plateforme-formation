using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Services;

public record QualityCheck(string Code, string Libelle, string Statut, string Detail); // Statut: OK | AVERTISSEMENT | ECHEC

// Niveau: EXCELLENT | BON | A_REVOIR. EstValide is the strict pass/fail gate (any ECHEC blocks) used
// by the generation retry loop — deliberately a view over the same Checks the nuanced 0-100 Score is
// computed from, not a second parallel validation pass, so the two can never disagree about the facts.
public record FormationQualityReport(int Score, string Niveau, List<QualityCheck> Checks)
{
    public bool EstValide => !Checks.Any(c => c.Statut == "ECHEC");
}

public interface IFormationQualityService
{
    FormationQualityReport Evaluate(Formation formation);
}
