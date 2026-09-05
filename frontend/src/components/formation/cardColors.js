// Shared module-card palette — used by FormationDetail's preview/editors and by PlanningTimeline so
// a given module numero renders in the same color everywhere in the formation UI.
export const CARD_COLORS = [
  { badge: 'bg-red-600', text: 'text-red-600 dark:text-red-400', border: 'border-l-red-500', chipBg: 'bg-red-500/12' },
  { badge: 'bg-blue-600', text: 'text-blue-600 dark:text-blue-400', border: 'border-l-blue-500', chipBg: 'bg-blue-500/12' },
  { badge: 'bg-purple-600', text: 'text-purple-600 dark:text-purple-400', border: 'border-l-purple-500', chipBg: 'bg-purple-500/12' },
  { badge: 'bg-emerald-600', text: 'text-emerald-600 dark:text-emerald-400', border: 'border-l-emerald-500', chipBg: 'bg-emerald-500/12' },
  { badge: 'bg-amber-600', text: 'text-amber-600 dark:text-amber-400', border: 'border-l-amber-500', chipBg: 'bg-amber-500/12' },
  { badge: 'bg-pink-600', text: 'text-pink-600 dark:text-pink-400', border: 'border-l-pink-500', chipBg: 'bg-pink-500/12' },
  { badge: 'bg-cyan-600', text: 'text-cyan-600 dark:text-cyan-400', border: 'border-l-cyan-500', chipBg: 'bg-cyan-500/12' },
];

// A module's `numero` normally comes from AI-generated or hand-edited JSON — it can be missing,
// non-numeric, or out of the 1..CARD_COLORS.length range. `CARD_COLORS[(numero - 1 + N) % N]`
// silently returns `undefined` for a non-finite numero (NaN propagates straight through the index
// math), and every call site immediately reads `.text`/`.badge`/etc. off that, crashing the page.
// Centralizing the lookup here means every caller gets the same "fall back to the first color"
// behavior instead of duplicating (and inconsistently forgetting) the guard.
export function colorForNumero(numero) {
  const n = Number(numero);
  const index = Number.isFinite(n) ? ((n - 1) % CARD_COLORS.length + CARD_COLORS.length) % CARD_COLORS.length : 0;
  return CARD_COLORS[index];
}
