import { useState } from 'react';
import { useNavigate, useSearchParams, Link } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Lock, Eye, EyeOff, AlertCircle } from 'lucide-react';
import { resetPassword } from '../services/authService';
import { useLanguage } from '../context/LanguageContext';
import AuthControls from '../components/AuthControls';
import './AuthPages.css';

const stagger = { animate: { transition: { staggerChildren: 0.07, delayChildren: 0.15 } } };
const fadeUp = {
  initial: { opacity: 0, y: 18 },
  animate: { opacity: 1, y: 0, transition: { type: 'spring', stiffness: 280, damping: 22 } },
};

function ResetPassword() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [done, setDone] = useState(false);
  const navigate = useNavigate();
  const { t } = useLanguage();

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');
    if (newPassword !== confirmPassword) {
      setError(t('resetPasswordMismatch'));
      return;
    }
    setLoading(true);
    try {
      await resetPassword(token, newPassword);
      setDone(true);
      setTimeout(() => navigate('/login'), 2500);
    } catch (err) {
      setError(err.response?.data?.message || t('resetError'));
    } finally {
      setLoading(false);
    }
  }

  const logoBlock = (
    <motion.div className="auth-card__logo" variants={fadeUp}>
      <div className="auth-card__logo-wrap">
        <img src="/logo.png" alt="Plateforme Formation" className="auth-card__logo-img auth-logo-light" />
        <img src="/logo-dark.png" alt="Plateforme Formation" className="auth-card__logo-img auth-logo-dark" />
      </div>
    </motion.div>
  );

  if (!token) {
    return (
      <div className="auth-page">
        <AuthControls />
        <motion.div
          className="auth-card"
          initial={{ opacity: 0, y: 24, scale: 0.98 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          transition={{ type: 'spring', stiffness: 240, damping: 22, delay: 0.05 }}
        >
          <motion.div variants={stagger} initial="initial" animate="animate">
            {logoBlock}
            <motion.h1 className="auth-card__title" variants={fadeUp}>{t('resetInvalidTitle')}</motion.h1>
            <motion.p className="auth-card__subtitle" variants={fadeUp}>{t('resetInvalidSubtitle')}</motion.p>
            <motion.p className="auth-card__footer" variants={fadeUp}>
              <Link to="/forgot-password">{t('resetNewLink')}</Link>
            </motion.p>
          </motion.div>
        </motion.div>
      </div>
    );
  }

  return (
    <div className="auth-page">
      <AuthControls />
      <motion.div
        className="auth-card"
        initial={{ opacity: 0, y: 24, scale: 0.98 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ type: 'spring', stiffness: 240, damping: 22, delay: 0.05 }}
      >
        <motion.div variants={stagger} initial="initial" animate="animate">
          {logoBlock}
          <motion.h1 className="auth-card__title" variants={fadeUp}>{t('resetTitle')}</motion.h1>

          {done ? (
            <motion.p className="auth-card__subtitle" variants={fadeUp}>{t('resetSuccess')}</motion.p>
          ) : (
            <>
              <motion.p className="auth-card__subtitle" variants={fadeUp}>{t('resetSubtitle')}</motion.p>

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

              <motion.form className="auth-form" onSubmit={handleSubmit} variants={fadeUp}>
                <div className="auth-input auth-input--toggleable">
                  <Lock className="auth-input__icon" />
                  <input
                    type={showNew ? 'text' : 'password'}
                    id="rp-new"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder=" "
                    minLength={8}
                    required
                  />
                  <label htmlFor="rp-new">{t('resetNewPassword')}</label>
                  <button type="button" className="auth-input__toggle" onClick={() => setShowNew((v) => !v)} tabIndex={-1}>
                    {showNew ? <EyeOff size={17} /> : <Eye size={17} />}
                  </button>
                  <div className="auth-input__border" />
                </div>

                <div className="auth-input auth-input--toggleable">
                  <Lock className="auth-input__icon" />
                  <input
                    type={showConfirm ? 'text' : 'password'}
                    id="rp-confirm"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    placeholder=" "
                    minLength={8}
                    required
                  />
                  <label htmlFor="rp-confirm">{t('resetConfirmPassword')}</label>
                  <button type="button" className="auth-input__toggle" onClick={() => setShowConfirm((v) => !v)} tabIndex={-1}>
                    {showConfirm ? <EyeOff size={17} /> : <Eye size={17} />}
                  </button>
                  <div className="auth-input__border" />
                </div>

                <motion.button
                  type="submit"
                  className="auth-form__submit"
                  disabled={loading}
                  variants={fadeUp}
                  whileHover={{ scale: 1.015, boxShadow: '0 8px 30px rgba(162, 5, 19, 0.35)' }}
                  whileTap={{ scale: 0.985 }}
                >
                  {loading && <span className="auth-form__spinner" />}
                  <span>{loading ? t('resetSubmitting') : t('resetSubmit')}</span>
                </motion.button>
              </motion.form>
            </>
          )}
        </motion.div>
      </motion.div>
    </div>
  );
}

export default ResetPassword;
