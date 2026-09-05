import { useEffect, useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { AlertCircle } from 'lucide-react';
import { fetchPendingUsers, rejectUser, validateUser } from '../services/adminService';
import { useLanguage } from '../context/LanguageContext';
import './AdminValidations.css';

function AdminValidations() {
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [actionId, setActionId] = useState(null);
  const { t, lang } = useLanguage();

  const loadUsers = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setUsers(await fetchPendingUsers());
    } catch (err) {
      setError(err.response?.data?.message || t('adminLoadError'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    let cancelled = false;
    // eslint-disable-next-line react-hooks/set-state-in-effect -- initial data fetch on mount
    if (!cancelled) loadUsers();
    return () => { cancelled = true; };
  }, [loadUsers]);

  const handleAction = useCallback(async (userId, action) => {
    setActionId(userId);
    setError('');
    try {
      action === 'validate' ? await validateUser(userId) : await rejectUser(userId);
      await loadUsers();
    } catch (err) {
      setError(err.response?.data?.message || t('adminActionError'));
    } finally {
      setActionId(null);
    }
  }, [loadUsers, t]);

  const getInitials = (name) => name ? name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2) : '?';

  const getRoleBadgeClass = (role) => {
    if (role === 'RESPONSABLE_PEDAGOGIQUE') return 'admin-role-badge--responsable';
    return 'admin-role-badge--admin';
  };

  const getRoleLabel = (role) => ({
    RESPONSABLE_PEDAGOGIQUE: t('adminRoleResp'),
    ADMINISTRATEUR: t('adminRoleAdmin'),
  }[role] || role);

  const formatDate = (dateStr) => {
    const d = new Date(dateStr);
    const locale = lang === 'fr' ? 'fr-FR' : 'en-US';
    return {
      date: d.toLocaleDateString(locale, { day: '2-digit', month: 'short', year: 'numeric' }),
      time: d.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' }),
    };
  };

  return (
    <div className="admin">
      <motion.div className="admin-header" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.1, type: 'spring', stiffness: 260, damping: 22 }}>
        <h1>{t('adminHeaderTitle')}</h1>
        <p>{t('adminHeaderSubtitle')}</p>
      </motion.div>

      <AnimatePresence>
        {error && (
          <motion.div className="admin-error"
            initial={{ opacity: 0, height: 0 }} animate={{ opacity: 1, height: 'auto' }} exit={{ opacity: 0, height: 0 }}
            transition={{ type: 'spring', stiffness: 300, damping: 25 }}>
            <AlertCircle size={16} className="shrink-0 mt-0.5" />
            {error}
          </motion.div>
        )}
      </AnimatePresence>

      {loading ? (
        <motion.div className="admin-loading" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          <div className="admin-loading__spinner" />
          <span className="admin-loading__text">{t('adminLoading')}</span>
        </motion.div>
      ) : users.length === 0 ? (
        <motion.div className="admin-empty"
          initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }}
          transition={{ type: 'spring', stiffness: 260, damping: 22 }}>
          <div className="admin-empty__icon">
            <svg viewBox="0 0 24 24"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
          </div>
          <h3 className="admin-empty__title">{t('adminEmptyTitle')}</h3>
          <p className="admin-empty__text">{t('adminEmptyText')}</p>
        </motion.div>
      ) : (
        <motion.div className="admin-table-wrap"
          initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.15, type: 'spring', stiffness: 260, damping: 22 }}>
          <table className="admin-table">
            <thead>
              <tr>
                <th>{t('adminTableUser')}</th>
                <th>{t('adminTableRole')}</th>
                <th>{t('adminTableDate')}</th>
                <th style={{ textAlign: 'right' }}>{t('adminTableActions')}</th>
              </tr>
            </thead>
            <tbody>
              <AnimatePresence>
                {users.map((user, i) => {
                  const dt = formatDate(user.dateCreation);
                  return (
                    <motion.tr key={user.id}
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, x: -20 }}
                      transition={{ delay: i * 0.05, type: 'spring', stiffness: 300, damping: 25 }}
                      layout>
                      <td>
                        <div className="admin-user-cell">
                          <div className="admin-user-cell__avatar">{getInitials(user.nom)}</div>
                          <div className="admin-user-cell__info">
                            <div className="admin-user-cell__name">{user.nom}</div>
                            <div className="admin-user-cell__email">{user.email}</div>
                            {(user.discipline || user.departement) && (
                              <div className="admin-user-cell__meta">{user.discipline || user.departement}</div>
                            )}
                          </div>
                        </div>
                      </td>
                      <td>
                        <span className={`admin-role-badge ${getRoleBadgeClass(user.role)}`}>
                          {getRoleLabel(user.role)}
                        </span>
                      </td>
                      <td>
                        <div className="admin-date-cell">
                          <span className="admin-date-cell__date">{dt.date}</span>
                          <span className="admin-date-cell__time">{dt.time}</span>
                        </div>
                      </td>
                      <td>
                        <div className="admin-actions" style={{ justifyContent: 'flex-end' }}>
                          <motion.button type="button" className="admin-btn admin-btn--validate"
                            onClick={() => handleAction(user.id, 'validate')}
                            disabled={actionId === user.id}
                            whileHover={{ scale: 1.04 }} whileTap={{ scale: 0.96 }}>
                            {actionId === user.id ? <span className="admin-btn__spinner" /> :
                              <svg viewBox="0 0 24 24"><polyline points="20 6 9 17 4 12"/></svg>}
                            {t('adminApprove')}
                          </motion.button>
                          <motion.button type="button" className="admin-btn admin-btn--reject"
                            onClick={() => handleAction(user.id, 'reject')}
                            disabled={actionId === user.id}
                            whileHover={{ scale: 1.04 }} whileTap={{ scale: 0.96 }}>
                            {actionId === user.id ? <span className="admin-btn__spinner" /> :
                              <svg viewBox="0 0 24 24"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg>}
                            {t('adminReject')}
                          </motion.button>
                        </div>
                      </td>
                    </motion.tr>
                  );
                })}
              </AnimatePresence>
            </tbody>
          </table>
        </motion.div>
      )}
    </div>
  );
}

export default AdminValidations;
