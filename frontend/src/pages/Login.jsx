import { useState, useCallback } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { GoogleLogin } from '@react-oauth/google';
import { Mail, Lock, Eye, EyeOff, AlertCircle } from 'lucide-react';
import { loginUser, googleLogin } from '../services/authService';
import { useAuth } from '../context/AuthContext';
import { useLanguage } from '../context/LanguageContext';
import AuthControls from '../components/AuthControls';
import './AuthPages.css';

const stagger = { animate: { transition: { staggerChildren: 0.07, delayChildren: 0.1 } } };
const fadeUp = {
  initial: { opacity: 0, y: 16 },
  animate: { opacity: 1, y: 0, transition: { type: 'spring', stiffness: 280, damping: 22 } },
};

function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const { login } = useAuth();
  const navigate = useNavigate();
  const { t } = useLanguage();

  const handleSubmit = useCallback(async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const data = await loginUser(email, password, rememberMe);
      login(data, rememberMe);
      navigate('/dashboard');
    } catch (err) {
      setError(err.response?.data?.message || t('loginError'));
    } finally {
      setLoading(false);
    }
  }, [email, password, rememberMe, login, navigate, t]);

  const handleGoogleSuccess = useCallback(async (credentialResponse) => {
    setError('');
    setLoading(true);
    try {
      const data = await googleLogin(credentialResponse.credential);
      login(data, true);
      navigate('/dashboard');
    } catch (err) {
      setError(err.response?.data?.message || 'Google sign-in failed.');
    } finally {
      setLoading(false);
    }
  }, [login, navigate]);

  return (
    <div className="auth-page">
      <AuthControls />
      <div className="auth-blobs" aria-hidden="true">
        <div className="auth-blob auth-blob--1" />
        <div className="auth-blob auth-blob--2" />
        <div className="auth-blob auth-blob--3" />
      </div>

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

          <motion.h1 className="auth-card__title" variants={fadeUp}>{t('loginTitle')}</motion.h1>
          <motion.p className="auth-card__subtitle" variants={fadeUp}>{t('loginSubtitle')}</motion.p>

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
                id="login-email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder=" "
                required
              />
              <label htmlFor="login-email">{t('email')}</label>
              <div className="auth-input__border" />
            </div>

            <div className="auth-input auth-input--toggleable">
              <Lock className="auth-input__icon" />
              <input
                type={showPassword ? 'text' : 'password'}
                id="login-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder=" "
                required
              />
              <label htmlFor="login-password">{t('password')}</label>
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

            <motion.div className="auth-form__meta-row" variants={fadeUp}>
              <label className="auth-form__checkbox">
                <input type="checkbox" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)} />
                <span className="auth-form__checkbox-mark" />
                <span>{t('loginRememberMe')}</span>
              </label>
              <Link to="/forgot-password" className="auth-form__link">{t('loginForgotPassword')}</Link>
            </motion.div>

            <motion.button
              type="submit"
              className="auth-form__submit"
              disabled={loading}
              variants={fadeUp}
              whileHover={{ scale: 1.015, boxShadow: '0 8px 30px rgba(162, 5, 19, 0.35)' }}
              whileTap={{ scale: 0.985 }}
            >
              <AnimatePresence mode="wait">
                {loading && (
                  <motion.span
                    key="spinner"
                    className="auth-form__spinner"
                    initial={{ opacity: 0, scale: 0.5 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.5 }}
                  />
                )}
              </AnimatePresence>
              <span>{loading ? t('loginSubmitting') : t('loginSubmit')}</span>
            </motion.button>
          </motion.form>

          <motion.div className="auth-form__divider" variants={fadeUp}>
            <span>{t('loginOrWith') || 'or continue with'}</span>
          </motion.div>

          <motion.div className="auth-form__google" variants={fadeUp}>
            <GoogleLogin
              onSuccess={handleGoogleSuccess}
              onError={() => setError('Google sign-in failed.')}
              width="100%"
              theme="outline"
              shape="rectangular"
              text="signin_with"
            />
          </motion.div>

          <motion.p className="auth-card__footer" variants={fadeUp}>
            {t('loginNoAccount')} <Link to="/register">{t('loginRequestAccess')}</Link>
          </motion.p>
        </motion.div>
      </motion.div>
    </div>
  );
}

export default Login;
