import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  GripVertical, RotateCcw, AlertTriangle, Loader2, Check, Minus, Plus, Award, ArrowRight,
} from 'lucide-react';
import { computePlanningJours, renumberModules, findForwardRefs } from '../../utils/planning';
import { colorForNumero } from './cardColors';

const DURATION_STEP = 0.5;
const MIN_DURATION = 0.5;

const colorFor = colorForNumero;

function DurationStepper({ value, onChange }) {
  return (
    <div className="flex items-center gap-1 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-0)] px-1 py-0.5 shrink-0">
      <button type="button" onClick={() => onChange(Math.max(MIN_DURATION, (value ?? 0) - DURATION_STEP))}
        className="w-5 h-5 rounded flex items-center justify-center text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-2)]">
        <Minus className="w-3 h-3" />
      </button>
      <span className="text-[11.5px] font-semibold text-[var(--color-ink)] w-9 text-center tabular-nums">{value ?? 0}h</span>
      <button type="button" onClick={() => onChange((value ?? 0) + DURATION_STEP)}
        className="w-5 h-5 rounded flex items-center justify-center text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-2)]">
        <Plus className="w-3 h-3" />
      </button>
    </div>
  );
}

function RegenerationPanel({ avant, apres, onKeep, onReplace, lang }) {
  return (
    <div className="mt-2.5 rounded-xl border border-[var(--color-primary)]/30 bg-[var(--color-primary)]/5 p-3">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 text-[11.5px]">
        <div>
          <p className="font-bold uppercase tracking-wide text-[10px] text-[var(--color-ink-muted)] mb-1">
            {lang === 'fr' ? 'Actuel' : 'Current'}
          </p>
          <p className="font-semibold text-[var(--color-ink)]">{avant.titre}</p>
          <p className="text-[var(--color-ink-muted)] mt-0.5">{avant.objectif}</p>
        </div>
        <div>
          <p className="font-bold uppercase tracking-wide text-[10px] text-[var(--color-primary)] mb-1 flex items-center gap-1">
            <ArrowRight className="w-3 h-3" /> {lang === 'fr' ? 'Proposé' : 'Proposed'}
          </p>
          <p className="font-semibold text-[var(--color-ink)]">{apres.titre}</p>
          <p className="text-[var(--color-ink-muted)] mt-0.5">{apres.objectif}</p>
        </div>
      </div>
      <div className="flex items-center justify-end gap-2 mt-3">
        <button type="button" onClick={onKeep}
          className="px-3 py-1.5 rounded-lg text-[11.5px] font-semibold text-[var(--color-ink)] hover:bg-[var(--color-surface-1)] transition-colors">
          {lang === 'fr' ? 'Garder' : 'Keep'}
        </button>
        <button type="button" onClick={onReplace}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-[var(--color-primary)] text-white text-[11.5px] font-semibold hover:opacity-90 transition-opacity">
          <Check className="w-3.5 h-3.5" /> {lang === 'fr' ? 'Remplacer' : 'Replace'}
        </button>
      </div>
    </div>
  );
}

