// JS mirror of PlateformeFormation.Api/Helpers/FormationPlanner.cs (ComputeJours) — the server value
// (formation.planningJours) is always the authoritative one; this exists only so the timeline can show
// live day re-flow while dragging, before a save round-trips through the server. Keep this in sync
// with FormationPlanner.cs by hand — there is no shared source between the two runtimes.
const DEFAULT_MAX_HEURES_PAR_JOUR = 7;

export function computePlanningJours(modules, moduleBonus, maxHeuresParJour = DEFAULT_MAX_HEURES_PAR_JOUR) {
  if (!maxHeuresParJour || maxHeuresParJour <= 0) maxHeuresParJour = DEFAULT_MAX_HEURES_PAR_JOUR;

  const all = [...modules].sort((a, b) => (a.numero ?? 0) - (b.numero ?? 0));
  if (moduleBonus?.inclusDansTroncCommun && moduleBonus.dureeHeures != null) {
    const bonusNumero = (all.length > 0 ? Math.max(...all.map(m => m.numero ?? 0)) : 0) + 1;
    all.push({ numero: bonusNumero, titre: moduleBonus.titre, dureeHeures: moduleBonus.dureeHeures });
  }

  const jours = [];
  let current = [];
  let currentSuite = new Set();
  let sum = 0;
  let idx = 1;

  function flushDay() {
    if (current.length === 0) return;
    jours.push({
      jour: `Jour ${idx}`,
      moduleNumeros: [...current],
      dureeHeures: Math.round(sum * 10) / 10,
      modulesEnSuite: [...currentSuite],
    });
    idx++;
    current = [];
    currentSuite = new Set();
    sum = 0;
  }

  for (const m of all) {
    let remaining = m.dureeHeures ?? 0;
    let isFirstChunk = true;

    while (remaining > maxHeuresParJour) {
      if (current.length > 0) flushDay();
      current.push(m.numero);
      if (!isFirstChunk) currentSuite.add(m.numero);
      sum = maxHeuresParJour;
      remaining -= maxHeuresParJour;
      isFirstChunk = false;
      flushDay();
    }

    if (current.length > 0 && sum + remaining > maxHeuresParJour) flushDay();

    current.push(m.numero);
    if (!isFirstChunk) currentSuite.add(m.numero);
    sum += remaining;
  }

  flushDay();
  return jours;
}

// Renumbers modules 1..N by array order and remaps every competencesPrerequises/
// reutiliseLivrableModule reference through the old->new map. References to a module no longer in
// the list are dropped (it was deleted). References that become forward-pointing are KEPT — unlike
// the server's SanitizeCompetencesPrerequises, which strips them — so findForwardRefs can surface
// them to the user instead of silently losing pedagogical structure the user just arranged.
export function renumberModules(modules) {
  const oldToNew = new Map(modules.map((m, i) => [m.numero, i + 1]));

  return modules.map((m, i) => {
    const numero = i + 1;
    const competencesPrerequises = (m.competencesPrerequises || [])
      .map(n => oldToNew.get(n))
      .filter(n => n != null);
    const reutiliseLivrableModule = m.reutiliseLivrableModule != null
      ? (oldToNew.get(m.reutiliseLivrableModule) ?? null)
      : null;

    return { ...m, numero, competencesPrerequises, reutiliseLivrableModule };
  });
}

// Mirrors the forward-reference condition in FormationHarmonizer.SanitizeCompetencesPrerequises
// (reference >= numero), but reports instead of stripping.
export function findForwardRefs(modules) {
  const byNumero = new Map(modules.map(m => [m.numero, m]));
  const result = new Map();

  for (const m of modules) {
    const prereqIssues = (m.competencesPrerequises || []).filter(n => n >= m.numero && byNumero.has(n));
    const livrableIssue = m.reutiliseLivrableModule != null
      && m.reutiliseLivrableModule >= m.numero && byNumero.has(m.reutiliseLivrableModule);

    if (prereqIssues.length > 0 || livrableIssue) {
      result.set(m.numero, { competencesPrerequises: prereqIssues, reutiliseLivrableModule: livrableIssue ? m.reutiliseLivrableModule : null });
    }
  }

  return result;
}

// Mirrors FormationHarmonizer.HarmonizeDureeFromModules for the case relevant client-side (all
// modules have a duration) — used to keep dureeEstimee in sync after a manual duration edit, so the
// HEURES_MODULES quality check doesn't fail immediately after a save.
export function sumModuleHours(modules, moduleBonus) {
  let total = modules.reduce((acc, m) => acc + (m.dureeHeures ?? 0), 0);
  if (moduleBonus?.inclusDansTroncCommun && moduleBonus.dureeHeures != null) {
    total += moduleBonus.dureeHeures;
  }
  return Math.round(total * 10) / 10;
}
