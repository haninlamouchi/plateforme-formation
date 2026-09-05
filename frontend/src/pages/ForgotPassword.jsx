import { useState } from 'react';
import { Link } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Mail, AlertCircle } from 'lucide-react';
import { requestPasswordReset } from '../services/authService';
import { useLanguage } from '../context/LanguageContext';
import AuthControls from '../components/AuthControls';
import './AuthPages.css';

const stagger = { animate: { transition: { staggerChildren: 0.07, delayChildren: 0.15 } } };
const fadeUp = {
  initial: { opacity: 0, y: 18 },
  animate: { opacity: 1, y: 0, transition: { type: 'spring', stiffness: 280, damping: 22 } },
};

function ForgotPassword() {
  const [email, setEmail] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const { t } = useLanguage();

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await requestPasswordReset(email);
      setSubmitted(true);
    } catch (err) {
      setError(err.response?.data?.message || t('forgotError'));
    } finally {
      setLoading(false);
    }
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

          {/* Logo */}
          <motion.div className="auth-card__logo" variants={fadeUp}>
            <div className="auth-card__logo-wrap">
              <img src="/logo.png" alt="Plateforme Formation" className="auth-card__logo-img auth-logo-light" />
              <img src="/logo-dark.png" alt="Plateforme Formation" className="auth-card__logo-img auth-logo-dark" />
            </div>
          </motion.div>

          <motion.h1 className="auth-card__title" variants={fadeUp}>{t('forgotTitle')}</motion.h1>

          {submitted ? (
            <>
              <motion.p className="auth-card__subtitle" variants={fadeUp}>{t('forgotSuccess')}</motion.p>
              <motion.p className="auth-card__footer" variants={fadeUp}>
                <Link to="/login">{t('forgotBackToLogin')}</Link>
              </motion.p>
            </>
          ) : (
            <>
              <motion.p className="auth-card__subtitle" variants={fadeUp}>{t('forgotSubtitle')}</motion.p>

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
                <div className="auth-input">
                  <Mail className="auth-input__icon" />
                  <input
                    type="email"
                    id="fp-email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder=" "
                    required
                  />
                  <label htmlFor="fp-email">{t('email')}</label>
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
                  <span>{loading ? t('forgotSubmitting') : t('forgotSubmit')}</span>
                </motion.button>
              </motion.form>

              <motion.p className="auth-card__footer" variants={fadeUp}>
                {t('forgotRemembered')} <Link to="/login">{t('forgotSignIn')}</Link>
              </motion.p>
            </>
          )}
        </motion.div>
      </motion.div>
    </div>
  );
}

export default ForgotPassword;
