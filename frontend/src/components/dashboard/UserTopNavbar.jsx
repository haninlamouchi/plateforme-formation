import { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { motion, AnimatePresence, LayoutGroup } from 'framer-motion';
import {
  LayoutDashboard, BookOpen, FileText,
  Globe, Sun, Moon, LogOut, Menu, X, UserCircle, MessageSquare,
} from 'lucide-react';
import { resolvePhotoUrl } from '../../services/uploadService';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { useTheme } from '../../context/ThemeContext';
import NotificationBell from './NotificationBell';

const NAV_ITEMS = [
  { to: '/dashboard', labelKey: 'dashboardNavDashboard', icon: LayoutDashboard },
  { to: '/formations', labelKey: 'dashboardNavFormations', icon: BookOpen },
  { to: '/documents', labelKey: 'dashboardNavDocuments', icon: FileText },
  { to: '/chat', labelKey: 'dashboardNavChat', icon: MessageSquare },
  { to: '/profile', labelKey: 'dashboardNavProfile', icon: UserCircle },
];

export default function UserTopNavbar() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const { user, logout } = useAuth();
  const { t, lang, toggleLang } = useLanguage();
  const { theme, toggleTheme } = useTheme();
  const location = useLocation();
  const navigate = useNavigate();
  const initials = user?.nom ? user.nom.split(' ').map(x => x[0]).join('').toUpperCase().slice(0, 2) : '?';

  function handleLogout() {
    logout();
    navigate('/login');
  }

  return (
    <div className="sticky top-0 z-40">
      <header className="bg-[var(--color-surface-1)]/90 backdrop-blur-xl border-b border-[var(--color-border)]">
        <div className="max-w-[1280px] mx-auto px-4 sm:px-6 flex items-center h-16 gap-4">

          {/* Logo */}
          <Link to="/dashboard" className="flex items-center shrink-0">
            <img
              src={theme === 'dark' ? '/logo-dark.png' : '/logo.png'}
              alt="Plateforme Formation"
              style={{ width: 48, height: 48, objectFit: 'contain', borderRadius: 12 }}
            />
          </Link>

          {/* Desktop nav links */}
          <LayoutGroup>
            <nav className="hidden md:flex items-center gap-0.5 flex-1">
              {NAV_ITEMS.map(({ to, labelKey, icon: Icon }) => {
                const active = location.pathname === to;
                return (
                  <Link
                    key={to}
                    to={to}
                    className={`relative flex items-center gap-2 px-3 py-2 rounded-lg text-[13.5px] font-medium transition-colors duration-150 ${
                      active
                        ? 'text-[var(--color-primary)]'
                        : 'text-[var(--color-ink-muted)] hover:text-[var(--color-ink)] hover:bg-[var(--color-surface-2)]'
                    }`}
                  >
                    <Icon className="w-4 h-4 shrink-0" />
                    {t(labelKey)}
                    {active && (
                      <motion.div
                        layoutId="user-nav-underline"
                        className="absolute bottom-0 left-1.5 right-1.5 h-[2px] rounded-full bg-[var(--color-primary)]"
                        transition={{ type: 'spring', stiffness: 380, damping: 30 }}
                      />
                    )}
                  </Link>
                );
              })}
            </nav>
          </LayoutGroup>

          <div className="flex-1 md:hidden" />

          {/* Right controls */}
          <div className="flex items-center gap-1">
            <motion.button
              onClick={toggleLang}
              whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}
              className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[11px] font-bold tracking-wide text-[var(--color-ink-muted)] bg-[var(--color-surface-2)] hover:bg-[var(--color-surface-3)] transition-colors"
              title={lang === 'fr' ? 'Switch to English' : 'Passer en français'}
            >
              <Globe className="w-3.5 h-3.5" />
              {lang === 'fr' ? 'FR' : 'EN'}
            </motion.button>

            <motion.button
              onClick={toggleTheme}
              whileHover={{ scale: 1.05 }} whileTap={{ scale: 0.95 }}
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
              className="w-8 h-8 rounded-xl bg-[var(--color-primary)] text-white flex items-center justify-center text-[11px] font-bold ml-1 hover:opacity-80 transition-opacity overflow-hidden shrink-0"
              title={user?.nom}
            >
              {user?.photoUrl ? (
                <img
                  src={resolvePhotoUrl(user.photoUrl)}
                  alt={user.nom}
                  style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                  onError={e => { e.currentTarget.style.display = 'none'; e.currentTarget.nextSibling.style.display = 'flex'; }}
                />
              ) : null}
              <span style={{ display: user?.photoUrl ? 'none' : 'flex' }}>{initials}</span>
            </Link>

            {/* Logout */}
            <button
              onClick={handleLogout}
              className="p-2 rounded-xl text-[var(--color-ink-muted)] hover:text-[var(--color-primary)] hover:bg-[var(--color-surface-2)] transition-colors"
              title="Logout"
            >
              <LogOut className="w-4 h-4" />
            </button>

            {/* Mobile hamburger */}
            <button
              onClick={() => setMobileOpen(v => !v)}
              className="md:hidden p-2 rounded-xl text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-2)] transition-colors ml-0.5"
            >
              {mobileOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
            </button>
          </div>
        </div>
      </header>

      {/* Mobile dropdown */}
      <AnimatePresence>
        {mobileOpen && (
          <motion.nav
            initial={{ opacity: 0, y: -8 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -8 }}
            transition={{ type: 'spring', stiffness: 320, damping: 28 }}
            className="md:hidden bg-[var(--color-surface-1)] border-b border-[var(--color-border)] px-4 py-3 flex flex-col gap-1 shadow-lg"
          >
            {NAV_ITEMS.map(({ to, labelKey, icon: Icon }) => {
              const active = location.pathname === to;
              return (
                <Link
                  key={to}
                  to={to}
                  onClick={() => setMobileOpen(false)}
                  className={`flex items-center gap-3 px-3 py-2.5 rounded-xl text-[14px] font-medium transition-all ${
                    active
                      ? 'bg-[var(--color-primary)] text-white shadow-sm'
                      : 'text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-2)] hover:text-[var(--color-ink)]'
                  }`}
                >
                  <Icon className="w-[18px] h-[18px] shrink-0" />
                  {t(labelKey)}
                </Link>
              );
            })}

            <div className="border-t border-[var(--color-border)] mt-2 pt-2 flex items-center gap-2 px-1">
              <button onClick={toggleLang}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[11px] font-bold text-[var(--color-ink-muted)] bg-[var(--color-surface-2)] hover:bg-[var(--color-surface-3)]">
                <Globe className="w-3.5 h-3.5" />
                {lang === 'fr' ? 'FR' : 'EN'}
              </button>
              <button onClick={toggleTheme}
                className="p-2 rounded-xl text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-2)]">
                {theme === 'dark' ? <Sun className="w-[18px] h-[18px]" /> : <Moon className="w-[18px] h-[18px]" />}
              </button>
              <button onClick={handleLogout}
                className="ml-auto flex items-center gap-2 text-[13px] font-medium text-[var(--color-ink-muted)] hover:text-[var(--color-primary)] px-3 py-2 rounded-xl hover:bg-[var(--color-surface-2)]">
                <LogOut className="w-4 h-4" />
                Logout
              </button>
            </div>
          </motion.nav>
        )}
      </AnimatePresence>
    </div>
  );
}
