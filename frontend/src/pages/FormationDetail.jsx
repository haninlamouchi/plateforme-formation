import { useState, useEffect, useCallback, useRef } from 'react';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { useParams, useNavigate } from 'react-router-dom';
import {
  ArrowLeft, ArrowRight, Clock, FileText, Check, Loader2, Trash2, ShieldCheck, RotateCcw, AlertTriangle, Download,
  Plus, X, Pencil, Eye, Users, BookOpenCheck, CheckCircle2, Target, XCircle, AlertCircle, Wrench, Layers, Award,
  CalendarRange, ChevronDown, Presentation,
} from 'lucide-react';
import {
  getFormationById, getFormationQualite, attachFormationTraces, previewFormationCorrection, updateFormation,
  updateFormationStatut, deleteFormation, exportFormationPdf, exportFormationPptx, regenerateFormationModule,
} from '../services/formationService';
import { useLanguage } from '../context/LanguageContext';
import { CARD_COLORS, colorForNumero } from '../components/formation/cardColors';
import PlanningTimeline from '../components/formation/PlanningTimeline';
import { sumModuleHours, renumberModules } from '../utils/planning';

// Objectifs/Modules/Évaluation are stored as JSON — object / card arrays — not prose. Old free-text
// or old-schema data still opens without crashing (unknown fields are simply absent, not fatal).
function parseObjectifs(json) {
  if (!json) return {};
  try {
    const parsed = JSON.parse(json);
    return typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

function parseCards(json) {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

// Client-only identity for drag-reorder animation — modules are otherwise identified by `numero`,
// which the planning timeline deliberately reassigns on every reorder (see utils/planning.js).
function makeUid() {
  return `m-${Math.random().toString(36).slice(2, 10)}`;
}

function stripUid(m) {
  const rest = { ...m };
  delete rest._uid;
  return rest;
}

// Field-level diff between an "avant" and "apres" FormationContentDto, used to highlight exactly
// what a correction changed instead of leaving the user to compare two full renders by eye.
function diffFormationContent(avant, apres) {
  const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);
  const oAvant = parseObjectifs(avant.objectifs);
  const oApres = parseObjectifs(apres.objectifs);

  const objectifs = {};
  for (const field of ['sourcesUtilisees', 'lacunesContexte', 'testPositionnement', 'moduleBonus', 'ressourcesPedagogiques']) {
    objectifs[field] = !eq(oAvant[field], oApres[field]);
  }

  const diffIndices = (before, after) => {
    const changed = new Set();
    for (let i = 0; i < Math.max(before.length, after.length); i++) {
      if (!eq(before[i], after[i])) changed.add(i);
    }
    return changed;
  };

  return {
    objectifs,
    modules: diffIndices(parseCards(avant.modules), parseCards(apres.modules)),
    evaluation: diffIndices(parseCards(avant.methodesEvaluation), parseCards(apres.methodesEvaluation)),
  };
}

function Field({ label, children }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-[12.5px] font-semibold text-[var(--color-ink)]">{label}</label>
      {children}
    </div>
  );
}

const inputClass = "px-2.5 py-1.5 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-0)] text-[var(--color-ink)] placeholder:text-[var(--color-ink-muted)] focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/40";

// ---------- Preview (read-only) ----------

function InfoPill({ icon: Icon, label }) {
  return (
    <span className="flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[12px] font-medium bg-[var(--color-surface-1)] border border-[var(--color-border)] text-[var(--color-ink)]">
      <Icon className="w-3.5 h-3.5 text-[var(--color-primary)]" />
      {label}
    </span>
  );
}

function SectionTitle({ children }) {
  return <h2 className="text-[12.5px] font-bold text-[var(--color-primary)] uppercase tracking-wide mb-3">{children}</h2>;
}

// Amber ring + badge used throughout the correction-preview "Après" column to show exactly what
// changed, instead of asking the user to spot the difference between two full renders by eye.
function ChangedBadge({ lang }) {
  return (
    <span className="absolute -top-2 -right-2 px-1.5 py-0.5 rounded-full text-[9px] font-bold bg-amber-500 text-white shadow z-10">
      {lang === 'fr' ? 'Modifié' : 'Changed'}
    </span>
  );
}
const CHANGED_RING = 'relative ring-2 ring-amber-400/70 bg-amber-500/5';

function TransparenceCard({ objectifs, lang, diff }) {
  const sources = objectifs.sourcesUtilisees;
  const lacunes = objectifs.lacunesContexte;
  if (!sources?.length && !lacunes?.length) return null;

  return (
    <div>
      <SectionTitle>{lang === 'fr' ? 'Transparence documentaire' : 'Documentary transparency'}</SectionTitle>
      <div className="flex flex-col gap-2.5">
        {sources?.length > 0 && (
          <div className={diff?.sourcesUtilisees ? `${CHANGED_RING} rounded-xl p-2.5 -m-1` : 'p-2.5 -m-1'}>
            {diff?.sourcesUtilisees && <ChangedBadge lang={lang} />}
            <p className="text-[11px] font-bold uppercase tracking-wide text-[var(--color-ink-muted)] mb-1.5">
              {lang === 'fr' ? 'Sources utilisées' : 'Sources used'}
            </p>
            <div className="flex flex-wrap gap-1.5">
              {sources.map((s, i) => (
                <span key={i} className="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11.5px] font-medium bg-[var(--color-surface-2)] text-[var(--color-ink)]">
                  <FileText className="w-3 h-3 text-[var(--color-ink-muted)]" />{s}
                </span>
              ))}
            </div>
          </div>
        )}
        {lacunes?.length > 0 && (
          <div className={diff?.lacunesContexte ? `${CHANGED_RING} rounded-xl p-2.5 -m-1` : 'p-2.5 -m-1'}>
            {diff?.lacunesContexte && <ChangedBadge lang={lang} />}
            <p className="text-[11px] font-bold uppercase tracking-wide text-[var(--color-ink-muted)] mb-1.5">
              {lang === 'fr' ? 'Lacunes du contexte' : 'Context gaps'}
            </p>
            <div className="flex flex-col gap-1">
              {lacunes.map((l, i) => (
                <div key={i} className="flex items-start gap-2 p-2 rounded-lg bg-amber-500/8">
                  <AlertTriangle className="w-3.5 h-3.5 text-amber-500 mt-0.5 shrink-0" />
                  <span className="text-[12px] text-[var(--color-ink)]">{l}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function PlanningTable({ jours, modules, lang }) {
  if (!jours?.length) return null;
  const titreParNumero = Object.fromEntries(modules.map(m => [m.numero, m.titre]));
  return (
    <div>
      <SectionTitle>{lang === 'fr' ? 'Planning' : 'Schedule'}</SectionTitle>
      <div className="rounded-xl border border-[var(--color-border)] overflow-hidden divide-y divide-[var(--color-border)]">
        {jours.map((j, i) => (
          <div key={i} className="flex items-start gap-3 p-3 bg-[var(--color-surface-1)]">
            <span className="text-[12px] font-bold text-[var(--color-primary)] w-16 shrink-0">{j.jour}</span>
            <span className="flex-1 text-[12.5px] text-[var(--color-ink)]">
              {(j.moduleNumeros || [])
                .map(n => {
                  const titre = titreParNumero[n] || `Module ${n}`;
                  return j.modulesEnSuite?.includes(n) ? `${titre} (suite)` : titre;
                })
                .join(', ')}
            </span>
            <span className="text-[11px] text-[var(--color-ink-muted)] shrink-0">{j.dureeHeures}h</span>
          </div>
        ))}
      </div>
      <p className="text-[10.5px] text-[var(--color-ink-muted)] mt-1.5 italic">
        {lang === 'fr'
          ? "Calculé automatiquement à partir des modules — jamais généré par l'IA."
          : 'Automatically computed from the modules — never generated by the AI.'}
      </p>
    </div>
  );
}

function TestPositionnementCard({ test, modules, lang, changed }) {
  if (!test) return null;
  const hasAny = test.objectif || test.exercice || test.qcmQuestions != null || test.seuilParcoursStandardPct != null || test.moduleRemediation != null;
  if (!hasAny) return null;
  const remediationTitre = modules.find(m => m.numero === test.moduleRemediation)?.titre;

  return (
    <div className={changed ? `${CHANGED_RING} rounded-xl p-2 -m-2` : undefined}>
      {changed && <ChangedBadge lang={lang} />}
      <SectionTitle>{lang === 'fr' ? 'Test de positionnement' : 'Placement test'}</SectionTitle>
      <div className="rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-1)] p-3.5 flex flex-col gap-2">
        {test.objectif && (
          <p className="text-[12.5px] text-[var(--color-ink)]"><span className="font-semibold">{lang === 'fr' ? 'Objectif' : 'Objective'} :</span> {test.objectif}</p>
        )}
        {test.qcmQuestions != null && (
          <p className="text-[12.5px] text-[var(--color-ink)]"><span className="font-semibold">QCM :</span> {test.qcmQuestions} {lang === 'fr' ? 'questions' : 'questions'}</p>
        )}
        {test.exercice && (
          <p className="text-[12.5px] text-[var(--color-ink)]"><span className="font-semibold">{lang === 'fr' ? 'Exercice' : 'Exercise'} :</span> {test.exercice}</p>
        )}
        {test.seuilParcoursStandardPct != null && (
          <p className="text-[12.5px] text-[var(--color-ink)]"><span className="font-semibold">{lang === 'fr' ? 'Seuil parcours standard' : 'Standard-track threshold'} :</span> {test.seuilParcoursStandardPct}%</p>
        )}
        {test.moduleRemediation != null && (
          <p className="text-[12.5px] text-[var(--color-ink)]">
            <span className="font-semibold">{lang === 'fr' ? 'Module de remédiation' : 'Remediation module'} :</span> #{test.moduleRemediation}{remediationTitre ? ` — ${remediationTitre}` : ''}
          </p>
        )}
      </div>
    </div>
  );
}

function ModuleBonusCard({ bonus, lang, changed }) {
  if (!bonus?.titre) return null;
  return (
    <div className={changed ? `${CHANGED_RING} rounded-xl p-2 -m-2` : undefined}>
      {changed && <ChangedBadge lang={lang} />}
      <SectionTitle>{bonus.inclusDansTroncCommun
        ? (lang === 'fr' ? 'Module bonus (inclus au tronc commun)' : 'Bonus module (part of the core track)')
        : (lang === 'fr' ? 'Module bonus (optionnel)' : 'Bonus module (optional)')}
      </SectionTitle>
      <div className="rounded-xl border border-l-4 border-l-amber-500 border-[var(--color-border)] bg-[var(--color-surface-1)] p-3.5 flex items-start gap-3">
        <Award className="w-5 h-5 text-amber-500 mt-0.5 shrink-0" />
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="font-bold text-[13.5px] text-[var(--color-ink)]">{bonus.titre}</span>
            {bonus.dureeHeures != null && <span className="text-[11px] text-[var(--color-ink-muted)]">{bonus.dureeHeures}h</span>}
          </div>
          {bonus.contenu?.length > 0 && (
            <div className="flex flex-wrap gap-1.5 mt-1.5">
              {bonus.contenu.map((c, i) => (
                <span key={i} className="px-2 py-0.5 rounded-full text-[11px] font-medium bg-amber-500/12 text-amber-600 dark:text-amber-400">{c}</span>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// `objectifs.ressourcesPedagogiques` is modeled as string[] everywhere (backend DTO, PDF/PPTX
// export, quality checks), but `Formation.Objectifs` is opaque JSON text with no schema
// enforcement in the DB — a formation generated or hand-edited at an earlier point can carry an
// unexpected shape (observed: {type, nom} objects instead of plain strings). Rendering an object
// directly as a React child crashes the whole page, so every entry is normalized to a display
// string before rendering rather than trusting the stored shape.
function resourceLabel(r) {
  if (typeof r === 'string') return r;
  if (r && typeof r === 'object') return [r.nom, r.type].filter(Boolean).join(' — ') || JSON.stringify(r);
  return String(r);
}

function RessourcesPedagogiquesList({ ressources, lang, changed }) {
  if (!ressources?.length) return null;
  return (
    <div className={changed ? `${CHANGED_RING} rounded-xl p-2 -m-2` : undefined}>
      {changed && <ChangedBadge lang={lang} />}
      <SectionTitle>{lang === 'fr' ? 'Ressources pédagogiques' : 'Learning resources'}</SectionTitle>
      <div className="flex flex-wrap gap-1.5">
        {ressources.map((r, i) => (
          <span key={i} className="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11.5px] font-medium bg-[var(--color-surface-2)] text-[var(--color-ink)]">{resourceLabel(r)}</span>
        ))}
      </div>
    </div>
  );
}

const NIVEAU_COLOR = { EXCELLENT: '#059669', BON: '#D97706', A_REVOIR: '#DC2626' };
const NIVEAU_LABEL = {
  EXCELLENT: { fr: 'Excellent', en: 'Excellent' },
  BON: { fr: 'Bon', en: 'Good' },
  A_REVOIR: { fr: 'À revoir', en: 'Needs review' },
};

function ScoreRing({ score, color }) {
  return (
    <div
      className="relative w-16 h-16 rounded-full shrink-0"
      style={{ background: `conic-gradient(${color} ${score * 3.6}deg, var(--color-surface-2) 0deg)` }}
    >
      <div className="absolute inset-1 rounded-full bg-[var(--color-surface-1)] flex items-center justify-center">
        <span className="text-[15px] font-extrabold text-[var(--color-ink)]">{score}</span>
      </div>
    </div>
  );
}

// Verified, not generated: every entry here is a check computed in C# against the formation's
// actual stored data — not something the LLM claims about itself. See FormationQualityService.cs.
function QualityPanel({ report, lang, onRetry, loading, onCorrect, correcting }) {
  if (loading) {
    return (
      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] p-5 flex items-center justify-center py-8">
        <Loader2 className="w-4.5 h-4.5 animate-spin text-[var(--color-ink-muted)]" />
      </div>
    );
  }
  if (!report) {
    return (
      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] p-5 flex items-center justify-between gap-3">
        <p className="text-[12.5px] text-[var(--color-ink-muted)]">
          {lang === 'fr' ? "Contrôle qualité indisponible pour l'instant." : 'Quality check unavailable right now.'}
        </p>
        <button onClick={onRetry} className="text-[12px] font-semibold text-[var(--color-primary)] hover:underline shrink-0">
          {lang === 'fr' ? 'Réessayer' : 'Retry'}
        </button>
      </div>
    );
  }

  const color = NIVEAU_COLOR[report.niveau] ?? NIVEAU_COLOR.A_REVOIR;
  const label = NIVEAU_LABEL[report.niveau]?.[lang] ?? report.niveau;

  return (
    <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] p-5">
      <div className="flex items-start justify-between gap-3 mb-4">
        <div className="flex items-center gap-4">
          <ScoreRing score={report.score} color={color} />
          <div>
            <h2 className="text-[14px] font-bold text-[var(--color-ink)]">
              {lang === 'fr' ? 'Qualité pédagogique' : 'Pedagogical quality'}
            </h2>
            <p className="text-[12.5px] font-semibold" style={{ color }}>{label}</p>
            <p className="text-[11px] text-[var(--color-ink-muted)]">
              {lang === 'fr'
                ? 'Contrôles vérifiés automatiquement, recalculés à chaque modification.'
                : 'Automatically verified checks, recomputed on every change.'}
            </p>
          </div>
        </div>
        {report.niveau !== 'EXCELLENT' && (
          <button
            onClick={onCorrect}
            disabled={correcting}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] font-semibold bg-[var(--color-primary)] text-white hover:opacity-90 transition-opacity disabled:opacity-40 shrink-0"
          >
            {correcting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Wrench className="w-3.5 h-3.5" />}
            {correcting
              ? (lang === 'fr' ? 'Analyse...' : 'Analyzing...')
              : (lang === 'fr' ? 'Corriger' : 'Fix')}
          </button>
        )}
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
        {report.checks.map((c, i) => {
          const Icon = c.statut === 'OK' ? CheckCircle2 : c.statut === 'AVERTISSEMENT' ? AlertCircle : XCircle;
          const iconColor = c.statut === 'OK' ? 'text-emerald-500' : c.statut === 'AVERTISSEMENT' ? 'text-amber-500' : 'text-red-500';
          return (
            <div key={i} className="flex items-start gap-2 p-2 rounded-lg">
              <Icon className={`w-4 h-4 mt-0.5 shrink-0 ${iconColor}`} />
              <div className="min-w-0">
                <p className="text-[12px] font-medium text-[var(--color-ink)]">{c.libelle}</p>
                <p className="text-[11px] text-[var(--color-ink-muted)]">{c.detail}</p>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function FormationPreview({ formation, lang, onOpenSource, onCatchUpTraces, catchingUp, diff }) {
  const objectifs = parseObjectifs(formation.objectifs);
  const modules = parseCards(formation.modules).sort((a, b) => (a.numero ?? 0) - (b.numero ?? 0));
  const evaluation = parseCards(formation.methodesEvaluation);
  const anySourced = modules.some(m => m.sources?.length > 0);

  return (
    <div className="flex flex-col gap-8">
      <div className="rounded-2xl border border-[var(--color-border)] overflow-hidden">
        <div className="p-6 bg-gradient-to-br from-[var(--color-primary)]/12 via-[var(--color-surface-1)] to-[var(--color-surface-1)]">
          <h1 className="text-[24px] font-extrabold text-[var(--color-ink)] leading-tight">{formation.titre}</h1>
          <div className="flex flex-wrap gap-2 mt-4">
            {formation.dureeEstimee != null && <InfoPill icon={Clock} label={`${formation.dureeEstimee}h`} />}
            {objectifs.publicCible && <InfoPill icon={Users} label={objectifs.publicCible} />}
            {objectifs.prerequisDetailles && <InfoPill icon={BookOpenCheck} label={objectifs.prerequisDetailles} />}
          </div>
        </div>
      </div>

      <TransparenceCard objectifs={objectifs} lang={lang} diff={diff?.objectifs} />

      {modules.length > 0 && (
        <div>
          <SectionTitle>{lang === 'fr' ? "Vue d'ensemble du programme" : 'Program overview'}</SectionTitle>
          <div className="rounded-xl border border-[var(--color-border)] overflow-hidden divide-y divide-[var(--color-border)] mb-5">
            {modules.map((m, i) => {
              const color = colorForNumero(m.numero);
              return (
                <div key={i} className="flex items-center gap-3 px-3.5 py-2 bg-[var(--color-surface-1)]">
                  <span className={`text-[11.5px] font-bold ${color.text} w-6 shrink-0`}>{m.numero}</span>
                  <span className="flex-1 text-[12.5px] text-[var(--color-ink)] font-medium truncate">{m.titre}</span>
                  {m.dureeHeures != null && <span className="text-[11px] text-[var(--color-ink-muted)] shrink-0">{m.dureeHeures}h</span>}
                </div>
              );
            })}
          </div>

          <PlanningTable jours={formation.planningJours} modules={modules} lang={lang} />

          <SectionTitle>{lang === 'fr' ? 'Programme détaillé' : 'Detailed program'}</SectionTitle>
          <div className="flex flex-col gap-3 mt-3">
            {modules.map((m, i) => {
              const color = colorForNumero(m.numero);
              const changed = diff?.modules?.has(i);
              return (
                <div key={i} className={`flex gap-3 p-4 rounded-2xl border border-l-4 ${color.border} border-[var(--color-border)] bg-[var(--color-surface-1)] ${changed ? CHANGED_RING : ''}`}>
                  {changed && <ChangedBadge lang={lang} />}
                  <div className={`w-9 h-9 rounded-full ${color.badge} text-white flex items-center justify-center font-bold text-[13px] shrink-0`}>
                    {m.numero}
                  </div>
                  <div className="flex-1 min-w-0 flex flex-col gap-1.5">
                    <div className="flex items-center justify-between gap-2 flex-wrap">
                      <h3 className={`font-bold text-[14.5px] ${color.text}`}>{m.titre}</h3>
                      {m.dureeHeures != null && <span className="text-[11px] font-medium text-[var(--color-ink-muted)]">{m.dureeHeures}h</span>}
                    </div>
                    {m.objectif && (
                      <p className="text-[12.5px] text-[var(--color-ink)] flex items-start gap-1.5">
                        <Target className="w-3.5 h-3.5 mt-0.5 shrink-0 text-[var(--color-ink-muted)]" />
                        {m.objectif}
                      </p>
                    )}
                    {m.contenu?.length > 0 && (
                      <div className="flex flex-wrap gap-1.5 mt-0.5">
                        {m.contenu.map((c, ci) => (
                          <span key={ci} className={`px-2 py-0.5 rounded-full text-[11px] font-medium ${color.chipBg} ${color.text}`}>{c}</span>
                        ))}
                      </div>
                    )}
                    {m.livrable && (
                      <p className="text-[11.5px] text-[var(--color-ink-muted)]">
                        <span className="font-semibold text-[var(--color-ink)]">{lang === 'fr' ? 'Livrable' : 'Deliverable'} :</span> {m.livrable}
                        {m.reutiliseLivrableModule != null && (
                          <span className="ml-1.5 text-[10.5px] font-medium">
                            ({lang === 'fr' ? 'poursuit le livrable du module' : 'continues the deliverable of module'} #{m.reutiliseLivrableModule})
                          </span>
                        )}
                      </p>
                    )}
                    {(m.methode?.type || m.methode?.pctTheorie != null) && (
                      <p className="text-[11px] text-[var(--color-ink-muted)]">
                        {[m.methode?.type, (m.methode?.pctTheorie != null && m.methode?.pctPratique != null) ? `${m.methode.pctTheorie}% théorie / ${m.methode.pctPratique}% pratique` : null]
                          .filter(Boolean).join('  ·  ')}
                      </p>
                    )}
                    {m.exerciceFormatif?.consigne && (
                      <p className="text-[11.5px] text-[var(--color-ink)]">
                        {m.exerciceFormatif.type && <span className="font-medium">{m.exerciceFormatif.type} : </span>}
                        {m.exerciceFormatif.consigne}
                        {m.exerciceFormatif.dureeMin != null && <span className="text-[var(--color-ink-muted)]"> ({m.exerciceFormatif.dureeMin} min)</span>}
                      </p>
                    )}
                    {m.competencesPrerequises?.length > 0 && (
                      <div className="flex items-center gap-1.5 flex-wrap mt-0.5">
                        <Layers className="w-3 h-3 text-[var(--color-ink-muted)]" />
                        {m.competencesPrerequises.map((n, ni) => (
                          <span key={ni} className="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-[var(--color-surface-2)] text-[var(--color-ink-muted)]">#{n}</span>
                        ))}
                      </div>
                    )}
                    {m.sources?.length > 0 && (
                      <button
                        type="button"
                        onClick={() => onOpenSource?.(m)}
                        className={`self-start flex items-center gap-1.5 mt-1 px-2.5 py-1 rounded-full text-[11px] font-medium ${color.chipBg} ${color.text} hover:opacity-80 transition-opacity`}
                      >
                        <FileText className="w-3 h-3" />
                        {lang === 'fr' ? 'Source' : 'Source'} : {m.sources[0].documentTitre}
                        {m.sources.length > 1 && ` +${m.sources.length - 1}`}
                      </button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
          {!anySourced && (
            <div className="flex items-center justify-between gap-3 mt-3 px-3.5 py-2.5 rounded-xl bg-[var(--color-surface-2)]">
              <p className="text-[11.5px] text-[var(--color-ink-muted)]">
                {lang === 'fr'
                  ? "Aucune source rattachée aux modules pour l'instant."
                  : 'No sources attached to the modules yet.'}
              </p>
              <button
                type="button"
                onClick={onCatchUpTraces}
                disabled={catchingUp}
                className="flex items-center gap-1.5 text-[11.5px] font-semibold text-[var(--color-primary)] hover:underline disabled:opacity-40 shrink-0"
              >
                {catchingUp && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
                {lang === 'fr' ? 'Retrouver les sources' : 'Find sources'}
              </button>
            </div>
          )}
        </div>
      )}

      <ModuleBonusCard bonus={objectifs.moduleBonus} lang={lang} changed={diff?.objectifs?.moduleBonus} />
      <TestPositionnementCard test={objectifs.testPositionnement} modules={modules} lang={lang} changed={diff?.objectifs?.testPositionnement} />

      {evaluation.length > 0 && (
        <div>
          <SectionTitle>{lang === 'fr' ? "Modalités d'évaluation" : 'Evaluation'}</SectionTitle>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
            {evaluation.map((e, i) => {
              const color = CARD_COLORS[i % CARD_COLORS.length];
              const changed = diff?.evaluation?.has(i);
              return (
                <div key={i} className={`flex items-start gap-2.5 p-3.5 rounded-xl border border-l-4 ${color.border} border-[var(--color-border)] bg-[var(--color-surface-1)] ${changed ? CHANGED_RING : ''}`}>
                  {changed && <ChangedBadge lang={lang} />}
                  <div className={`w-6 h-6 rounded-full ${color.badge} text-white flex items-center justify-center font-bold text-[11px] shrink-0 mt-0.5`}>{i + 1}</div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-1.5 flex-wrap">
                      <span className={`font-bold text-[13px] ${color.text}`}>{e.nom}</span>
                      {e.pct != null && (
                        <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded-full bg-[var(--color-surface-2)] text-[var(--color-ink-muted)]">{e.pct}%</span>
                      )}
                      {e.estEvaluationContinue && (
                        <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded-full bg-emerald-500/12 text-emerald-600 dark:text-emerald-400">
                          {lang === 'fr' ? 'Continue' : 'Continuous'}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      <RessourcesPedagogiquesList ressources={objectifs.ressourcesPedagogiques} lang={lang} changed={diff?.objectifs?.ressourcesPedagogiques} />
    </div>
  );
}

// Shows the actual document passage(s) a module was matched against — makes the RAG mechanism
// auditable instead of trusting the module's content on faith.
function SourceModal({ module, onClose, lang }) {
  return createPortal(
    <motion.div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/40 px-4"
      initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
      onClick={onClose}>
      <motion.div className="w-full max-w-lg max-h-[80vh] flex flex-col rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] shadow-[0_24px_60px_rgba(0,0,0,0.3)] overflow-hidden"
        initial={{ opacity: 0, scale: 0.95, y: 16 }} animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.95, y: 16 }}
        transition={{ type: 'spring', stiffness: 340, damping: 30 }}
        onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between gap-2 px-5 py-4 border-b border-[var(--color-border)] bg-[var(--color-surface-2)]">
          <div>
            <h3 className="text-[13.5px] font-bold text-[var(--color-ink)]">
              {lang === 'fr' ? 'Sources du module' : 'Module sources'}
            </h3>
            <p className="text-[11.5px] text-[var(--color-ink-muted)]">{module.titre}</p>
          </div>
          <button onClick={onClose}
            className="w-7 h-7 rounded-lg flex items-center justify-center text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-1)] shrink-0">
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto px-5 py-4 flex flex-col gap-3">
          {(module.sources || []).map((s, i) => (
            <div key={i} className="rounded-xl border border-[var(--color-border)] p-3.5">
              <div className="flex items-center justify-between gap-2 mb-1.5">
                <span className="flex items-center gap-1.5 text-[12.5px] font-semibold text-[var(--color-ink)]">
                  <FileText className="w-3.5 h-3.5 text-[var(--color-ink-muted)]" />
                  {s.documentTitre}
                </span>
                <span className="text-[10.5px] text-[var(--color-ink-muted)] shrink-0">
                  {lang === 'fr' ? 'pertinence' : 'relevance'} {Math.round(s.score * 100)}%
                </span>
              </div>
              <p className="text-[12px] text-[var(--color-ink-muted)] leading-relaxed italic">&ldquo;{s.extrait}&rdquo;</p>
            </div>
          ))}
        </div>
      </motion.div>
    </motion.div>,
    document.body
  );
}

// Side-by-side comparison so the user decides — a correction is never applied silently.
function CorrectionPreviewModal({ preview, titre, onClose, onApply, applying, lang }) {
  const { avant, apres } = preview;
  const unchanged = avant.objectifs === apres.objectifs && avant.modules === apres.modules
    && avant.methodesEvaluation === apres.methodesEvaluation;
  const diff = unchanged ? null : diffFormationContent(avant, apres);

  return createPortal(
    <motion.div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/40 px-4"
      initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
      onClick={() => !applying && onClose()}>
      <motion.div className="w-full max-w-6xl max-h-[88vh] flex flex-col rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] shadow-[0_24px_60px_rgba(0,0,0,0.3)] overflow-hidden"
        initial={{ opacity: 0, scale: 0.95, y: 16 }} animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.95, y: 16 }}
        transition={{ type: 'spring', stiffness: 340, damping: 30 }}
        onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between gap-3 px-5 py-4 border-b border-[var(--color-border)] bg-[var(--color-surface-2)]">
          <div className="flex items-center gap-3">
            <Wrench className="w-4.5 h-4.5 text-[var(--color-primary)]" />
            <h3 className="text-[14px] font-bold text-[var(--color-ink)]">
              {lang === 'fr' ? 'Aperçu de la correction' : 'Correction preview'}
            </h3>
            <span className="flex items-center gap-1.5 text-[12.5px] font-semibold text-[var(--color-ink-muted)]">
              {avant.qualiteScore} <ArrowRight className="w-3.5 h-3.5" style={{ color: NIVEAU_COLOR[apres.qualiteNiveau] }} /> {apres.qualiteScore}
            </span>
          </div>
          <button onClick={onClose} disabled={applying}
            className="w-7 h-7 rounded-lg flex items-center justify-center text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-1)] disabled:opacity-40 shrink-0">
            <X className="w-4 h-4" />
          </button>
        </div>

        {unchanged ? (
          <div className="flex-1 flex items-center justify-center py-16 px-5">
            <p className="text-[13px] text-[var(--color-ink-muted)] text-center max-w-sm">
              {lang === 'fr'
                ? "Aucun changement à proposer — le contenu respecte déjà les contrôles vérifiables."
                : 'Nothing to change — the content already passes the verifiable checks.'}
            </p>
          </div>
        ) : (
          <div className="flex-1 overflow-hidden grid grid-cols-1 lg:grid-cols-2 divide-y lg:divide-y-0 lg:divide-x divide-[var(--color-border)]">
            {[
              { label: lang === 'fr' ? 'Avant' : 'Before', content: avant, diff: null },
              { label: lang === 'fr' ? 'Après' : 'After', content: apres, diff },
            ].map((col, i) => (
              <div key={i} className="flex flex-col min-h-0">
                <div className="px-5 py-2.5 border-b border-[var(--color-border)] flex items-center justify-between shrink-0">
                  <span className="text-[11.5px] font-bold uppercase tracking-wide text-[var(--color-ink-muted)]">{col.label}</span>
                  <span className="text-[12px] font-bold" style={{ color: NIVEAU_COLOR[col.content.qualiteNiveau] }}>
                    {col.content.qualiteScore}/100
                  </span>
                </div>
                <div className="flex-1 overflow-y-auto p-5">
                  <FormationPreview formation={{ titre, ...col.content }} lang={lang} diff={col.diff} />
                </div>
              </div>
            ))}
          </div>
        )}

        <div className="flex items-center justify-end gap-2 px-5 py-3.5 border-t border-[var(--color-border)] bg-[var(--color-surface-2)] shrink-0">
          <button type="button" onClick={onClose} disabled={applying}
            className="px-4 py-2.5 rounded-xl text-[13px] font-semibold text-[var(--color-ink)] hover:bg-[var(--color-surface-1)] disabled:opacity-40 transition-colors">
            {lang === 'fr' ? "Garder l'original" : 'Keep original'}
          </button>
          {!unchanged && (
            <button type="button" onClick={onApply} disabled={applying}
              className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-[var(--color-primary)] text-white text-[13px] font-semibold disabled:opacity-40 transition-opacity">
              {applying ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
              {lang === 'fr' ? 'Appliquer la correction' : 'Apply correction'}
            </button>
          )}
        </div>
      </motion.div>
    </motion.div>,
    document.body
  );
}

// ---------- Editors ----------

function ObjectifsEditor({ label, value, onChange }) {
  const { lang } = useLanguage();

  function update(patch) { onChange({ ...value, ...patch }); }

  const testPositionnement = value.testPositionnement || {};
  function updateTest(patch) { update({ testPositionnement: { ...testPositionnement, ...patch } }); }

  const moduleBonus = value.moduleBonus || {};
  function updateBonus(patch) { update({ moduleBonus: { ...moduleBonus, ...patch } }); }

  return (
    <Field label={label}>
      <div className="flex flex-col gap-2">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          <input value={value.publicCible || ''} onChange={e => update({ publicCible: e.target.value })}
            placeholder={lang === 'fr' ? 'Public cible' : 'Target audience'} className={`${inputClass} text-[12.5px]`} />
          <input value={value.prerequisDetailles || ''} onChange={e => update({ prerequisDetailles: e.target.value })}
            placeholder={lang === 'fr' ? 'Prérequis détaillés' : 'Detailed prerequisites'} className={`${inputClass} text-[12.5px]`} />
        </div>

        <input
          value={(value.ressourcesPedagogiques || []).map(resourceLabel).join(', ')}
          onChange={e => update({ ressourcesPedagogiques: e.target.value.split(',').map(s => s.trim()).filter(Boolean) })}
          placeholder={lang === 'fr' ? 'Ressources pédagogiques (séparées par virgules)' : 'Learning resources (comma-separated)'}
          className={`${inputClass} text-[12.5px] mt-1.5`} />

        <div className="flex flex-col gap-1.5 mt-1.5">
          <span className="text-[11.5px] font-semibold text-[var(--color-ink-muted)]">
            {lang === 'fr' ? 'Test de positionnement' : 'Placement test'}
          </span>
          <input value={testPositionnement.objectif || ''} onChange={e => updateTest({ objectif: e.target.value })}
            placeholder={lang === 'fr' ? 'Objectif du test' : 'Test objective'} className={`${inputClass} text-[12px]`} />
          <input value={testPositionnement.exercice || ''} onChange={e => updateTest({ exercice: e.target.value })}
            placeholder={lang === 'fr' ? 'Exercice diagnostique' : 'Diagnostic exercise'} className={`${inputClass} text-[12px]`} />
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
            <input type="number" value={testPositionnement.qcmQuestions ?? ''} onChange={e => updateTest({ qcmQuestions: e.target.value === '' ? null : Number(e.target.value) })}
              placeholder="QCM (nb questions)" className={`${inputClass} text-[12px]`} />
            <input type="number" value={testPositionnement.seuilParcoursStandardPct ?? ''} onChange={e => updateTest({ seuilParcoursStandardPct: e.target.value === '' ? null : Number(e.target.value) })}
              placeholder={lang === 'fr' ? 'Seuil standard (%)' : 'Standard threshold (%)'} className={`${inputClass} text-[12px]`} />
            <input type="number" value={testPositionnement.moduleRemediation ?? ''} onChange={e => updateTest({ moduleRemediation: e.target.value === '' ? null : Number(e.target.value) })}
              placeholder={lang === 'fr' ? 'N° module remédiation' : 'Remediation module #'} className={`${inputClass} text-[12px]`} />
          </div>
        </div>

        <div className="flex flex-col gap-1.5 mt-1.5">
          <span className="text-[11.5px] font-semibold text-[var(--color-ink-muted)]">
            {lang === 'fr' ? 'Module bonus' : 'Bonus module'}
          </span>
          <label className="flex items-center gap-2 text-[12px] text-[var(--color-ink)]">
            <input type="checkbox" checked={!!moduleBonus.inclusDansTroncCommun} onChange={e => updateBonus({ inclusDansTroncCommun: e.target.checked })}
              className="accent-[var(--color-primary)]" />
            {lang === 'fr' ? 'Inclus dans le tronc commun' : 'Included in the core track'}
          </label>
          <div className="flex items-center gap-2">
            <input value={moduleBonus.titre || ''} onChange={e => updateBonus({ titre: e.target.value })}
              placeholder={lang === 'fr' ? 'Titre du module bonus' : 'Bonus module title'} className={`${inputClass} flex-1 text-[12px]`} />
            <input type="number" value={moduleBonus.dureeHeures ?? ''} onChange={e => updateBonus({ dureeHeures: e.target.value === '' ? null : Number(e.target.value) })}
              placeholder="h" className={`${inputClass} w-16 shrink-0 text-[12px]`} />
          </div>
          <input
            value={(moduleBonus.contenu || []).join(', ')}
            onChange={e => updateBonus({ contenu: e.target.value.split(',').map(s => s.trim()).filter(Boolean) })}
            placeholder={lang === 'fr' ? 'Contenu (séparé par virgules)' : 'Content (comma-separated)'}
            className={`${inputClass} text-[12px]`} />
        </div>
      </div>
    </Field>
  );
}

function ModulesEditor({ label, items, onChange }) {
  const { lang } = useLanguage();

  function update(i, patch) {
    onChange(items.map((m, idx) => idx === i ? { ...m, ...patch } : m));
  }
  function updateMethode(i, patch) { update(i, { methode: { ...(items[i].methode || {}), ...patch } }); }
  function updateExercice(i, patch) { update(i, { exerciceFormatif: { ...(items[i].exerciceFormatif || {}), ...patch } }); }
  function remove(i) { onChange(items.filter((_, idx) => idx !== i)); }
  function add() {
    const nextNumero = items.length > 0 ? Math.max(...items.map(m => m.numero || 0)) + 1 : 1;
    onChange([...items, {
      numero: nextNumero, titre: '', dureeHeures: null, objectif: '', methode: { type: '', pctTheorie: null, pctPratique: null },
      contenu: [], exerciceFormatif: { type: '', consigne: '', dureeMin: null }, livrable: '',
      reutiliseLivrableModule: null, competencesPrerequises: [],
    }]);
  }

  return (
    <Field label={label}>
      <div className="flex flex-col gap-3">
        {items.map((m, i) => {
          const color = CARD_COLORS[i % CARD_COLORS.length];
          return (
            <div key={i} className={`flex gap-3 p-3.5 rounded-2xl border border-l-4 ${color.border} border-[var(--color-border)] bg-[var(--color-surface-1)]`}>
              <div className={`w-8 h-8 rounded-full ${color.badge} text-white flex items-center justify-center font-bold text-[12.5px] shrink-0`}>
                {m.numero}
              </div>
              <div className="flex-1 min-w-0 flex flex-col gap-2">
                <div className="flex items-center gap-2">
                  <input type="number" value={m.numero ?? ''} onChange={e => update(i, { numero: Number(e.target.value) })}
                    className={`${inputClass} w-14 text-center text-[12px] shrink-0`} />
                  <input value={m.titre || ''} onChange={e => update(i, { titre: e.target.value })}
                    placeholder={lang === 'fr' ? 'Titre du module' : 'Module title'}
                    className={`${inputClass} flex-1 min-w-0 font-bold text-[13.5px] ${color.text}`} />
                  <input type="number" value={m.dureeHeures ?? ''} onChange={e => update(i, { dureeHeures: e.target.value === '' ? null : Number(e.target.value) })}
                    placeholder="h" className={`${inputClass} w-16 text-center text-[12px] shrink-0`} />
                  <button type="button" onClick={() => remove(i)}
                    className="w-7 h-7 rounded-lg flex items-center justify-center text-[var(--color-ink-muted)] hover:bg-red-500/10 hover:text-red-500 transition-colors shrink-0">
                    <X className="w-3.5 h-3.5" />
                  </button>
                </div>
                <input value={m.objectif || ''} onChange={e => update(i, { objectif: e.target.value })}
                  placeholder={lang === 'fr' ? 'Objectif : Être capable de...' : 'Objective: ...'}
                  className={`${inputClass} text-[12.5px]`} />
                <input
                  value={(m.contenu || []).join(', ')}
                  onChange={e => update(i, { contenu: e.target.value.split(',').map(s => s.trim()).filter(Boolean) })}
                  placeholder={lang === 'fr' ? 'Contenu (séparé par des virgules)' : 'Content (comma-separated)'}
                  className={`${inputClass} text-[12px]`} />
                <div className="flex items-center gap-2">
                  <input value={m.livrable || ''} onChange={e => update(i, { livrable: e.target.value })}
                    placeholder={lang === 'fr' ? 'Livrable' : 'Deliverable'}
                    className={`${inputClass} flex-1 text-[12px]`} />
                  <input type="number" value={m.reutiliseLivrableModule ?? ''} onChange={e => update(i, { reutiliseLivrableModule: e.target.value === '' ? null : Number(e.target.value) })}
                    placeholder={lang === 'fr' ? 'Réutilise module #' : 'Reuses module #'} className={`${inputClass} w-32 shrink-0 text-[11.5px]`} />
                </div>
                <input
                  value={(m.competencesPrerequises || []).join(', ')}
                  onChange={e => update(i, { competencesPrerequises: e.target.value.split(',').map(s => s.trim()).filter(Boolean).map(Number) })}
                  placeholder={lang === 'fr' ? 'Modules prérequis (numéros séparés par virgules)' : 'Prerequisite module numbers (comma-separated)'}
                  className={`${inputClass} text-[12px]`} />
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                  <input value={m.methode?.type || ''} onChange={e => updateMethode(i, { type: e.target.value })}
                    placeholder={lang === 'fr' ? 'Méthode pédagogique' : 'Teaching method'} className={`${inputClass} text-[12px]`} />
                  <input type="number" value={m.methode?.pctTheorie ?? ''} onChange={e => updateMethode(i, { pctTheorie: e.target.value === '' ? null : Number(e.target.value) })}
                    placeholder="% théorie" className={`${inputClass} text-[12px]`} />
                  <input type="number" value={m.methode?.pctPratique ?? ''} onChange={e => updateMethode(i, { pctPratique: e.target.value === '' ? null : Number(e.target.value) })}
                    placeholder="% pratique" className={`${inputClass} text-[12px]`} />
                </div>
                <div className="flex items-center gap-2">
                  <input value={m.exerciceFormatif?.type || ''} onChange={e => updateExercice(i, { type: e.target.value })}
                    placeholder={lang === 'fr' ? 'Type exercice' : 'Exercise type'} className={`${inputClass} w-28 shrink-0 text-[11.5px]`} />
                  <input value={m.exerciceFormatif?.consigne || ''} onChange={e => updateExercice(i, { consigne: e.target.value })}
                    placeholder={lang === 'fr' ? 'Consigne' : 'Instructions'} className={`${inputClass} flex-1 text-[11.5px]`} />
                  <input type="number" value={m.exerciceFormatif?.dureeMin ?? ''} onChange={e => updateExercice(i, { dureeMin: e.target.value === '' ? null : Number(e.target.value) })}
                    placeholder="min" className={`${inputClass} w-16 shrink-0 text-[11.5px]`} />
                </div>
              </div>
            </div>
          );
        })}
        <button type="button" onClick={add}
          className="flex items-center justify-center gap-1.5 py-2.5 rounded-xl border border-dashed border-[var(--color-border)] text-[12.5px] font-medium text-[var(--color-ink-muted)] hover:text-[var(--color-primary)] hover:border-[var(--color-primary)]/40 transition-colors">
          <Plus className="w-3.5 h-3.5" />
          {lang === 'fr' ? 'Ajouter un module' : 'Add a module'}
        </button>
      </div>
    </Field>
  );
}

function EvaluationEditor({ label, items, onChange }) {
  const { lang } = useLanguage();

  function update(i, patch) { onChange(items.map((m, idx) => idx === i ? { ...m, ...patch } : m)); }
  function remove(i) { onChange(items.filter((_, idx) => idx !== i)); }
  function add() { onChange([...items, { nom: '', pct: null, estEvaluationContinue: false }]); }

  return (
    <Field label={label}>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
        {items.map((item, i) => {
          const color = CARD_COLORS[i % CARD_COLORS.length];
          return (
            <div key={i} className={`flex items-start gap-2 p-3 rounded-xl border border-l-4 ${color.border} border-[var(--color-border)] bg-[var(--color-surface-1)]`}>
              <div className={`w-6 h-6 rounded-full ${color.badge} text-white flex items-center justify-center font-bold text-[10.5px] shrink-0 mt-0.5`}>{i + 1}</div>
              <div className="flex-1 min-w-0 flex flex-col gap-1.5">
                <input value={item.nom || ''} onChange={e => update(i, { nom: e.target.value })}
                  placeholder={lang === 'fr' ? 'Nom de la méthode' : 'Method name'} className={`${inputClass} font-semibold text-[12.5px] ${color.text}`} />
                <div className="flex items-center gap-2">
                  <input type="number" value={item.pct ?? ''} onChange={e => update(i, { pct: e.target.value === '' ? null : Number(e.target.value) })}
                    placeholder="%" className={`${inputClass} w-20 text-[11px]`} />
                  <label className="flex items-center gap-1.5 text-[11px] text-[var(--color-ink)]">
                    <input type="checkbox" checked={!!item.estEvaluationContinue} onChange={e => update(i, { estEvaluationContinue: e.target.checked })}
                      className="accent-[var(--color-primary)]" />
                    {lang === 'fr' ? 'Continue' : 'Continuous'}
                  </label>
                </div>
              </div>
              <button type="button" onClick={() => remove(i)}
                className="w-6 h-6 rounded-lg flex items-center justify-center text-[var(--color-ink-muted)] hover:bg-red-500/10 hover:text-red-500 transition-colors shrink-0">
                <X className="w-3.5 h-3.5" />
              </button>
            </div>
          );
        })}
      </div>
      <button type="button" onClick={add}
        className="flex items-center justify-center gap-1.5 py-2 rounded-xl border border-dashed border-[var(--color-border)] text-[12px] font-medium text-[var(--color-ink-muted)] hover:text-[var(--color-primary)] hover:border-[var(--color-primary)]/40 transition-colors">
        <Plus className="w-3.5 h-3.5" />
        {lang === 'fr' ? 'Ajouter' : 'Add'}
      </button>
    </Field>
  );
}

// ---------- Main page ----------

export default function FormationDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { lang } = useLanguage();

  const [formation, setFormation] = useState(null);
  const [loading, setLoading] = useState(true);
  const [qualite, setQualite] = useState(null);
  const [qualiteLoading, setQualiteLoading] = useState(true);
  const [mode, setMode] = useState('preview'); // 'preview' | 'planning' | 'edit'
  const [form, setForm] = useState(null);
  const [planningDraft, setPlanningDraft] = useState(null); // { modules, moduleBonus }
  const [planningBaseline, setPlanningBaseline] = useState(null);
  const [savingPlanning, setSavingPlanning] = useState(false);
  const [saving, setSaving] = useState(false);
  const [statutSaving, setStatutSaving] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [exportingFormat, setExportingFormat] = useState(null); // 'pdf' | 'pptx' | null
  const [exportMenuOpen, setExportMenuOpen] = useState(false);
  const exportMenuRef = useRef();
  const [toast, setToast] = useState(null);
  const [sourceModal, setSourceModal] = useState(null);
  const [catchingUp, setCatchingUp] = useState(false);
  const [correctionPreview, setCorrectionPreview] = useState(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [applyingCorrection, setApplyingCorrection] = useState(false);

  const showToast = (msg, type = 'success') => {
    setToast({ msg, type });
    setTimeout(() => setToast(null), 3000);
  };

  // Always holds the id of the formation actually being viewed right now — read by load()/loadQualite()
  // after their await resolves. Without this, navigating quickly from formation A's page to formation
  // B's (same route, component not remounted) could let A's late-arriving response overwrite B's
  // already-loaded state: a classic stale-response race, since neither fetch was ever cancelled or
  // told to ignore itself once superseded.
  const latestIdRef = useRef(id);
  useEffect(() => { latestIdRef.current = id; }, [id]);

  const load = useCallback(async () => {
    const requestId = id;
    setLoading(true);
    try {
      const data = await getFormationById(id);
      if (latestIdRef.current !== requestId) return;
      setFormation(data);
    } catch {
      if (latestIdRef.current !== requestId) return;
      showToast(lang === 'fr' ? 'Erreur de chargement.' : 'Load error.', 'error');
    } finally {
      if (latestIdRef.current === requestId) setLoading(false);
    }
  }, [id, lang]);

  const loadQualite = useCallback(async () => {
    const requestId = id;
    setQualiteLoading(true);
    try {
      const data = await getFormationQualite(id);
      if (latestIdRef.current !== requestId) return;
      setQualite(data);
    } catch {
      if (latestIdRef.current === requestId) setQualite(null);
    } finally {
      if (latestIdRef.current === requestId) setQualiteLoading(false);
    }
  }, [id]);

  // eslint-disable-next-line react-hooks/set-state-in-effect -- initial data fetch on mount / id change
  useEffect(() => { load(); loadQualite(); }, [load, loadQualite]);

  // Close the export dropdown on an outside click, same pattern as the category dropdown in
  // Documents.jsx.
  useEffect(() => {
    function onClickOutside(e) {
      if (exportMenuRef.current && !exportMenuRef.current.contains(e.target)) setExportMenuOpen(false);
    }
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, []);

  function enterEditMode() {
    setForm({
      titre: formation.titre,
      objectifs: parseObjectifs(formation.objectifs),
      dureeEstimee: formation.dureeEstimee ?? '',
      modules: parseCards(formation.modules),
      methodesEvaluation: parseCards(formation.methodesEvaluation),
    });
    setMode('edit');
  }

  function enterPlanningMode() {
    const parsed = parseCards(formation.modules)
      .sort((a, b) => (a.numero ?? 0) - (b.numero ?? 0));
    // Every numero-keyed lookup downstream (PlanningTimeline's byNumero map, the schedule computed
    // by computePlanningJours, regen-state matching) silently collapses if two modules share a
    // numero — a formation from an older/broken generation run can have this (all modules at the
    // same numero, e.g. 0), which renders as N identical duplicate cards instead of the real
    // distinct modules. Renumbering unconditionally on entry — not only on save — fixes the display
    // immediately, before the user has to spot and drag anything.
    const renumbered = renumberModules(parsed);
    const modules = renumbered.map(m => ({ ...m, _uid: makeUid() }));
    const moduleBonus = parseObjectifs(formation.objectifs).moduleBonus || null;
    setPlanningDraft({ modules, moduleBonus });
    // Baseline is the ORIGINAL (pre-renumber) shape: for an already well-numbered formation,
    // renumbering is a no-op and nothing appears dirty, same as before. For a broken one, the
    // renumbering itself is the "change" — Save becomes available right away, no drag required.
    setPlanningBaseline(JSON.stringify(parsed));
    setMode('planning');
  }

  const planningDirty = planningDraft
    ? JSON.stringify(planningDraft.modules.map(stripUid)) !== planningBaseline
    : false;

  // Central mode switch — the only place that decides whether leaving Planning with unsaved drag/
  // duration/regenerate edits needs confirmation, so every entry point (toggle buttons, Annuler) goes
  // through the same check instead of risking a silent discard like the existing edit-mode Cancel does.
  function switchMode(target) {
    if (mode === 'planning' && target !== 'planning' && planningDirty) {
      const ok = window.confirm(lang === 'fr'
        ? 'Annuler les modifications de planning non enregistrées ?'
        : 'Discard unsaved planning changes?');
      if (!ok) return;
    }
    setPlanningDraft(null);
    if (target === 'planning') enterPlanningMode();
    else if (target === 'edit') enterEditMode();
    else setMode('preview');
  }

  async function handleRegenerateModule(numero) {
    return regenerateFormationModule(id, numero);
  }

  async function handleSavePlanning() {
    setSavingPlanning(true);
    try {
      // Always renumbered on save, not only after a drag — a module's `numero` can already be
      // missing/degenerate on formations from an older generation run (surfaces as "Module 0" in the
      // schedule and in the quality/planning computation, since both key off `numero`). Renumbering
      // unconditionally makes every Planning save self-healing, and is a no-op for already-clean data
      // since it reassigns 1..N by the current (already-sorted-on-entry) array order.
      const modules = renumberModules(planningDraft.modules.map(stripUid));
      const dureeEstimee = sumModuleHours(modules, planningDraft.moduleBonus);
      const updated = await updateFormation(id, {
        titre: formation.titre,
        objectifs: formation.objectifs,
        dureeEstimee,
        modules: JSON.stringify(modules),
        activites: formation.activites ?? '[]',
        methodesEvaluation: formation.methodesEvaluation,
      });
      setFormation(updated);
      setPlanningDraft(null);
      setMode('preview');
      showToast(lang === 'fr' ? 'Planning enregistré.' : 'Planning saved.');
      loadQualite();
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setSavingPlanning(false);
    }
  }

  async function handleSave(e) {
    e.preventDefault();
    setSaving(true);
    try {
      const updated = await updateFormation(id, {
        titre: form.titre,
        objectifs: JSON.stringify(form.objectifs),
        dureeEstimee: form.dureeEstimee === '' ? null : Number(form.dureeEstimee),
        modules: JSON.stringify(form.modules),
        activites: '[]',
        methodesEvaluation: JSON.stringify(form.methodesEvaluation),
      });
      setFormation(updated);
      setMode('preview');
      showToast(lang === 'fr' ? 'Formation enregistrée.' : 'Training saved.');
      loadQualite();
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setSaving(false);
    }
  }

  async function toggleStatut() {
    setStatutSaving(true);
    try {
      const next = formation.statut === 'VALIDEE' ? 'BROUILLON' : 'VALIDEE';
      const updated = await updateFormationStatut(id, next);
      setFormation(updated);
      showToast(next === 'VALIDEE'
        ? (lang === 'fr' ? 'Formation validée.' : 'Training validated.')
        : (lang === 'fr' ? 'Repassée en brouillon.' : 'Reverted to draft.'));
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setStatutSaving(false);
    }
  }

  async function handleCatchUpTraces() {
    setCatchingUp(true);
    try {
      const updated = await attachFormationTraces(id);
      setFormation(updated);
      showToast(lang === 'fr' ? 'Sources retrouvées.' : 'Sources found.');
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setCatchingUp(false);
    }
  }

  async function handleCorrect() {
    setPreviewLoading(true);
    try {
      const preview = await previewFormationCorrection(id);
      setCorrectionPreview(preview);
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setPreviewLoading(false);
    }
  }

  async function handleApplyCorrection() {
    if (!correctionPreview) return;
    const { apres } = correctionPreview;
    setApplyingCorrection(true);
    try {
      let updated = await updateFormation(id, {
        titre: formation.titre,
        objectifs: apres.objectifs,
        dureeEstimee: apres.dureeEstimee,
        modules: apres.modules,
        activites: '[]',
        methodesEvaluation: apres.methodesEvaluation,
      });
      if (apres.modules !== formation.modules) {
        try { updated = await attachFormationTraces(id); } catch { /* keep the applied correction even if this fails */ }
      }
      setFormation(updated);
      setCorrectionPreview(null);
      showToast(lang === 'fr' ? 'Correction appliquée.' : 'Correction applied.');
      loadQualite();
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setApplyingCorrection(false);
    }
  }

  async function handleExport(format) {
    setExportMenuOpen(false);
    setExportingFormat(format);
    try {
      if (format === 'pptx') await exportFormationPptx(id, formation.titre);
      else await exportFormationPdf(id, formation.titre);
    } catch (err) {
      // A plain generic message hid whether this was a 403 (someone else's formation, or a role
      // change since the page loaded) vs a real server error — and nothing was logged, so a report
      // of "export failed" had no way to be diagnosed. Blob responses put the server's JSON error body
      // in err.response.data as a Blob, not a parsed object, so it's read as text first.
      let serverMessage;
      if (err.response?.data instanceof Blob) {
        try { serverMessage = JSON.parse(await err.response.data.text())?.message; } catch { /* not JSON — fall through to the generic message */ }
      } else {
        serverMessage = err.response?.data?.message;
      }

      console.error('Export failed', { format, status: err.response?.status, err });

      const message = err.response?.status === 403
        ? (lang === 'fr' ? "Vous n'avez pas accès à cette formation." : "You don't have access to this formation.")
        : serverMessage || (lang === 'fr' ? "Erreur lors de l'export." : 'Export failed.');
      showToast(message, 'error');
    } finally {
      setExportingFormat(null);
    }
  }

  async function handleDelete() {
    setDeleting(true);
    try {
      await deleteFormation(id);
      navigate('/formations');
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
      setDeleting(false);
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-24 text-[var(--color-ink-muted)]">
        <Loader2 className="w-5 h-5 animate-spin" />
      </div>
    );
  }

  if (!formation) return null;

  const isValidee = formation.statut === 'VALIDEE';

  return (
    <div className="max-w-4xl mx-auto flex flex-col gap-6 pb-10">
      <AnimatePresence>
        {toast && (
          <motion.div
            className={`fixed top-4 right-4 z-[70] px-4 py-2.5 rounded-xl text-[13px] font-medium shadow-lg ${
              toast.type === 'error' ? 'bg-red-500 text-white' : 'bg-emerald-500 text-white'
            }`}
            initial={{ opacity: 0, y: -14 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -14 }}
          >
            {toast.msg}
          </motion.div>
        )}
      </AnimatePresence>

      <div className="flex items-center justify-between gap-3 flex-wrap">
        <button onClick={() => navigate('/formations')}
          className="flex items-center gap-1.5 text-[12.5px] font-medium text-[var(--color-ink-muted)] hover:text-[var(--color-ink)] transition-colors">
          <ArrowLeft className="w-4 h-4" />
          {lang === 'fr' ? 'Retour aux formations' : 'Back to trainings'}
        </button>

        <div className="flex items-center gap-2">
          <span className={`px-2.5 py-1 rounded-full text-[11px] font-semibold ${
            isValidee
              ? 'bg-emerald-500/12 text-emerald-600 dark:text-emerald-400'
              : 'bg-amber-500/12 text-amber-600 dark:text-amber-400'
          }`}>
            {isValidee ? (lang === 'fr' ? 'Validée' : 'Validated') : (lang === 'fr' ? 'Brouillon' : 'Draft')}
          </span>
          <button
            onClick={toggleStatut}
            disabled={statutSaving}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] font-semibold border border-[var(--color-border)] text-[var(--color-ink)] hover:bg-[var(--color-surface-2)] transition-colors disabled:opacity-40"
          >
            {statutSaving ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
              : isValidee ? <RotateCcw className="w-3.5 h-3.5" /> : <ShieldCheck className="w-3.5 h-3.5" />}
            {isValidee ? (lang === 'fr' ? 'Repasser en brouillon' : 'Revert to draft') : (lang === 'fr' ? 'Valider' : 'Validate')}
          </button>
          <div className="flex items-center rounded-lg border border-[var(--color-border)] overflow-hidden">
            {[
              { key: 'preview', icon: Eye, label: lang === 'fr' ? 'Aperçu' : 'Preview' },
              { key: 'planning', icon: CalendarRange, label: lang === 'fr' ? 'Planning' : 'Planning' },
              { key: 'edit', icon: Pencil, label: lang === 'fr' ? 'Modifier' : 'Edit' },
            ].map(({ key, icon: Icon, label }) => (
              <button key={key} onClick={() => switchMode(key)}
                className={`flex items-center gap-1.5 px-3 py-1.5 text-[12px] font-semibold transition-colors ${
                  mode === key ? 'bg-[var(--color-primary)] text-white' : 'text-[var(--color-ink)] hover:bg-[var(--color-surface-2)]'
                }`}>
                <Icon className="w-3.5 h-3.5" />
                {label}
              </button>
            ))}
          </div>
          <div className="relative" ref={exportMenuRef}>
            <button
              onClick={() => setExportMenuOpen(o => !o)}
              disabled={!!exportingFormat}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] font-semibold bg-[var(--color-primary)] text-white hover:opacity-90 transition-opacity disabled:opacity-40"
            >
              {exportingFormat ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Download className="w-3.5 h-3.5" />}
              {lang === 'fr' ? 'Exporter' : 'Export'}
              <ChevronDown className={`w-3.5 h-3.5 transition-transform ${exportMenuOpen ? 'rotate-180' : ''}`} />
            </button>
            <AnimatePresence>
              {exportMenuOpen && (
                <motion.div
                  initial={{ opacity: 0, y: -6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -6 }}
                  className="absolute right-0 mt-1.5 w-48 rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-1)] shadow-[0_12px_28px_rgba(0,0,0,0.18)] overflow-hidden z-20"
                >
                  <button
                    onClick={() => handleExport('pdf')}
                    className="w-full flex items-center gap-2 px-3.5 py-2.5 text-[12.5px] font-medium text-[var(--color-ink)] hover:bg-[var(--color-surface-2)] transition-colors"
                  >
                    <FileText className="w-3.5 h-3.5 text-[var(--color-ink-muted)]" />
                    PDF
                  </button>
                  <button
                    onClick={() => handleExport('pptx')}
                    className="w-full flex items-center gap-2 px-3.5 py-2.5 text-[12.5px] font-medium text-[var(--color-ink)] hover:bg-[var(--color-surface-2)] transition-colors border-t border-[var(--color-border)]"
                  >
                    <Presentation className="w-3.5 h-3.5 text-[var(--color-ink-muted)]" />
                    PowerPoint
                  </button>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </div>
      </div>

      {formation.documents.length > 0 && (
        <div className="flex flex-wrap gap-1.5 -mt-2">
          {formation.documents.map(d => (
            <span key={d.documentId} className="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11.5px] font-medium bg-[var(--color-surface-2)] text-[var(--color-ink)]">
              <FileText className="w-3 h-3 text-[var(--color-ink-muted)]" />
              {d.documentTitre}
              {d.scorePertinence != null && (
                <span className="text-[var(--color-ink-muted)]">{Math.round(d.scorePertinence * 100)}%</span>
              )}
            </span>
          ))}
        </div>
      )}

      {mode === 'preview' ? (
        <>
          <QualityPanel
            report={qualite} loading={qualiteLoading} lang={lang} onRetry={loadQualite}
            onCorrect={handleCorrect} correcting={previewLoading}
          />
          <FormationPreview
            formation={formation} lang={lang}
            onOpenSource={setSourceModal}
            onCatchUpTraces={handleCatchUpTraces}
            catchingUp={catchingUp}
          />
        </>
      ) : mode === 'planning' ? (
        planningDraft && (
          <div className="flex flex-col gap-4 pb-16">
            <p className="text-[12px] text-[var(--color-ink-muted)]">
              {lang === 'fr'
                ? 'Glissez un module pour le réordonner ou le déplacer vers un autre jour. Les jours sont recalculés automatiquement.'
                : 'Drag a module to reorder it or move it to another day. Days are recomputed automatically.'}
            </p>
            <PlanningTimeline
              modules={planningDraft.modules}
              moduleBonus={planningDraft.moduleBonus}
              onChange={modules => setPlanningDraft(d => ({ ...d, modules }))}
              onRegenerate={handleRegenerateModule}
              lang={lang}
            />

            <AnimatePresence>
              {planningDirty && (
                <motion.div
                  initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: 20 }}
                  className="fixed bottom-6 left-1/2 -translate-x-1/2 z-[60] flex items-center gap-3 px-4 py-3 rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] shadow-[0_16px_40px_rgba(0,0,0,0.25)]"
                >
                  <span className="text-[12.5px] font-medium text-[var(--color-ink)]">
                    {lang === 'fr' ? 'Modifications non enregistrées' : 'Unsaved changes'}
                  </span>
                  <button onClick={() => switchMode('preview')}
                    className="px-3 py-1.5 rounded-lg text-[12px] font-semibold text-[var(--color-ink)] hover:bg-[var(--color-surface-2)] transition-colors">
                    {lang === 'fr' ? 'Annuler' : 'Cancel'}
                  </button>
                  <button onClick={handleSavePlanning} disabled={savingPlanning}
                    className="flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg bg-[var(--color-primary)] text-white text-[12px] font-semibold disabled:opacity-40 transition-opacity">
                    {savingPlanning ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Check className="w-3.5 h-3.5" />}
                    {lang === 'fr' ? 'Enregistrer' : 'Save'}
                  </button>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        )
      ) : (
        <form onSubmit={handleSave} className="flex flex-col gap-5">
          <input
            value={form.titre}
            onChange={e => setForm(f => ({ ...f, titre: e.target.value }))}
            className="text-[20px] font-bold text-[var(--color-ink)] bg-transparent border-b border-transparent hover:border-[var(--color-border)] focus:border-[var(--color-primary)] focus:outline-none py-1 transition-colors"
          />

          <ObjectifsEditor
            label={lang === 'fr' ? 'Objectifs pédagogiques' : 'Learning objectives'}
            value={form.objectifs}
            onChange={v => setForm(f => ({ ...f, objectifs: v }))}
          />

          <Field label={lang === 'fr' ? 'Durée totale estimée (heures)' : 'Estimated total duration (hours)'}>
            <div className="flex items-center gap-2 max-w-[180px]">
              <Clock className="w-4 h-4 text-[var(--color-ink-muted)] shrink-0" />
              <input
                type="number" min="0" step="0.5"
                value={form.dureeEstimee}
                onChange={e => setForm(f => ({ ...f, dureeEstimee: e.target.value }))}
                className="w-full px-3.5 py-2.5 rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-0)] text-[13px] text-[var(--color-ink)] focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/40"
              />
            </div>
          </Field>

          <ModulesEditor
            label={lang === 'fr' ? 'Modules' : 'Modules'}
            items={form.modules}
            onChange={v => setForm(f => ({ ...f, modules: v }))}
          />
          <EvaluationEditor
            label={lang === 'fr' ? "Méthodes d'évaluation" : 'Evaluation methods'}
            items={form.methodesEvaluation}
            onChange={v => setForm(f => ({ ...f, methodesEvaluation: v }))}
          />

          <div className="flex items-center justify-between gap-3 pt-2 border-t border-[var(--color-border)]">
            <button
              type="button"
              onClick={() => setConfirmDelete(true)}
              className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-[12.5px] font-semibold text-red-500 hover:bg-red-500/10 transition-colors"
            >
              <Trash2 className="w-4 h-4" />
              {lang === 'fr' ? 'Supprimer' : 'Delete'}
            </button>
            <div className="flex items-center gap-2">
              <button type="button" onClick={() => setMode('preview')}
                className="px-4 py-2.5 rounded-xl text-[13px] font-semibold text-[var(--color-ink)] hover:bg-[var(--color-surface-2)] transition-colors">
                {lang === 'fr' ? 'Annuler' : 'Cancel'}
              </button>
              <button
                type="submit"
                disabled={saving}
                className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-[var(--color-primary)] text-white text-[13px] font-semibold disabled:opacity-40 transition-opacity"
              >
                {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
                {lang === 'fr' ? 'Enregistrer' : 'Save'}
              </button>
            </div>
          </div>
        </form>
      )}

      <AnimatePresence>
        {sourceModal && (
          <SourceModal module={sourceModal} onClose={() => setSourceModal(null)} lang={lang} />
        )}
      </AnimatePresence>

      <AnimatePresence>
        {correctionPreview && (
          <CorrectionPreviewModal
            preview={correctionPreview}
            titre={formation.titre}
            onClose={() => setCorrectionPreview(null)}
            onApply={handleApplyCorrection}
            applying={applyingCorrection}
            lang={lang}
          />
        )}
      </AnimatePresence>

      <AnimatePresence>
        {confirmDelete && (
          <motion.div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/40 px-4"
            initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            onClick={() => !deleting && setConfirmDelete(false)}>
            <motion.div className="w-full max-w-sm rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] shadow-[0_24px_60px_rgba(0,0,0,0.3)] p-5 flex flex-col gap-3"
              initial={{ opacity: 0, scale: 0.95, y: 16 }} animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 16 }}
              onClick={e => e.stopPropagation()}>
              <div className="flex items-center gap-2 text-red-500">
                <AlertTriangle className="w-5 h-5" />
                <h3 className="text-[14px] font-bold">{lang === 'fr' ? 'Supprimer cette formation ?' : 'Delete this training?'}</h3>
              </div>
              <p className="text-[12.5px] text-[var(--color-ink-muted)]">
                {lang === 'fr' ? 'Cette action est irréversible.' : 'This action cannot be undone.'}
              </p>
              <div className="flex justify-end gap-2 mt-1">
                <button onClick={() => setConfirmDelete(false)}
                  className="px-3.5 py-2 rounded-lg text-[12.5px] font-semibold text-[var(--color-ink)] hover:bg-[var(--color-surface-2)] transition-colors">
                  {lang === 'fr' ? 'Annuler' : 'Cancel'}
                </button>
                <button onClick={handleDelete} disabled={deleting}
                  className="flex items-center gap-1.5 px-3.5 py-2 rounded-lg text-[12.5px] font-semibold bg-red-500 text-white disabled:opacity-40 transition-opacity">
                  {deleting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Trash2 className="w-3.5 h-3.5" />}
                  {lang === 'fr' ? 'Supprimer' : 'Delete'}
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
