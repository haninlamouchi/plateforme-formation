import { motion } from 'framer-motion';
import { X } from 'lucide-react';

const CATEGORY_COLORS = [
  { bg: 'bg-blue-500/12', text: 'text-blue-600 dark:text-blue-400', dot: 'bg-blue-500' },
  { bg: 'bg-purple-500/12', text: 'text-purple-600 dark:text-purple-400', dot: 'bg-purple-500' },
  { bg: 'bg-emerald-500/12', text: 'text-emerald-600 dark:text-emerald-400', dot: 'bg-emerald-500' },
  { bg: 'bg-amber-500/12', text: 'text-amber-600 dark:text-amber-400', dot: 'bg-amber-500' },
  { bg: 'bg-rose-500/12', text: 'text-rose-600 dark:text-rose-400', dot: 'bg-rose-500' },
  { bg: 'bg-cyan-500/12', text: 'text-cyan-600 dark:text-cyan-400', dot: 'bg-cyan-500' },
  { bg: 'bg-orange-500/12', text: 'text-orange-600 dark:text-orange-400', dot: 'bg-orange-500' },
  { bg: 'bg-indigo-500/12', text: 'text-indigo-600 dark:text-indigo-400', dot: 'bg-indigo-500' },
];

function colorForDomaine(domaine) {
  let hash = 0;
  for (let i = 0; i < domaine.length; i++) hash = (hash * 31 + domaine.charCodeAt(i)) >>> 0;
  return CATEGORY_COLORS[hash % CATEGORY_COLORS.length];
}

export default function CompetencesModal({ title, competences, onClose, lang }) {
  const domaines = [...new Set(competences.map(c => c.domaine))];

  return (
    <motion.div
      className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40 px-4"
      initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
      onClick={onClose}
    >
      <motion.div
        className="w-full max-w-md max-h-[80vh] flex flex-col rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] shadow-[0_24px_60px_rgba(0,0,0,0.3)] overflow-hidden"
        initial={{ opacity: 0, scale: 0.95, y: 16 }} animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.95, y: 16 }}
        transition={{ type: 'spring', stiffness: 340, damping: 30 }}
        onClick={e => e.stopPropagation()}
      >
        <div className="flex items-center justify-between gap-2 px-5 py-4 border-b border-[var(--color-border)] bg-[var(--color-surface-2)]">
          <div className="min-w-0">
            <h3 className="text-[14px] font-bold text-[var(--color-ink)]">
              {lang === 'fr' ? 'Compétences identifiées' : 'Identified competences'}
            </h3>
            {title && (
              <p className="text-[11.5px] text-[var(--color-ink-muted)] truncate">{title}</p>
            )}
          </div>
          <button onClick={onClose} className="w-7 h-7 rounded-lg flex items-center justify-center text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-1)] shrink-0">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4 flex flex-col gap-5">
          {domaines.length === 0 && (
            <p className="text-[12.5px] text-[var(--color-ink-muted)] text-center py-4">
              {lang === 'fr' ? 'Aucune compétence identifiée.' : 'No competences identified.'}
            </p>
          )}
          {domaines.map(domaine => {
            const color = colorForDomaine(domaine);
            return (
              <div key={domaine}>
                <div className="flex items-center gap-1.5 mb-2">
                  <span className={`w-2 h-2 rounded-full ${color.dot}`} />
                  <span className={`text-[11.5px] font-semibold uppercase tracking-wide ${color.text}`}>{domaine}</span>
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {competences.filter(c => c.domaine === domaine).map((c, i) => (
                    <span key={i} className={`px-2.5 py-1 rounded-full text-[12.5px] font-medium ${color.bg} ${color.text}`}>
                      {c.libelle}
                    </span>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </motion.div>
    </motion.div>
  );
}
