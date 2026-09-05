import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence, useReducedMotion } from 'framer-motion';
import { User, Mail, Shield, Phone, Calendar, Building2, Camera, KeyRound, Check, X, Pencil, BadgeCheck, Clock } from 'lucide-react';
import { getMe, updateMe, changePassword } from '../services/userService';
import { uploadAvatar, resolvePhotoUrl } from '../services/uploadService';
import { useAuth } from '../context/AuthContext';
import { useLanguage } from '../context/LanguageContext';
import './Profile.css';

const ROLE_LABELS = {
  ADMINISTRATEUR: { fr: 'Administrateur', en: 'Administrator' },
  RESPONSABLE_PEDAGOGIQUE: { fr: 'Resp. Pédagogique', en: 'Academic Coordinator' },
};

export default function Profile() {
  const { updateUser } = useAuth();
  const { lang } = useLanguage();

  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [toast, setToast] = useState(null);

  const [form, setForm] = useState({});
  const [photoPreview, setPhotoPreview] = useState(null);

  // Change password section
  const [pwOpen, setPwOpen] = useState(false);
  const [pw, setPw] = useState({ old: '', next: '', confirm: '' });
  const [pwSaving, setPwSaving] = useState(false);
  const [pwError, setPwError] = useState('');

  const showToast = useCallback((message, type = 'success') => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 3500);
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getMe();
      setProfile(data);
      setForm({
        nom: data.nom,
        discipline: data.discipline ?? '',
        departement: data.departement ?? '',
        telephone: data.telephone ?? '',
        dateNaissance: data.dateNaissance ? data.dateNaissance.slice(0, 10) : '',
        photoUrl: data.photoUrl ?? '',
      });
      setPhotoPreview(resolvePhotoUrl(data.photoUrl));
    } catch {
      showToast(lang === 'fr' ? 'Impossible de charger le profil.' : 'Unable to load profile.', 'error');
    } finally {
      setLoading(false);
    }
  }, [lang, showToast]);

  // eslint-disable-next-line react-hooks/set-state-in-effect -- initial data fetch on mount
  useEffect(() => { load(); }, [load]);

  async function handlePhotoChange(e) {
    const file = e.target.files?.[0];
    if (!file) return;
    setPhotoPreview(URL.createObjectURL(file));
    setUploadingPhoto(true);
    try {
      const url = await uploadAvatar(file);
      setForm(f => ({ ...f, photoUrl: url }));
    } catch {
      showToast(lang === 'fr' ? 'Échec du téléversement.' : 'Upload failed.', 'error');
      setPhotoPreview(profile?.photoUrl || null);
    } finally {
      setUploadingPhoto(false);
    }
  }

  async function handleSave() {
    setSaving(true);
    try {
      await updateMe({
        nom: form.nom,
        discipline: form.discipline || null,
        departement: form.departement || null,
        telephone: form.telephone || null,
        dateNaissance: form.dateNaissance || null,
        photoUrl: form.photoUrl || null,
      });
      showToast(lang === 'fr' ? 'Profil mis à jour.' : 'Profile updated.');
      setEditing(false);
      updateUser({ nom: form.nom, photoUrl: form.photoUrl || null });
      load();
    } catch {
      showToast(lang === 'fr' ? 'Erreur lors de la sauvegarde.' : 'Save failed.', 'error');
    } finally {
      setSaving(false);
    }
  }

  function handleCancel() {
    setEditing(false);
    setForm({
      nom: profile.nom,
      discipline: profile.discipline ?? '',
      departement: profile.departement ?? '',
      telephone: profile.telephone ?? '',
      dateNaissance: profile.dateNaissance ? profile.dateNaissance.slice(0, 10) : '',
      photoUrl: profile.photoUrl ?? '',
    });
    setPhotoPreview(resolvePhotoUrl(profile.photoUrl));
  }

  async function handleChangePassword(e) {
    e.preventDefault();
    setPwError('');
    if (pw.next !== pw.confirm) {
      setPwError(lang === 'fr' ? 'Les mots de passe ne correspondent pas.' : 'Passwords do not match.');
      return;
    }
    setPwSaving(true);
    try {
      await changePassword({ oldPassword: pw.old, newPassword: pw.next });
      showToast(lang === 'fr' ? 'Mot de passe modifié.' : 'Password changed.');
      setPwOpen(false);
      setPw({ old: '', next: '', confirm: '' });
    } catch (err) {
      setPwError(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'));
    } finally {
      setPwSaving(false);
    }
  }

  const reduced = useReducedMotion();
  const initials = profile?.nom
    ? profile.nom.split(' ').map(x => x[0]).join('').toUpperCase().slice(0, 2)
    : '?';

  const roleLabel = profile ? (ROLE_LABELS[profile.role]?.[lang] ?? profile.role) : '';

  if (loading) {
    return (
      <div className="profile-loading">
        <div className="profile-loading__spinner" />
      </div>
    );
  }

  return (
    <div className="profile">
      {/* Toast */}
      <AnimatePresence>
        {toast && (
          <motion.div
            className={`profile-toast profile-toast--${toast.type}`}
            initial={{ opacity: 0, y: -16 }} animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -16 }}
            transition={{ type: 'spring', stiffness: 320, damping: 28 }}
          >
            {toast.type === 'success' ? <Check className="profile-toast__icon" /> : <X className="profile-toast__icon" />}
            {toast.message}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Header card */}
      <motion.div className="profile-card profile-card--header"
        initial={reduced ? false : { opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22 }}>

        {/* Avatar */}
        <div className="profile-avatar-wrap">
          <div className="profile-avatar">
            {(photoPreview || profile?.photoUrl) ? (
              <img
                src={photoPreview || resolvePhotoUrl(profile.photoUrl)}
                alt={profile.nom}
                className="profile-avatar__img"
                onError={e => {
                  e.currentTarget.style.display = 'none';
                  e.currentTarget.parentElement.querySelector('.profile-avatar__initials')?.style?.setProperty('display', 'flex');
                }}
              />
            ) : null}
            {(!photoPreview && !profile?.photoUrl) && (
              <span className="profile-avatar__initials">{initials}</span>
            )}
            {editing && (
              <label className="profile-avatar__upload">
                {uploadingPhoto
                  ? <div className="profile-avatar__upload-spinner" />
                  : <Camera className="profile-avatar__upload-icon" />}
                <input type="file" accept="image/png,image/jpeg,image/webp" onChange={handlePhotoChange} disabled={uploadingPhoto} />
              </label>
            )}
          </div>
        </div>

        {/* Name + role */}
        <div className="profile-header-info">
          {editing ? (
            <input
              className="profile-name-input"
              value={form.nom}
              onChange={e => setForm(f => ({ ...f, nom: e.target.value }))}
              placeholder={lang === 'fr' ? 'Nom complet' : 'Full name'}
            />
          ) : (
            <h1 className="profile-name">{profile?.nom}</h1>
          )}
          <div className="profile-role-badge">{roleLabel}</div>
          <div className="profile-email">
            <Mail className="profile-field__icon" />
            {profile?.email}
          </div>
        </div>

        {/* Edit / Save / Cancel buttons */}
        <div className="profile-header-actions">
          {!editing ? (
            <button className="profile-btn profile-btn--edit" onClick={() => setEditing(true)}>
              <Pencil className="w-4 h-4" />
              {lang === 'fr' ? 'Modifier' : 'Edit'}
            </button>
          ) : (
            <>
              <button className="profile-btn profile-btn--save" onClick={handleSave} disabled={saving}>
                {saving ? <div className="profile-btn__spinner" /> : <Check className="w-4 h-4" />}
                {lang === 'fr' ? 'Enregistrer' : 'Save'}
              </button>
              <button className="profile-btn profile-btn--cancel" onClick={handleCancel}>
                <X className="w-4 h-4" />
                {lang === 'fr' ? 'Annuler' : 'Cancel'}
              </button>
            </>
          )}
        </div>
      </motion.div>

      {/* Details card */}
      <motion.div className="profile-card"
        initial={reduced ? false : { opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22, delay: 0.07 }}>

        <h2 className="profile-card__title">
          <User className="profile-card__title-icon" />
          {lang === 'fr' ? 'Informations' : 'Information'}
        </h2>

        <div className="profile-fields">
          <div className="profile-field">
            <span className="profile-field__label">
              <Shield className="profile-field__icon" />
              {lang === 'fr' ? 'Rôle' : 'Role'}
            </span>
            <span className="profile-field__value">{roleLabel}</span>
          </div>

          {(profile?.role === 'RESPONSABLE_PEDAGOGIQUE' || editing) && (
            <div className="profile-field">
              <span className="profile-field__label">
                <Building2 className="profile-field__icon" />
                {lang === 'fr' ? 'Département' : 'Department'}
              </span>
              {editing ? (
                <input className="profile-field__input"
                  value={form.departement}
                  onChange={e => setForm(f => ({ ...f, departement: e.target.value }))}
                  placeholder={lang === 'fr' ? 'Votre département' : 'Your department'} />
              ) : (
                <span className="profile-field__value">{profile?.departement || '—'}</span>
              )}
            </div>
          )}

          <div className="profile-field">
            <span className="profile-field__label">
              <Phone className="profile-field__icon" />
              {lang === 'fr' ? 'Téléphone' : 'Phone'}
            </span>
            {editing ? (
              <input className="profile-field__input" type="tel"
                value={form.telephone}
                onChange={e => setForm(f => ({ ...f, telephone: e.target.value }))}
                placeholder={lang === 'fr' ? 'Numéro de téléphone' : 'Phone number'} />
            ) : (
              <span className="profile-field__value">{profile?.telephone || '—'}</span>
            )}
          </div>

          <div className="profile-field">
            <span className="profile-field__label">
              <Calendar className="profile-field__icon" />
              {lang === 'fr' ? 'Date de naissance' : 'Date of birth'}
            </span>
            {editing ? (
              <input className="profile-field__input" type="date"
                value={form.dateNaissance}
                onChange={e => setForm(f => ({ ...f, dateNaissance: e.target.value }))} />
            ) : (
              <span className="profile-field__value">
                {profile?.dateNaissance
                  ? new Date(profile.dateNaissance).toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US', { day: '2-digit', month: 'long', year: 'numeric' })
                  : '—'}
              </span>
            )}
          </div>

          <div className="profile-field">
            <span className="profile-field__label">
              <BadgeCheck className="profile-field__icon" />
              {lang === 'fr' ? 'Membre depuis' : 'Member since'}
            </span>
            <span className="profile-field__value">
              {profile?.dateCreation
                ? new Date(profile.dateCreation).toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US', { day: '2-digit', month: 'long', year: 'numeric' })
                : '—'}
            </span>
          </div>

          {profile?.derniereConnexion && (
            <div className="profile-field">
              <span className="profile-field__label">
                <Clock className="profile-field__icon" />
                {lang === 'fr' ? 'Dernière connexion' : 'Last login'}
              </span>
              <span className="profile-field__value">
                {new Date(profile.derniereConnexion).toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US', { day: '2-digit', month: 'long', year: 'numeric' })}
              </span>
            </div>
          )}
        </div>
      </motion.div>

      {/* Change password card */}
      <motion.div className="profile-card"
        initial={reduced ? false : { opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22, delay: 0.14 }}>

        <button className="profile-pw-toggle" onClick={() => { setPwOpen(v => !v); setPwError(''); }}>
          <span className="profile-card__title">
            <KeyRound className="profile-card__title-icon" />
            {lang === 'fr' ? 'Changer le mot de passe' : 'Change password'}
          </span>
          <span className="profile-pw-toggle__chevron" style={{ transform: pwOpen ? 'rotate(180deg)' : 'rotate(0deg)' }}>▾</span>
        </button>

        <AnimatePresence>
          {pwOpen && (
            <motion.form onSubmit={handleChangePassword}
              initial={{ opacity: 0, height: 0 }} animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              transition={{ type: 'spring', stiffness: 300, damping: 28 }}
              className="profile-pw-form">

              {pwError && <div className="profile-pw-error">{pwError}</div>}

              <div className="profile-pw-fields">
                <div className="profile-field-group">
                  <label>{lang === 'fr' ? 'Mot de passe actuel' : 'Current password'}</label>
                  <input type="password" className="profile-field__input" required
                    value={pw.old} onChange={e => setPw(p => ({ ...p, old: e.target.value }))} />
                </div>
                <div className="profile-field-group">
                  <label>{lang === 'fr' ? 'Nouveau mot de passe' : 'New password'}</label>
                  <input type="password" className="profile-field__input" required minLength={8}
                    value={pw.next} onChange={e => setPw(p => ({ ...p, next: e.target.value }))} />
                </div>
                <div className="profile-field-group">
                  <label>{lang === 'fr' ? 'Confirmer le mot de passe' : 'Confirm password'}</label>
                  <input type="password" className="profile-field__input" required
                    value={pw.confirm} onChange={e => setPw(p => ({ ...p, confirm: e.target.value }))} />
                </div>
              </div>

              <button type="submit" className="profile-btn profile-btn--save" disabled={pwSaving}>
                {pwSaving ? <div className="profile-btn__spinner" /> : <Check className="w-4 h-4" />}
                {lang === 'fr' ? 'Changer le mot de passe' : 'Change password'}
              </button>
            </motion.form>
          )}
        </AnimatePresence>
      </motion.div>
    </div>
  );
}
