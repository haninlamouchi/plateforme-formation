import { motion } from 'framer-motion';
import { Globe, Sun, Moon } from 'lucide-react';
import { useTheme } from '../context/ThemeContext';
import { useLanguage } from '../context/LanguageContext';

export default function AuthControls() {
  const { theme, toggleTheme } = useTheme();
  const { lang, toggleLang } = useLanguage();

  return (
    <div style={{ position: 'fixed', top: 16, right: 16, zIndex: 100, display: 'flex', gap: 8, alignItems: 'center' }}>
      <motion.button
        onClick={toggleLang}
        whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}
        style={{
          display: 'flex', alignItems: 'center', gap: 6,
          padding: '6px 14px', borderRadius: 999,
          fontSize: 11, fontWeight: 700, letterSpacing: '0.08em',
          background: 'var(--glass-bg)', backdropFilter: 'blur(14px)',
          WebkitBackdropFilter: 'blur(14px)',
          border: '1px solid var(--outline-variant)',
          color: 'var(--slate)', cursor: 'pointer',
          boxShadow: '0 2px 8px rgba(0,0,0,0.08)',
        }}
      >
        <Globe size={13} />
        {lang === 'fr' ? 'FR' : 'EN'}
      </motion.button>

      <motion.button
        onClick={toggleTheme}
        whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}
        title={theme === 'dark' ? 'Light mode' : 'Dark mode'}
        style={{
          padding: 8, borderRadius: 12,
          background: 'var(--glass-bg)', backdropFilter: 'blur(14px)',
          WebkitBackdropFilter: 'blur(14px)',
          border: '1px solid var(--outline-variant)',
          color: 'var(--slate)', cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          boxShadow: '0 2px 8px rgba(0,0,0,0.08)',
        }}
      >
        {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
      </motion.button>
    </div>
  );
}
