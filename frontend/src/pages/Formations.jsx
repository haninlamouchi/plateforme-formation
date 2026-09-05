import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useNavigate } from 'react-router-dom';
import {
  GraduationCap, Sparkles, X, Clock, FileText, Check, Loader2, ChevronRight, ShieldCheck,
} from 'lucide-react';
import { getAllFormations, generateFormation } from '../services/formationService';
import { getAllDocuments } from '../services/documentService';
import { useLanguage } from '../context/LanguageContext';

function parsePublicCible(objectifsJson) {
  if (!objectifsJson) return null;
  try {
    const parsed = JSON.parse(objectifsJson);
    return typeof parsed === 'object' && parsed !== null ? parsed.publicCible || null : null;
  } catch {
    return null;
  }
}

const STATUT_TABS = [
  { value: '', fr: 'Toutes', en: 'All' },
  { value: 'BROUILLON', fr: 'Brouillons', en: 'Drafts' },
  { value: 'VALIDEE', fr: 'Validées', en: 'Validated' },
];

const QUALITE_STYLE = {
  EXCELLENT: 'bg-emerald-500/12 text-emerald-600 dark:text-emerald-400',
  BON: 'bg-amber-500/12 text-amber-600 dark:text-amber-400',
  A_REVOIR: 'bg-red-500/12 text-red-600 dark:text-red-400',
};

function QualiteBadge({ score, niveau }) {
  if (score == null) return null;
  return (
    <span className={`flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold ${QUALITE_STYLE[niveau] ?? QUALITE_STYLE.A_REVOIR}`}>
      <ShieldCheck className="w-3 h-3" />
      {score}
    </span>
  );
}

function StatutBadge({ statut, lang }) {
  const isValidee = statut === 'VALIDEE';
  return (
    <span className={`px-2 py-0.5 rounded-full text-[11px] font-semibold ${
      isValidee
        ? 'bg-emerald-500/12 text-emerald-600 dark:text-emerald-400'
        : 'bg-amber-500/12 text-amber-600 dark:text-amber-400'
    }`}>
      {isValidee ? (lang === 'fr' ? 'Validée' : 'Validated') : (lang === 'fr' ? 'Brouillon' : 'Draft')}
    </span>
  );
}

