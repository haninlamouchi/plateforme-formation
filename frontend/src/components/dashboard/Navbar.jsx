import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Globe, Sun, Moon, Menu } from 'lucide-react';
import { useTheme } from '../../context/ThemeContext';
import { useLanguage } from '../../context/LanguageContext';
import { useAuth } from '../../context/AuthContext';
import { resolvePhotoUrl } from '../../services/uploadService';
import NotificationBell from './NotificationBell';

export default function Navbar({ onMenuClick }) {
  const { theme, toggleTheme } = useTheme();
  const { lang, toggleLang } = useLanguage();
  const { user } = useAuth();
  const initials = user?.nom ? user.nom.split(' ').map(x => x[0]).join('').toUpperCase().slice(0, 2) : '?';
  const photo = resolvePhotoUrl(user?.photoUrl);

  return (
    <header className="sticky top-0 z-30 flex items-center gap-4 px-6 lg:px-8 py-3 bg-[var(--color-surface-0)]/80 backdrop-blur-xl border-b border-[var(--color-border-subtle)]">
      {/* Mobile menu */}
      <button
        onClick={onMenuClick}
        className="lg:hidden p-2 rounded-xl text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-2)] transition-colors"
      >
        <Menu className="w-5 h-5" />
      </button>

      <div className="flex-1" />

      {/* Language toggle */}
      <motion.button
        onClick={toggleLang}
        whileHover={{ scale: 1.05 }}
        whileTap={{ scale: 0.95 }}
        className="flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[12px] font-semibold tracking-wide text-[var(--color-ink-muted)] bg-[var(--color-surface-2)] hover:bg-[var(--color-surface-3)] transition-colors"
        title={lang === 'fr' ? 'Switch to English' : 'Passer en français'}
      >
        <Globe className="w-3.5 h-3.5" />
        {lang === 'fr' ? 'FR' : 'EN'}
      </motion.button>

      {/* Theme toggle */}
      <motion.button
        onClick={toggleTheme}
        whileHover={{ scale: 1.05 }}
        whileTap={{ scale: 0.95 }}
        className="p-2 rounded-xl text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-2)] hover:text-[var(--color-ink)] transition-colors"
        title={theme === 'dark' ? 'Light mode' : 'Dark mode'}
      >
        {theme === 'dark' ? <Sun className="w-[18px] h-[18px]" /> : <Moon className="w-[18px] h-[18px]" />}
      </motion.button>

      {/* Notifications */}
      <NotificationBell />

      {/* Avatar */}
      <Link
        to="/profile"
        className="w-9 h-9 rounded-xl bg-[var(--color-primary)] overflow-hidden flex items-center justify-center text-[12px] font-bold hover:opacity-80 transition-opacity shrink-0"
        title={user?.nom}
      >
        {photo ? (
          <img
            src={photo}
            alt={user?.nom}
            style={{ width: '100%', height: '100%', objectFit: 'cover' }}
            onError={e => { e.currentTarget.style.display = 'none'; if (e.currentTarget.nextSibling) e.currentTarget.nextSibling.style.display = 'flex'; }}
          />
        ) : null}
        <span
          className="text-white flex items-center justify-center w-full h-full"
          style={{ display: photo ? 'none' : 'flex' }}
        >
          {initials}
        </span>
      </Link>
    </header>
  );
}
