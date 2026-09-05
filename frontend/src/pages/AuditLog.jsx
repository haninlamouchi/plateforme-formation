import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ScrollText, ChevronLeft, ChevronRight } from 'lucide-react';
import { fetchAuditLog, fetchAuditLogActions } from '../services/adminService';
import { useLanguage } from '../context/LanguageContext';
import './UsersManagement.css';
import './AuditLog.css';

const PAGE_SIZE = 25;

// Curated colors for the most common actions — anything not listed here still gets a color, via
// colorForAction's deterministic hash-to-palette fallback below, so a new backend action added
// without a matching entry here never falls through to an unstyled/broken state.
const ACTION_COLORS = {
  UPLOAD_DOCUMENT: '#2563EB', GENERATE_FORMATION: '#7C3AED',
  EXPORT_FORMATION: '#0891B2', EXPORT_FORMATION_PPTX: '#0E7490',
  VALIDATE_USER: '#1e8e5a', REJECT_USER: '#b91c1c',
  ADMIN_DEACTIVATE_USER: '#b91c1c', ADMIN_REACTIVATE_USER: '#1e8e5a',
};

function colorForAction(action) {
  if (ACTION_COLORS[action]) return ACTION_COLORS[action];
  let h = 0;
  for (let i = 0; i < action.length; i++) h = action.charCodeAt(i) + ((h << 5) - h);
  const palette = ['#9B111E', '#A67C1B', '#1E8E5A', '#2563EB', '#7C3AED', '#DB2777', '#0891B2', '#374151'];
  return palette[Math.abs(h) % palette.length];
}

function formatAction(action, lang) {
  const label = action.replace(/_/g, ' ').toLowerCase();
  return lang === 'fr' ? label : label;
}

export default function AuditLog() {
  const { lang } = useLanguage();

  const [entries, setEntries] = useState([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [actionFilter, setActionFilter] = useState('');
  const [actions, setActions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  const totalPages = Math.ceil(total / PAGE_SIZE);

  const load = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const params = { page, pageSize: PAGE_SIZE };
      if (actionFilter) params.action = actionFilter;
      const result = await fetchAuditLog(params);
      setEntries(result.items ?? []);
      setTotal(result.total ?? 0);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, [page, actionFilter]);

  // eslint-disable-next-line react-hooks/set-state-in-effect -- initial data fetch on mount / page or filter change
  useEffect(() => { load(); }, [load]);
  useEffect(() => { fetchAuditLogActions().then(setActions).catch(() => {}); }, []);

  function handleActionFilterChange(value) {
    setActionFilter(value);
    setPage(1);
  }

  const formatDate = (iso) => new Date(iso).toLocaleString(lang === 'fr' ? 'fr-FR' : 'en-US', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  });

  return (
    <div className="um">
      <motion.div className="um-header"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22 }}>
        <div className="um-header__info">
          <ScrollText className="um-header__icon" />
          <div>
            <h1 className="um-header__title">
              {lang === 'fr' ? "Journal d'activité" : 'Audit log'}
            </h1>
            <p className="um-header__sub">
              {total} {lang === 'fr' ? 'action(s) enregistrée(s)' : 'recorded action(s)'}
            </p>
          </div>
        </div>
      </motion.div>

      <motion.div className="um-filters"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22, delay: 0.06 }}>
        <select className="um-select" value={actionFilter} onChange={e => handleActionFilterChange(e.target.value)}>
          <option value="">{lang === 'fr' ? 'Toutes les actions' : 'All actions'}</option>
          {actions.map(a => <option key={a} value={a}>{a}</option>)}
        </select>
      </motion.div>

      {loading ? (
        <div className="um-loading">
          <div className="um-loading__spinner" />
          <span>{lang === 'fr' ? 'Chargement...' : 'Loading...'}</span>
        </div>
      ) : error ? (
        <motion.div className="um-empty" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          <ScrollText className="um-empty__icon" />
          <p>{lang === 'fr' ? 'Impossible de charger le journal.' : 'Unable to load the audit log.'}</p>
        </motion.div>
      ) : entries.length === 0 ? (
        <motion.div className="um-empty" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          <ScrollText className="um-empty__icon" />
          <p>{lang === 'fr' ? 'Aucune activité trouvée.' : 'No activity found.'}</p>
        </motion.div>
      ) : (
        <motion.div className="um-table-wrap"
          initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
          transition={{ type: 'spring', stiffness: 260, damping: 22, delay: 0.1 }}>
          <table className="um-table">
            <thead>
              <tr>
                <th>{lang === 'fr' ? 'Date' : 'Date'}</th>
                <th>{lang === 'fr' ? 'Utilisateur' : 'User'}</th>
                <th>{lang === 'fr' ? 'Action' : 'Action'}</th>
                <th>{lang === 'fr' ? 'Entité' : 'Entity'}</th>
              </tr>
            </thead>
            <tbody>
              <AnimatePresence>
                {entries.map((e, i) => (
                  <motion.tr key={e.id}
                    initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0 }}
                    transition={{ delay: i * 0.02, type: 'spring', stiffness: 300, damping: 26 }}>
                    <td className="al-date">{formatDate(e.dateAction)}</td>
                    <td>
                      <div className="um-user-cell__name">{e.utilisateurNom}</div>
                      <div className="um-user-cell__email">{e.utilisateurEmail}</div>
                    </td>
                    <td>
                      <span className="al-action-badge" style={{ '--al-color': colorForAction(e.action) }}>
                        {formatAction(e.action, lang)}
                      </span>
                    </td>
                    <td className="al-entity">
                      {e.entiteConcernee
                        ? `${e.entiteConcernee}${e.entiteId != null ? ` #${e.entiteId}` : ''}`
                        : '—'}
                    </td>
                  </motion.tr>
                ))}
              </AnimatePresence>
            </tbody>
          </table>
        </motion.div>
      )}

      {totalPages > 1 && (
        <motion.div className="al-pagination" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          <button className="um-btn um-btn--icon" disabled={page === 1} onClick={() => setPage(p => p - 1)}>
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="al-pagination__label">
            {lang === 'fr' ? `Page ${page} / ${totalPages}` : `Page ${page} of ${totalPages}`}
          </span>
          <button className="um-btn um-btn--icon" disabled={page === totalPages} onClick={() => setPage(p => p + 1)}>
            <ChevronRight className="w-4 h-4" />
          </button>
        </motion.div>
      )}
    </div>
  );
}