function ModuleCard({
  module, isDragging, isDragOver, forwardRef, regen, onDragStart, onDragOver, onDrop, onDragEnd,
  onDuree, onRegenerate, onKeepRegen, onReplaceRegen, lang,
}) {
  const color = colorFor(module.numero);
  const hasWarning = forwardRef && (forwardRef.competencesPrerequises.length > 0 || forwardRef.reutiliseLivrableModule != null);

  return (
    <motion.div
      layout
      draggable
      onDragStart={onDragStart}
      onDragOver={onDragOver}
      onDrop={onDrop}
      onDragEnd={onDragEnd}
      className={`flex flex-col gap-0 p-3 rounded-2xl border border-l-4 ${color.border} border-[var(--color-border)] bg-[var(--color-surface-1)] transition-shadow ${
        isDragging ? 'opacity-40' : ''
      } ${isDragOver ? 'ring-2 ring-[var(--color-primary)]/50' : ''}`}
    >
      <div className="flex items-center gap-2.5">
        <span className="cursor-grab active:cursor-grabbing text-[var(--color-ink-muted)] shrink-0">
          <GripVertical className="w-4 h-4" />
        </span>
        <div className={`w-7 h-7 rounded-full ${color.badge} text-white flex items-center justify-center font-bold text-[11.5px] shrink-0`}>
          {module.numero}
        </div>
        <div className="flex-1 min-w-0">
          <p className={`font-bold text-[13px] ${color.text} truncate`}>{module.titre || '—'}</p>
          {module.objectif && <p className="text-[11px] text-[var(--color-ink-muted)] truncate">{module.objectif}</p>}
        </div>
        <DurationStepper value={module.dureeHeures} onChange={onDuree} />
        <button type="button" onClick={onRegenerate} disabled={regen?.loading}
          title={lang === 'fr' ? 'Régénérer ce module' : 'Regenerate this module'}
          className="w-7 h-7 rounded-lg flex items-center justify-center text-[var(--color-ink-muted)] hover:bg-[var(--color-primary)]/10 hover:text-[var(--color-primary)] transition-colors disabled:opacity-40 shrink-0">
          {regen?.loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <RotateCcw className="w-3.5 h-3.5" />}
        </button>
      </div>

      {hasWarning && (
        <div className="flex items-start gap-1.5 mt-2 ml-9 px-2.5 py-1.5 rounded-lg bg-amber-500/10 text-[11px] text-amber-600 dark:text-amber-400">
          <AlertTriangle className="w-3.5 h-3.5 mt-0.5 shrink-0" />
          <span>
            {forwardRef.competencesPrerequises.length > 0 && (
              lang === 'fr'
                ? `Prérequis #${forwardRef.competencesPrerequises.join(', #')} vien${forwardRef.competencesPrerequises.length > 1 ? 'nent' : 't'} après ce module.`
                : `Prerequisite #${forwardRef.competencesPrerequises.join(', #')} come${forwardRef.competencesPrerequises.length > 1 ? '' : 's'} after this module.`
            )}
            {forwardRef.competencesPrerequises.length > 0 && forwardRef.reutiliseLivrableModule != null && ' '}
            {forwardRef.reutiliseLivrableModule != null && (
              lang === 'fr'
                ? `Livrable réutilisé (#${forwardRef.reutiliseLivrableModule}) vient après ce module.`
                : `Reused deliverable (#${forwardRef.reutiliseLivrableModule}) comes after this module.`
            )}
          </span>
        </div>
      )}

      {regen?.error && (
        <p className="mt-2 ml-9 text-[11px] text-red-500">
          {lang === 'fr' ? "Échec de la régénération. Réessayez." : 'Regeneration failed. Try again.'}
        </p>
      )}

      {regen?.apres && (
        <RegenerationPanel avant={regen.avant} apres={regen.apres} onKeep={onKeepRegen} onReplace={onReplaceRegen} lang={lang} />
      )}
    </motion.div>
  );
}