export default function Formations() {
  const { lang } = useLanguage();
  const navigate = useNavigate();

  const [formations, setFormations] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [statutFilter, setStatutFilter] = useState('');

  const [wizardOpen, setWizardOpen] = useState(false);
  const [objectif, setObjectif] = useState('');
  const [docs, setDocs] = useState([]);
  const [selectedDocIds, setSelectedDocIds] = useState([]);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const params = {};
      if (statutFilter) params.statut = statutFilter;
      const result = await getAllFormations(params);
      setFormations(result.items ?? []);
      setTotal(result.total ?? 0);
    } catch {
      setFormations([]);
    } finally {
      setLoading(false);
    }
  }, [statutFilter]);

  // eslint-disable-next-line react-hooks/set-state-in-effect -- initial data fetch on mount / filter change
  useEffect(() => { load(); }, [load]);

  async function openWizard() {
    setWizardOpen(true);
    setObjectif('');
    setSelectedDocIds([]);
    setError('');
    try {
      const result = await getAllDocuments({ pageSize: 100 });
      setDocs((result.items ?? []).filter(d => d.statutTraitement === 'DISPONIBLE'));
    } catch {
      setDocs([]);
    }
  }

  function toggleDoc(id) {
    setSelectedDocIds(prev => prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]);
  }

  async function handleGenerate(e) {
    e.preventDefault();
    if (!objectif.trim() || generating) return;
    setGenerating(true);
    setError('');
    try {
      const formation = await generateFormation({
        objectif: objectif.trim(),
        documentIds: selectedDocIds.length ? selectedDocIds : undefined,
      });
      setWizardOpen(false);
      navigate(`/formations/${formation.id}`);
    } catch (err) {
      setError(err.response?.data?.message || (lang === 'fr' ? "Erreur lors de la génération." : 'Generation failed.'));
    } finally {
      setGenerating(false);
    }
  }

  return (
    <div className="max-w-6xl mx-auto flex flex-col gap-6">
      {/* Header */}
      <motion.div className="flex items-center justify-between gap-4 flex-wrap"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22 }}>
        <div className="flex items-center gap-3">
          <div className="w-11 h-11 rounded-xl bg-[var(--color-primary)]/10 flex items-center justify-center shrink-0">
            <GraduationCap className="w-5.5 h-5.5 text-[var(--color-primary)]" />
          </div>
          <div>
            <h1 className="text-[19px] font-bold text-[var(--color-ink)]">
              {lang === 'fr' ? 'Formations' : 'Trainings'}
            </h1>
            <p className="text-[12.5px] text-[var(--color-ink-muted)]">
              {total} {lang === 'fr' ? 'formation(s)' : 'training(s)'}
            </p>
          </div>
        </div>
        <button
          onClick={openWizard}
          className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-[var(--color-primary)] text-white text-[13px] font-semibold shadow-sm hover:opacity-90 transition-opacity"
        >
          <Sparkles className="w-4 h-4" />
          {lang === 'fr' ? 'Créer une formation' : 'Create a training'}
        </button>
      </motion.div>

      {/* Tabs */}
      <div className="flex items-center gap-1.5 border-b border-[var(--color-border)] pb-0.5">
        {STATUT_TABS.map(tab => (
          <button
            key={tab.value}
            onClick={() => setStatutFilter(tab.value)}
            className={`px-3.5 py-2 rounded-t-lg text-[12.5px] font-semibold transition-colors ${
              statutFilter === tab.value
                ? 'text-[var(--color-primary)] border-b-2 border-[var(--color-primary)]'
                : 'text-[var(--color-ink-muted)] hover:text-[var(--color-ink)]'
            }`}
          >
            {lang === 'fr' ? tab.fr : tab.en}
          </button>
        ))}
      </div>

      {/* List */}
      {loading ? (
        <div className="flex items-center justify-center py-20 text-[var(--color-ink-muted)]">
          <Loader2 className="w-5 h-5 animate-spin" />
        </div>
      ) : formations.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 gap-3 text-center">
          <GraduationCap className="w-10 h-10 text-[var(--color-ink-muted)]" />
          <p className="text-[13.5px] text-[var(--color-ink-muted)] max-w-sm">
            {lang === 'fr'
              ? "Aucune formation pour l'instant. Créez-en une à partir de vos documents indexés."
              : 'No trainings yet. Create one from your indexed documents.'}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {formations.map((f, i) => (
            <motion.button
              key={f.id}
              onClick={() => navigate(`/formations/${f.id}`)}
              className="text-left flex flex-col gap-3 p-4 rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] hover:border-[var(--color-primary)]/40 hover:shadow-[0_8px_24px_rgba(0,0,0,0.08)] transition-all"
              initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
              transition={{ delay: i * 0.03, type: 'spring', stiffness: 280, damping: 24 }}
            >
              <div className="flex items-start justify-between gap-2">
                <div className="flex items-center gap-1.5">
                  <StatutBadge statut={f.statut} lang={lang} />
                  <QualiteBadge score={f.qualiteScore} niveau={f.qualiteNiveau} />
                </div>
                <ChevronRight className="w-4 h-4 text-[var(--color-ink-muted)] shrink-0" />
              </div>
              <h3 className="text-[14.5px] font-bold text-[var(--color-ink)] leading-snug line-clamp-2">
                {f.titre}
              </h3>
              {parsePublicCible(f.objectifs) && (
                <p className="text-[12px] text-[var(--color-ink-muted)] line-clamp-2">{parsePublicCible(f.objectifs)}</p>
              )}
              <div className="flex items-center gap-3 mt-auto pt-2 border-t border-[var(--color-border)] text-[11.5px] text-[var(--color-ink-muted)]">
                {f.dureeEstimee != null && (
                  <span className="flex items-center gap-1">
                    <Clock className="w-3 h-3" />
                    {f.dureeEstimee}h
                  </span>
                )}
                <span className="flex items-center gap-1">
                  <FileText className="w-3 h-3" />
                  {f.documents.length}
                </span>
              </div>
            </motion.button>
          ))}
        </div>
      )}

      {/* Generation wizard */}
      <AnimatePresence>
        {wizardOpen && (
          <motion.div
            className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 px-4"
            initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            onClick={() => !generating && setWizardOpen(false)}
          >
            <motion.div
              className="w-full max-w-lg max-h-[85vh] flex flex-col rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] shadow-[0_24px_60px_rgba(0,0,0,0.3)] overflow-hidden"
              initial={{ opacity: 0, scale: 0.95, y: 16 }} animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 16 }}
              transition={{ type: 'spring', stiffness: 340, damping: 30 }}
              onClick={e => e.stopPropagation()}
            >
              <div className="flex items-center justify-between gap-2 px-5 py-4 border-b border-[var(--color-border)] bg-[var(--color-surface-2)]">
                <div className="flex items-center gap-2">
                  <Sparkles className="w-4.5 h-4.5 text-[var(--color-primary)]" />
                  <h3 className="text-[14px] font-bold text-[var(--color-ink)]">
                    {lang === 'fr' ? 'Créer une formation' : 'Create a training'}
                  </h3>
                </div>
                <button onClick={() => !generating && setWizardOpen(false)}
                  className="w-7 h-7 rounded-lg flex items-center justify-center text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-1)]">
                  <X className="w-4 h-4" />
                </button>
              </div>

              <form onSubmit={handleGenerate} className="flex-1 overflow-y-auto px-5 py-4 flex flex-col gap-4">
                <div className="flex flex-col gap-1.5">
                  <label className="text-[12.5px] font-semibold text-[var(--color-ink)]">
                    {lang === 'fr' ? 'Objectif de la formation' : 'Training objective'}
                  </label>
                  <textarea
                    autoFocus
                    value={objectif}
                    onChange={e => setObjectif(e.target.value)}
                    rows={3}
                    placeholder={lang === 'fr'
                      ? 'ex: Former les nouveaux développeurs aux bonnes pratiques Java en 2 jours'
                      : 'e.g. Train new developers on Java best practices over 2 days'}
                    className="px-3.5 py-2.5 rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-0)] text-[13px] text-[var(--color-ink)] placeholder:text-[var(--color-ink-muted)] resize-none focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/40"
                  />
                  <p className="text-[11px] text-[var(--color-ink-muted)]">
                    {lang === 'fr'
                      ? "L'IA analyse vos documents indexés pour construire un plan concret : modules, activités, durée, évaluation."
                      : 'The AI analyzes your indexed documents to build a concrete plan: modules, activities, duration, evaluation.'}
                  </p>
                </div>

                {docs.length > 0 && (
                  <div className="flex flex-col gap-1.5">
                    <label className="text-[12.5px] font-semibold text-[var(--color-ink)]">
                      {lang === 'fr'
                        ? `Documents sources (optionnel — ${selectedDocIds.length || 'sélection automatique'})`
                        : `Source documents (optional — ${selectedDocIds.length || 'auto-selected'})`}
                    </label>
                    <div className="max-h-40 overflow-y-auto flex flex-col gap-1 rounded-xl border border-[var(--color-border)] p-2">
                      {docs.map(d => (
                        <label key={d.id} className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-[var(--color-surface-2)] cursor-pointer">
                          <input
                            type="checkbox"
                            checked={selectedDocIds.includes(d.id)}
                            onChange={() => toggleDoc(d.id)}
                            className="accent-[var(--color-primary)]"
                          />
                          <span className="text-[12.5px] text-[var(--color-ink)] truncate">{d.titre}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}

                {error && (
                  <p className="text-[12px] text-red-500 bg-red-500/10 rounded-lg px-3 py-2">{error}</p>
                )}

                <button
                  type="submit"
                  disabled={!objectif.trim() || generating}
                  className="flex items-center justify-center gap-2 mt-1 px-4 py-2.5 rounded-xl bg-[var(--color-primary)] text-white text-[13px] font-semibold disabled:opacity-40 transition-opacity"
                >
                  {generating
                    ? <><Loader2 className="w-4 h-4 animate-spin" />{lang === 'fr' ? 'Génération en cours...' : 'Generating...'}</>
                    : <><Check className="w-4 h-4" />{lang === 'fr' ? 'Générer le plan' : 'Generate plan'}</>}
                </button>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
