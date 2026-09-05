using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Helpers;

// ModulesEnSuite: numeros that appear on this day as a CONTINUATION of a module split across
// multiple days (R14) — the renderer appends "(suite)" for these so a module repeated across
// several days doesn't read as if it were taught identically on each one.
public record PlanningJour(string Jour, List<int> ModuleNumeros, double DureeHeures, HashSet<int> ModulesEnSuite);

// The schema forbids the model from generating a day-by-day plan at all (R13) — a real recurring
// bug was a day's announced duration silently disconnecting from what its modules actually summed
// to. Instead, the breakdown is computed here — deterministically, from real module durations, every
// time a formation is read — so it can never drift from the modules it describes. Never persisted.
public static class FormationPlanner
{
    private const double DefaultMaxHeuresParJour = 7;

    // R9: a module bonus that's part of the core track must appear in the computed planning, not
    // just count toward the total hours — folding it in here (as one extra pseudo-module placed
    // last) makes that true by construction rather than by a separate check that could drift.
    //
    // R14: no computed day may exceed maxHeuresParJour — including when a SINGLE module's own
    // duree_heures is bigger than a full day (a real observed bug: "Jour 4 — 33h"). Such a module is
    // split across as many consecutive days as it takes, guaranteed here by construction rather than
    // merely flagged afterward.
    public static List<PlanningJour> ComputeJours(
        List<ModuleCard> modules, ModuleBonus? moduleBonus = null, double maxHeuresParJour = DefaultMaxHeuresParJour)
    {
        if (maxHeuresParJour <= 0) maxHeuresParJour = DefaultMaxHeuresParJour;

        var all = modules.OrderBy(m => m.Numero).ToList();
        if (moduleBonus is { InclusDansTroncCommun: true, DureeHeures: not null })
        {
            var bonusNumero = (all.Count > 0 ? all.Max(m => m.Numero) : 0) + 1;
            all.Add(new ModuleCard(
                bonusNumero, moduleBonus.Titre, moduleBonus.DureeHeures, null, null, null, null, null, null, null, null, null, null));
        }

        var jours = new List<PlanningJour>();
        var current = new List<int>();
        var currentSuite = new HashSet<int>();
        double sum = 0;
        var idx = 1;

        void FlushDay()
        {
            if (current.Count == 0) return;
            jours.Add(new PlanningJour($"Jour {idx}", [.. current], Math.Round(sum, 1), [.. currentSuite]));
            idx++;
            current = [];
            currentSuite = [];
            sum = 0;
        }

        foreach (var m in all)
        {
            var remaining = m.DureeHeures ?? 0;
            var isFirstChunk = true;

            while (remaining > maxHeuresParJour)
            {
                if (current.Count > 0) FlushDay(); // an oversized module always starts its own fresh day
                current.Add(m.Numero);
                if (!isFirstChunk) currentSuite.Add(m.Numero);
                sum = maxHeuresParJour;
                remaining -= maxHeuresParJour;
                isFirstChunk = false;
                FlushDay();
            }

            if (current.Count > 0 && sum + remaining > maxHeuresParJour) FlushDay();

            current.Add(m.Numero);
            if (!isFirstChunk) currentSuite.Add(m.Numero);
            sum += remaining;
        }

        FlushDay();

        return jours;
    }
}
