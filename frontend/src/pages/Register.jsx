import { useState } from 'react';
import { Link } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import {
  User, Mail, Lock, Eye, EyeOff, Phone, Calendar,
  Building2, UserCog, AlertCircle,
} from 'lucide-react';
import { registerUser } from '../services/authService';
import { uploadAvatar } from '../services/uploadService';
import { useLanguage } from '../context/LanguageContext';
import AuthControls from '../components/AuthControls';
import PasswordStrengthMeter from '../components/PasswordStrengthMeter';
import './AuthPages.css';

const stagger = {
  animate: { transition: { staggerChildren: 0.06, delayChildren: 0.1 } },
};
const fadeUp = {
  initial: { opacity: 0, y: 18 },
  animate: { opacity: 1, y: 0, transition: { type: 'spring', stiffness: 280, damping: 22 } },
};

function Register() {
  const [nom, setNom] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [role, setRole] = useState('');
  const [departement, setDepartement] = useState('');
  const [telephone, setTelephone] = useState('');
  const [dateNaissance, setDateNaissance] = useState('');
  const [photoUrl, setPhotoUrl] = useState('');
  const [photoPreview, setPhotoPreview] = useState(null);
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [error, setError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');
  const [loading, setLoading] = useState(false);
  const { t } = useLanguage();

  function handleRoleChange(e) {
    setRole(e.target.value);
    setDepartement('');
  }

  async function handlePhotoChange(e) {
    const file = e.target.files?.[0];
    if (!file) return;
    setPhotoPreview(URL.createObjectURL(file));
    setError('');
    setPhotoUrl('');
    setUploadingPhoto(true);
    try {
      setPhotoUrl(await uploadAvatar(file));
    } catch (err) {
      setError(err.response?.data?.message || t('registerError'));
      setPhotoUrl('');
      setPhotoPreview(null);
    } finally {
      setUploadingPhoto(false);
    }
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');
    setSuccessMessage('');
    setLoading(true);
    try {
      const data = await registerUser({
        nom, email, password, role,
        discipline: null,
        departement: role === 'RESPONSABLE_PEDAGOGIQUE' ? departement : null,
        telephone: telephone || null,
        dateNaissance: dateNaissance || null,
        photoUrl: photoUrl || null,
      });
      setSuccessMessage(data.message ?? t('registerSuccess'));
    } catch (err) {
      setError(err.response?.data?.message || t('registerError'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="auth-page">
      <AuthControls />
      <div className="auth-blobs" aria-hidden="true">
        <div className="auth-blob auth-blob--1" />
        <div className="auth-blob auth-blob--2" />
        <div className="auth-blob auth-blob--3" />
      </div>

      <motion.div
        className="auth-card auth-card--wide"
        initial={{ opacity: 0, y: 30, scale: 0.97 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ type: 'spring', stiffness: 240, damping: 22, delay: 0.05 }}
      >
        <motion.div variants={stagger} initial="initial" animate="animate">

          {/* Logo */}
          <motion.div className="auth-card__logo" variants={fadeUp}>
            <div className="auth-card__logo-wrap">
              <img src="/logo.png" alt="Plateforme Formation" className="auth-card__logo-img auth-logo-light" />
              <img src="/logo-dark.png" alt="Plateforme Formation" className="auth-card__logo-img auth-logo-dark" />
            </div>
          </motion.div>

          <motion.h1 className="auth-card__title" variants={fadeUp}>{t('registerTitle')}</motion.h1>
          <motion.p className="auth-card__subtitle" variants={fadeUp}>{t('registerSubtitle')}</motion.p>

          <AnimatePresence>
            {error && (
              <motion.div
                className="auth-form__error"
                initial={{ opacity: 0, y: -8 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -8 }}
                transition={{ duration: 0.2 }}
              >
                <AlertCircle size={16} className="shrink-0 mt-0.5" />
                {error}
              </motion.div>
            )}
          </AnimatePresence>

          {successMessage && <div className="auth-form__success">{successMessage}</div>}

          <motion.form className="auth-form" onSubmit={handleSubmit} variants={fadeUp}>

            {/* Avatar upload */}
            <div className="auth-form__avatar">
              <div className="auth-form__avatar-preview">
                {photoPreview
                  ? <img src={photoPreview} alt="" />
                  : <span>{t('registerPhoto')}</span>
                }
              </div>
              <label className="auth-form__avatar-upload">
                <span>{uploadingPhoto ? t('registerUploading') : t('registerChoosePhoto')}</span>
                <input
                  type="file"
                  accept="image/png, image/jpeg, image/webp"
                  onChange={handlePhotoChange}
                  disabled={uploadingPhoto}
                />
              </label>
            </div>

            {/* Full name */}
            <div className="auth-input">
              <User className="auth-input__icon" />
              <input
                type="text"
                id="reg-name"
                value={nom}
                onChange={(e) => setNom(e.target.value)}
                placeholder=" "
                required
              />
              <label htmlFor="reg-name">{t('registerFullName')}</label>
              <div className="auth-input__border" />
            </div>

            {/* Email */}
            <div className="auth-input">
              <Mail className="auth-input__icon" />
              <input
                type="email"
                id="reg-email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder=" "
                required
              />
              <label htmlFor="reg-email">{t('email')}</label>
              <div className="auth-input__border" />
            </div>

            {/* Password + toggle */}
            <div className="auth-input auth-input--toggleable">
              <Lock className="auth-input__icon" />
              <input
                type={showPassword ? 'text' : 'password'}
                id="reg-pass"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder=" "
                minLength={8}
                required
              />
              <label htmlFor="reg-pass">{t('registerPassword')}</label>
              <button
                type="button"
                className="auth-input__toggle"
                onClick={() => setShowPassword((v) => !v)}
                tabIndex={-1}
              >
                {showPassword ? <EyeOff size={17} /> : <Eye size={17} />}
              </button>
              <div className="auth-input__border" />
            </div>
            <PasswordStrengthMeter password={password} />

            {/* Role */}
            <div className="auth-input">
              <UserCog className="auth-input__icon" />
              <select id="reg-role" value={role} onChange={handleRoleChange} required>
                <option value="" disabled />
                <option value="RESPONSABLE_PEDAGOGIQUE">{t('registerCoordinator')}</option>
              </select>
              <label htmlFor="reg-role">{t('registerRole')}</label>
              <div className="auth-input__border" />
            </div>

            {/* Department — Responsable only */}
            <AnimatePresence>
              {role === 'RESPONSABLE_PEDAGOGIQUE' && (
                <motion.div
                  className="auth-input"
                  initial={{ opacity: 0, height: 0 }}
                  animate={{ opacity: 1, height: 'auto' }}
                  exit={{ opacity: 0, height: 0 }}
                  transition={{ type: 'spring', stiffness: 300, damping: 25 }}
                >
                  <Building2 className="auth-input__icon" />
                  <input
                    type="text"
                    id="reg-dept"
                    value={departement}
                    onChange={(e) => setDepartement(e.target.value)}
                    placeholder=" "
                    required
                  />
                  <label htmlFor="reg-dept">{t('registerDepartment')}</label>
                  <div className="auth-input__border" />
                </motion.div>
              )}
            </AnimatePresence>

            {/* Section divider — separates required fields from optional ones */}
            <div className="auth-form__section">
              <span>{t('registerOptional') || 'Optional'}</span>
            </div>

            {/* Phone + DOB */}
            <div className="auth-form__row">
              <div className="auth-input">
                <Phone className="auth-input__icon" />
                <input
                  type="tel"
                  id="reg-phone"
                  value={telephone}
                  onChange={(e) => setTelephone(e.target.value)}
                  placeholder=" "
                />
                <label htmlFor="reg-phone">{t('registerPhone')}</label>
                <div className="auth-input__border" />
              </div>
              <div className="auth-input">
                <Calendar className="auth-input__icon" />
                <input
                  type="date"
                  id="reg-dob"
                  value={dateNaissance}
                  onChange={(e) => setDateNaissance(e.target.value)}
                  placeholder=" "
                />
                <label htmlFor="reg-dob">{t('registerDob')}</label>
                <div className="auth-input__border" />
              </div>
            </div>

            <motion.button
              type="submit"
              className="auth-form__submit"
              disabled={loading || uploadingPhoto}
              variants={fadeUp}
              whileHover={{ scale: 1.015, boxShadow: '0 8px 30px rgba(162, 5, 19, 0.35)' }}
              whileTap={{ scale: 0.985 }}
            >
              {(loading || uploadingPhoto) && <span className="auth-form__spinner" />}
              <span>{loading ? t('registerSubmitting') : t('registerSubmit')}</span>
            </motion.button>
          </motion.form>

          <motion.p className="auth-card__footer" variants={fadeUp}>
            {t('registerHasAccount')} <Link to="/login">{t('registerSignIn')}</Link>
          </motion.p>
        </motion.div>
      </motion.div>
    </div>
  );
}

export default Register;
