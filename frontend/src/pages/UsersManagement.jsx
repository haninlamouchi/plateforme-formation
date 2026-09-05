import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Search, X, Check, UserX, UserCheck, Pencil, Users } from 'lucide-react';
import { getAllUsers, updateUser, deactivateUser, reactivateUser } from '../services/userService';
import { resolvePhotoUrl } from '../services/uploadService';
import { useAuth } from '../context/AuthContext';
import { useLanguage } from '../context/LanguageContext';
import './UsersManagement.css';

const ROLE_OPTS = ['', 'ADMINISTRATEUR', 'RESPONSABLE_PEDAGOGIQUE'];
const STATUT_OPTS = ['', 'VALIDE', 'EN_ATTENTE', 'REFUSE'];

const ROLE_LABELS = {
  ADMINISTRATEUR: { fr: 'Admin', en: 'Admin' },
  RESPONSABLE_PEDAGOGIQUE: { fr: 'Resp. Péd.', en: 'Coordinator' },
};

const STATUS_LABELS = {
  VALIDE: { fr: 'Validé', en: 'Validated' },
  EN_ATTENTE: { fr: 'En attente', en: 'Pending' },
  REFUSE: { fr: 'Refusé', en: 'Rejected' },
};

export default function UsersManagement() {
  const { user: authUser } = useAuth();
  const { lang } = useLanguage();
  const isAdmin = authUser?.role === 'ADMINISTRATEUR';

  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [statutFilter, setStatutFilter] = useState('');
  const [actionId, setActionId] = useState(null);
  const [editUser, setEditUser] = useState(null);
  const [editForm, setEditForm] = useState({ nom: '', role: '', actif: true });
  const [editSaving, setEditSaving] = useState(false);
  const [toast, setToast] = useState(null);

  const showToast = useCallback((msg, type = 'success') => {
    setToast({ msg, type });
    setTimeout(() => setToast(null), 3000);
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const params = {};
      if (search) params.search = search;
      if (roleFilter) params.role = roleFilter;
      if (statutFilter) params.statut = statutFilter;
      setUsers(await getAllUsers(params));
    } catch {
      showToast(lang === 'fr' ? 'Impossible de charger les utilisateurs.' : 'Unable to load users.', 'error');
    } finally {
      setLoading(false);
    }
  }, [search, roleFilter, statutFilter, lang, showToast]);

  useEffect(() => {
    const t = setTimeout(() => load(), search ? 300 : 0);
    return () => clearTimeout(t);
  }, [load, search]);

  async function handleToggleActive(user) {
    setActionId(user.id);
    try {
      if (user.actif) {
        await deactivateUser(user.id);
        showToast(lang === 'fr' ? 'Utilisateur désactivé.' : 'User deactivated.');
      } else {
        await reactivateUser(user.id);
        showToast(lang === 'fr' ? 'Utilisateur réactivé.' : 'User reactivated.');
      }
      load();
    } catch {
      showToast(lang === 'fr' ? 'Erreur.' : 'Error.', 'error');
    } finally {
      setActionId(null);
    }
  }

  function openEdit(user) {
    setEditUser(user);
    setEditForm({ nom: user.nom, role: user.role, actif: user.actif });
  }

  async function handleEditSave(e) {
    e.preventDefault();
    setEditSaving(true);
    try {
      await updateUser(editUser.id, editForm);
      showToast(lang === 'fr' ? 'Utilisateur mis à jour.' : 'User updated.');
      setEditUser(null);
      load();
    } catch {
      showToast(lang === 'fr' ? 'Erreur lors de la mise à jour.' : 'Update failed.', 'error');
    } finally {
      setEditSaving(false);
    }
  }

  const getInitials = (nom) => nom?.split(' ').map(x => x[0]).join('').toUpperCase().slice(0, 2) || '?';
  const roleLabel = (role) => ROLE_LABELS[role]?.[lang] ?? role;
  const statusLabel = (s) => STATUS_LABELS[s]?.[lang] ?? s;

  return (
    <div className="um">
      {/* Toast */}
      <AnimatePresence>
        {toast && (
          <motion.div className={`um-toast um-toast--${toast.type}`}
            initial={{ opacity: 0, y: -14 }} animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -14 }}>
            {toast.msg}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Edit modal */}
      <AnimatePresence>
        {editUser && isAdmin && (
          <motion.div className="um-overlay"
            initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            onClick={() => setEditUser(null)}>
            <motion.div className="um-modal"
              initial={{ opacity: 0, scale: 0.95, y: 16 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 16 }}
              transition={{ type: 'spring', stiffness: 320, damping: 28 }}
              onClick={e => e.stopPropagation()}>

              <div className="um-modal__header">
                <h2 className="um-modal__title">
                  {lang === 'fr' ? 'Modifier l\'utilisateur' : 'Edit user'}
                </h2>
                <button className="um-modal__close" onClick={() => setEditUser(null)}>
                  <X className="w-5 h-5" />
                </button>
              </div>

              <form onSubmit={handleEditSave} className="um-modal__form">
                <div className="um-modal__field">
                  <label>{lang === 'fr' ? 'Nom complet' : 'Full name'}</label>
                  <input className="um-modal__input"
                    value={editForm.nom}
                    onChange={e => setEditForm(f => ({ ...f, nom: e.target.value }))}
                    required />
                </div>

                <div className="um-modal__field">
                  <label>{lang === 'fr' ? 'Rôle' : 'Role'}</label>
                  <select className="um-modal__input"
                    value={editForm.role}
                    onChange={e => setEditForm(f => ({ ...f, role: e.target.value }))}>
                    <option value="RESPONSABLE_PEDAGOGIQUE">{lang === 'fr' ? 'Resp. Pédagogique' : 'Academic Coordinator'}</option>
                    <option value="ADMINISTRATEUR">Admin</option>
                  </select>
                </div>

                <label className="um-modal__checkbox">
                  <input type="checkbox" checked={editForm.actif}
                    onChange={e => setEditForm(f => ({ ...f, actif: e.target.checked }))} />
                  <span>{lang === 'fr' ? 'Compte actif' : 'Active account'}</span>
                </label>

                <div className="um-modal__actions">
                  <button type="button" className="um-btn um-btn--ghost" onClick={() => setEditUser(null)}>
                    {lang === 'fr' ? 'Annuler' : 'Cancel'}
                  </button>
                  <button type="submit" className="um-btn um-btn--primary" disabled={editSaving}>
                    {editSaving
                      ? <div className="um-spinner" />
                      : <Check className="w-4 h-4" />}
                    {lang === 'fr' ? 'Enregistrer' : 'Save'}
                  </button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Page header */}
      <motion.div className="um-header"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22 }}>
        <div className="um-header__info">
          <Users className="um-header__icon" />
          <div>
            <h1 className="um-header__title">
              {lang === 'fr' ? 'Utilisateurs' : 'Users'}
            </h1>
            <p className="um-header__sub">
              {users.length} {lang === 'fr' ? 'membre(s)' : 'member(s)'}
            </p>
          </div>
        </div>
      </motion.div>

      {/* Filters */}
      <motion.div className="um-filters"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22, delay: 0.06 }}>

        <div className="um-search">
          <Search className="um-search__icon" />
          <input
            className="um-search__input"
            placeholder={lang === 'fr' ? 'Rechercher...' : 'Search...'}
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
          {search && (
            <button className="um-search__clear" onClick={() => setSearch('')}>
              <X className="w-4 h-4" />
            </button>
          )}
        </div>

        <select className="um-select" value={roleFilter} onChange={e => setRoleFilter(e.target.value)}>
          <option value="">{lang === 'fr' ? 'Tous les rôles' : 'All roles'}</option>
          {ROLE_OPTS.slice(1).map(r => (
            <option key={r} value={r}>{roleLabel(r)}</option>
          ))}
        </select>

        {isAdmin && (
          <select className="um-select" value={statutFilter} onChange={e => setStatutFilter(e.target.value)}>
            <option value="">{lang === 'fr' ? 'Tous les statuts' : 'All statuses'}</option>
            {STATUT_OPTS.slice(1).map(s => (
              <option key={s} value={s}>{statusLabel(s)}</option>
            ))}
          </select>
        )}
      </motion.div>

      {/* Table */}
      {loading ? (
        <div className="um-loading">
          <div className="um-loading__spinner" />
          <span>{lang === 'fr' ? 'Chargement...' : 'Loading...'}</span>
        </div>
      ) : users.length === 0 ? (
        <motion.div className="um-empty"
          initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          <Users className="um-empty__icon" />
          <p>{lang === 'fr' ? 'Aucun utilisateur trouvé.' : 'No users found.'}</p>
        </motion.div>
      ) : (
        <motion.div className="um-table-wrap"
          initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
          transition={{ type: 'spring', stiffness: 260, damping: 22, delay: 0.1 }}>
          <table className="um-table">
            <thead>
              <tr>
                <th>{lang === 'fr' ? 'Utilisateur' : 'User'}</th>
                <th>{lang === 'fr' ? 'Rôle' : 'Role'}</th>
                {isAdmin && <th>{lang === 'fr' ? 'Statut' : 'Status'}</th>}
                {isAdmin && <th>{lang === 'fr' ? 'Actif' : 'Active'}</th>}
                {isAdmin && <th style={{ textAlign: 'right' }}>{lang === 'fr' ? 'Actions' : 'Actions'}</th>}
              </tr>
            </thead>
            <tbody>
              <AnimatePresence>
                {users.map((u, i) => (
                  <motion.tr key={u.id}
                    initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0 }}
                    transition={{ delay: i * 0.03, type: 'spring', stiffness: 300, damping: 26 }}
                    layout>
                    <td>
                      <div className="um-user-cell">
                        <div className="um-user-cell__avatar" style={{ backgroundImage: u.photoUrl ? `url(${resolvePhotoUrl(u.photoUrl)})` : undefined }}>
                          {!u.photoUrl && <span>{getInitials(u.nom)}</span>}
                        </div>
                        <div>
                          <div className="um-user-cell__name">{u.nom}</div>
                          <div className="um-user-cell__email">{u.email}</div>
                          {(u.discipline || u.departement) && (
                            <div className="um-user-cell__meta">{u.discipline || u.departement}</div>
                          )}
                        </div>
                      </div>
                    </td>
                    <td>
                      <span className={`um-badge um-badge--role um-badge--${u.role.toLowerCase()}`}>
                        {roleLabel(u.role)}
                      </span>
                    </td>
                    {isAdmin && (
                      <td>
                        <span className={`um-badge um-badge--statut um-badge--${u.statutCompte.toLowerCase()}`}>
                          {statusLabel(u.statutCompte)}
                        </span>
                      </td>
                    )}
                    {isAdmin && (
                      <td>
                        <button
                          className={`um-toggle ${u.actif ? 'um-toggle--on' : 'um-toggle--off'}`}
                          onClick={() => handleToggleActive(u)}
                          disabled={actionId === u.id}
                          title={u.actif ? (lang === 'fr' ? 'Désactiver' : 'Deactivate') : (lang === 'fr' ? 'Réactiver' : 'Reactivate')}
                        >
                          {actionId === u.id
                            ? <div className="um-spinner um-spinner--sm" />
                            : u.actif
                              ? <UserCheck className="w-4 h-4" />
                              : <UserX className="w-4 h-4" />}
                        </button>
                      </td>
                    )}
                    {isAdmin && (
                      <td style={{ textAlign: 'right' }}>
                        <button className="um-btn um-btn--icon" onClick={() => openEdit(u)} title={lang === 'fr' ? 'Modifier' : 'Edit'}>
                          <Pencil className="w-4 h-4" />
                        </button>
                      </td>
                    )}
                  </motion.tr>
                ))}
              </AnimatePresence>
            </tbody>
          </table>
        </motion.div>
      )}
    </div>
  );
}