// Drag-reorder + duration + regenerate timeline over a formation's modules. Days are never an
// independent field — they're recomputed live (computePlanningJours) from the array order, exactly
// like the server does from Numero (FormationPlanner.ComputeJours). Dropping a card into a different
// day's visual group is really just moving it to a different position in that one ordered sequence.
export default function PlanningTimeline({ modules, moduleBonus, onChange, onRegenerate, lang }) {
  const [dragUid, setDragUid] = useState(null);
  const [overUid, setOverUid] = useState(null);
  const [regen, setRegen] = useState(null); // { numero, loading, avant, apres, error }

  const jours = computePlanningJours(modules, moduleBonus);
  const forwardRefs = findForwardRefs(modules);
  const byNumero = new Map(modules.map(m => [m.numero, m]));

  function reorder(fromUid, toUid) {
    const fromIdx = modules.findIndex(m => m._uid === fromUid);
    const toIdx = modules.findIndex(m => m._uid === toUid);
    if (fromIdx === -1 || toIdx === -1 || fromIdx === toIdx) return;
    const next = [...modules];
    const [moved] = next.splice(fromIdx, 1);
    next.splice(toIdx, 0, moved);
    onChange(renumberModules(next));
    if (regen) setRegen(null);
  }

  function updateDuree(uid, dureeHeures) {
    onChange(modules.map(m => (m._uid === uid ? { ...m, dureeHeures } : m)));
  }

  async function handleRegenerate(numero) {
    setRegen({ numero, loading: true });
    try {
      const result = await onRegenerate(numero);
      setRegen({ numero, loading: false, avant: result.avant, apres: result.apres });
    } catch {
      setRegen({ numero, loading: false, error: true });
    }
  }

  function keepRegen() { setRegen(null); }

  function replaceRegen() {
    const { numero, apres } = regen;
    onChange(modules.map(m => (m.numero === numero ? { ...apres, _uid: m._uid } : m)));
    setRegen(null);
  }

  return (
    <div className="flex flex-col gap-5">
      {jours.map((jour) => {
        const cardModules = jour.moduleNumeros.map(n => byNumero.get(n)).filter(Boolean);
        const atCap = jour.dureeHeures >= 7;
        const bonusInThisDay = moduleBonus?.inclusDansTroncCommun
          && jour.moduleNumeros.length > cardModules.length;

        return (
          <div key={jour.jour}>
            <div className="flex items-center gap-2.5 mb-2.5">
              <span className="text-[12.5px] font-bold text-[var(--color-ink)]">{jour.jour}</span>
              <div className="flex-1 h-px bg-[var(--color-border)]" />
              <span className={`text-[11px] font-semibold px-2 py-0.5 rounded-full ${atCap ? 'bg-amber-500/15 text-amber-600 dark:text-amber-400' : 'bg-[var(--color-surface-2)] text-[var(--color-ink-muted)]'}`}>
                {jour.dureeHeures}h {atCap && (lang === 'fr' ? '· plein' : '· full')}
              </span>
            </div>

            <div className="flex flex-col gap-2.5">
              <AnimatePresence initial={false}>
                {cardModules.map(m => (
                  <ModuleCard
                    key={m._uid}
                    module={m}
                    isDragging={dragUid === m._uid}
                    isDragOver={overUid === m._uid && dragUid !== m._uid}
                    forwardRef={forwardRefs.get(m.numero)}
                    regen={regen?.numero === m.numero ? regen : null}
                    onDragStart={() => setDragUid(m._uid)}
                    onDragOver={e => { e.preventDefault(); setOverUid(m._uid); }}
                    onDrop={e => { e.preventDefault(); reorder(dragUid, m._uid); setDragUid(null); setOverUid(null); }}
                    onDragEnd={() => { setDragUid(null); setOverUid(null); }}
                    onDuree={v => updateDuree(m._uid, v)}
                    onRegenerate={() => handleRegenerate(m.numero)}
                    onKeepRegen={keepRegen}
                    onReplaceRegen={replaceRegen}
                    lang={lang}
                  />
                ))}
              </AnimatePresence>
              {bonusInThisDay && (
                <div className="flex items-center gap-2 px-3.5 py-2 rounded-xl border border-dashed border-amber-500/40 text-[11.5px] text-amber-600 dark:text-amber-400">
                  <Award className="w-3.5 h-3.5 shrink-0" />
                  {lang === 'fr' ? 'Module bonus (tronc commun)' : 'Bonus module (core track)'} — {moduleBonus.titre}
                </div>
              )}
            </div>
          </div>
        );
      })}

      {/* Trailing drop zone so a card can be moved to the very end of the sequence. */}
      {modules.length > 0 && (
        <div
          onDragOver={e => { e.preventDefault(); setOverUid('__end__'); }}
          onDrop={e => {
            e.preventDefault();
            const fromIdx = modules.findIndex(m => m._uid === dragUid);
            if (fromIdx !== -1 && fromIdx !== modules.length - 1) {
              const next = [...modules];
              const [moved] = next.splice(fromIdx, 1);
              next.push(moved);
              onChange(renumberModules(next));
            }
            setDragUid(null);
            setOverUid(null);
          }}
          className={`h-6 rounded-xl border-2 border-dashed transition-colors ${overUid === '__end__' ? 'border-[var(--color-primary)]/50 bg-[var(--color-primary)]/5' : 'border-transparent'}`}
        />
      )}
    </div>
  );
}
