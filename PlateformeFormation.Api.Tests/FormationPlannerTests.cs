using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;

namespace PlateformeFormation.Api.Tests;

public class FormationPlannerTests
{
    // The schema forbids the model from generating a day-by-day plan at all — this is the
    // deterministic replacement, computed purely from real module durations.
    [Fact]
    public void ComputeJours_PacksModulesWithinDailyLimit()
    {
        var modules = new List<ModuleCard>
        {
            new(1, "M1", 3, null, null, null, null, null, null, null, null, null, null),
            new(2, "M2", 3, null, null, null, null, null, null, null, null, null, null),
            new(3, "M3", 3, null, null, null, null, null, null, null, null, null, null),
        };

        var jours = FormationPlanner.ComputeJours(modules, maxHeuresParJour: 7);

        Assert.Equal(2, jours.Count);
        Assert.Equal([1, 2], jours[0].ModuleNumeros);
        Assert.Equal(6, jours[0].DureeHeures);
        Assert.Equal([3], jours[1].ModuleNumeros);
        Assert.Equal(3, jours[1].DureeHeures);
    }

    // Exact reported bug (R9): a module bonus counted in the total hours but never shown in the plan.
    // Folding it into ComputeJours as a trailing pseudo-module makes that structurally impossible —
    // if it has hours and is part of the core track, it WILL appear.
    [Fact]
    public void ComputeJours_IncludesCoreTrackModuleBonus()
    {
        var modules = new List<ModuleCard> { new(1, "M1", 3, null, null, null, null, null, null, null, null, null, null) };
        var bonus = new ModuleBonus(InclusDansTroncCommun: true, Titre: "Bonus", DureeHeures: 2, Contenu: null);

        var jours = FormationPlanner.ComputeJours(modules, bonus, maxHeuresParJour: 7);

        var allNumeros = jours.SelectMany(j => j.ModuleNumeros).ToList();
        Assert.Contains(2, allNumeros); // bonus assigned the next free numero
        Assert.Equal(5, jours.Sum(j => j.DureeHeures));
    }

    [Fact]
    public void ComputeJours_ExcludesOptionalModuleBonus()
    {
        var modules = new List<ModuleCard> { new(1, "M1", 3, null, null, null, null, null, null, null, null, null, null) };
        var bonus = new ModuleBonus(InclusDansTroncCommun: false, Titre: "Bonus", DureeHeures: 2, Contenu: null);

        var jours = FormationPlanner.ComputeJours(modules, bonus, maxHeuresParJour: 7);

        Assert.Equal(3, jours.Sum(j => j.DureeHeures));
    }

    // R14: real observed bug — a module whose own duree_heures exceeds a full day produced a single
    // planning entry like "Jour 4 — 33h". No computed day may exceed maxHeuresParJour; an oversized
    // module is split across as many consecutive days as it takes, guaranteed by construction.
    [Fact]
    public void ComputeJours_SplitsModuleLongerThanADay()
    {
        var modules = new List<ModuleCard> { new(1, "M1", 24, null, null, null, null, null, null, null, null, null, null) };

        var jours = FormationPlanner.ComputeJours(modules, maxHeuresParJour: 7);

        Assert.Equal(4, jours.Count);
        Assert.All(jours, j => Assert.True(j.DureeHeures <= 7));
        Assert.Equal([7, 7, 7, 3], jours.Select(j => j.DureeHeures));
        Assert.All(jours, j => Assert.Equal([1], j.ModuleNumeros));
        Assert.Empty(jours[0].ModulesEnSuite); // first day is the module's actual start, not a continuation
        Assert.Contains(1, jours[1].ModulesEnSuite);
        Assert.Contains(1, jours[2].ModulesEnSuite);
        Assert.Contains(1, jours[3].ModulesEnSuite);
    }

    [Fact]
    public void ComputeJours_OversizedModuleStartsFreshDay_NeverMergedWithPriorModule()
    {
        var modules = new List<ModuleCard>
        {
            new(1, "M1", 2, null, null, null, null, null, null, null, null, null, null),
            new(2, "M2", 10, null, null, null, null, null, null, null, null, null, null),
        };

        var jours = FormationPlanner.ComputeJours(modules, maxHeuresParJour: 7);

        Assert.All(jours, j => Assert.True(j.DureeHeures <= 7));
        Assert.Equal([1], jours[0].ModuleNumeros); // module 1 alone on day 1
        Assert.DoesNotContain(2, jours[0].ModuleNumeros); // module 2 never packed alongside it
    }
}
