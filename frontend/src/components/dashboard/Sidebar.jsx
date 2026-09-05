import { Link, useLocation, useNavigate } from 'react-router-dom';
import { motion, LayoutGroup } from 'framer-motion';
import {
  LayoutDashboard
  , FileText, Users, ShieldCheck, GraduationCap,
  LogOut, UserCircle, FolderOpen, MessageSquare, BarChart3, ScrollText,
} from 'lucide-react';
import { useTheme } from '../../context/ThemeContext';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { resolvePhotoUrl } from '../../services/uploadService';

const SIDEBAR_W = 260;

function Avatar({ user, size = 36 }) {
  const initials = user?.nom
    ? user.nom.split(' ').map(x => x[0]).join('').toUpperCase().slice(0, 2)
    : '?';
  const photo = resolvePhotoUrl(user?.photoUrl);

  return (
    <div
      style={{
        width: size, height: size, borderRadius: 10,
        background: 'var(--color-primary)',
        flexShrink: 0, overflow: 'hidden',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}
    >
      {photo ? (
        <img
          src={photo}
          alt={user?.nom}
          style={{ width: '100%', height: '100%', objectFit: 'cover' }}
          onError={e => {
            e.currentTarget.style.display = 'none';
            if (e.currentTarget.nextSibling) e.currentTarget.nextSibling.style.display = 'flex';
          }}
        />
      ) : null}
      <span
        style={{
          display: photo ? 'none' : 'flex',
          color: '#fff', fontSize: size * 0.33, fontWeight: 700,
          alignItems: 'center', justifyContent: 'center',
          width: '100%', height: '100%',
        }}
      >
        {initials}
      </span>
    </div>
  );
}

export default function Sidebar({ open, onClose }) {
  const { user, logout } = useAuth();
  const { t } = useLanguage();
  const { theme } = useTheme();
  const location = useLocation();
  const navigate = useNavigate();

  const roleLabel = ({
    ADMINISTRATEUR: t('dashboardRoleAdmin'),
    RESPONSABLE_PEDAGOGIQUE: t('dashboardRoleResp'),
  }[user?.role] || user?.role);

  const mainNav = [
    { to: '/dashboard', label: t('dashboardNavDashboard'), icon: LayoutDashboard },
    { to: '/formations', label: t('dashboardNavFormations'), icon: GraduationCap },
    { to: '/documents', label: t('dashboardNavDocuments'), icon: FileText },
    { to: '/chat', label: t('dashboardNavChat'), icon: MessageSquare },
    { to: '/profile', label: t('dashboardNavProfile'), icon: UserCircle },
  ];

  const adminNav = user?.role === 'ADMINISTRATEUR' ? [
    { to: '/users', label: t('dashboardNavUsers'), icon: Users },
    { to: '/admin/validations', label: t('dashboardNavValidations'), icon: ShieldCheck },
    { to: '/categories', label: t('dashboardNavCategories'), icon: FolderOpen },
    { to: '/analytics', label: t('dashboardNavAnalytics'), icon: BarChart3 },
    { to: '/audit-log', label: t('dashboardNavAuditLog'), icon: ScrollText },
  ] : [];

  const isActive = (p) => location.pathname === p;

  function handleLogout() {
    logout();
    navigate('/login');
  }

  const NavItem = ({ to, label, icon: Icon }) => {
    const active = isActive(to);
    return (
      <Link
        to={to}
        onClick={onClose}
        className="relative flex items-center gap-3 px-3 py-2.5 rounded-xl text-[13.5px] font-medium transition-colors duration-150"
        style={active
          ? { background: 'var(--sidebar-active-bg)', color: 'var(--sidebar-active-ink)' }
          : { color: 'var(--sidebar-ink-muted)' }
        }
        onMouseEnter={e => { if (!active) { e.currentTarget.style.background = 'var(--sidebar-hover-bg)'; e.currentTarget.style.color = 'var(--sidebar-ink)'; } }}
        onMouseLeave={e => { if (!active) { e.currentTarget.style.background = ''; e.currentTarget.style.color = 'var(--sidebar-ink-muted)'; } }}
      >
        {active && (
          <motion.div
            layoutId="sidebar-active-bar"
            className="absolute left-0 inset-y-2 w-[3px] rounded-r-full"
            style={{ background: 'var(--sidebar-active-ink)' }}
            transition={{ type: 'spring', stiffness: 400, damping: 32 }}
          />
        )}
        <Icon className="w-[17px] h-[17px] shrink-0" />
        <span>{label}</span>
      </Link>
    );
  };

  return (
    <>
      {open && (
        <motion.div
          className="fixed inset-0 z-40 bg-black/40 backdrop-blur-sm lg:hidden"
          initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
          onClick={onClose}
        />
      )}

      <aside
        className="fixed top-0 left-0 h-screen z-50 flex flex-col transition-transform duration-300 lg:translate-x-0"
        style={{
          width: SIDEBAR_W,
          transform: open ? 'translateX(0)' : undefined,
          background: 'var(--sidebar-bg)',
          borderRight: '1px solid var(--sidebar-border)',
          boxShadow: '2px 0 16px rgba(15,23,42,0.07)',
        }}
      >
        {/* Logo */}
        <div
          className="flex items-center px-4 py-5"
          style={{ borderBottom: '1px solid var(--sidebar-divider)' }}
        >
          <div style={{ width: 140, height: 52, overflow: 'hidden', borderRadius: 10, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <img
              src={theme === 'dark' ? '/logo-dark.png' : '/logo.png'}
              alt="Plateforme Formation"
              style={{ width: '140%', height: '140%', objectFit: 'contain' }}
            />
          </div>
          <button
            onClick={onClose}
            className="ml-auto lg:hidden p-1.5 rounded-lg transition-colors"
            style={{ color: 'var(--sidebar-ink-muted)' }}
            onMouseEnter={e => { e.currentTarget.style.background = 'var(--sidebar-hover-bg)'; e.currentTarget.style.color = 'var(--sidebar-ink)'; }}
            onMouseLeave={e => { e.currentTarget.style.background = ''; e.currentTarget.style.color = 'var(--sidebar-ink-muted)'; }}
          >
            <svg viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
              <path d="M18 6 6 18"/><path d="m6 6 12 12"/>
            </svg>
          </button>
        </div>

        {/* Nav */}
        <LayoutGroup>
          <nav className="flex-1 flex flex-col px-3 py-4 overflow-y-auto gap-0.5">
            {mainNav.map(item => <NavItem key={item.to} {...item} />)}

            {adminNav.length > 0 && (
              <>
                <div className="flex items-center gap-2 px-3 pt-5 pb-1.5">
                  <span
                    className="text-[10px] font-bold tracking-widest uppercase"
                    style={{ color: 'var(--sidebar-label)' }}
                  >
                    Administration
                  </span>
                  <div className="flex-1 h-px" style={{ background: 'var(--sidebar-divider)' }} />
                </div>
                {adminNav.map(item => <NavItem key={item.to} {...item} />)}
              </>
            )}
          </nav>
        </LayoutGroup>

        {/* User footer */}
        <div
          className="px-3 py-3"
          style={{
            background: 'var(--sidebar-footer-bg)',
            borderTop: '1px solid var(--sidebar-divider)',
          }}
        >
          <div className="flex items-center gap-3 px-2 py-1.5">
            <Avatar user={user} size={36} />
            <div className="flex-1 min-w-0">
              <div
                className="text-[13px] font-semibold truncate leading-tight"
                style={{ color: 'var(--sidebar-ink)' }}
              >
                {user?.nom}
              </div>
              <div
                className="text-[11px] mt-0.5 truncate"
                style={{ color: 'var(--sidebar-ink-muted)' }}
              >
                {roleLabel}
              </div>
            </div>
            <button
              onClick={handleLogout}
              className="p-2 rounded-lg transition-colors shrink-0"
              style={{ color: 'var(--sidebar-ink-muted)' }}
              onMouseEnter={e => { e.currentTarget.style.color = '#FCA5A5'; e.currentTarget.style.background = 'rgba(239,68,68,0.12)'; }}
              onMouseLeave={e => { e.currentTarget.style.color = 'var(--sidebar-ink-muted)'; e.currentTarget.style.background = ''; }}
              title="Logout"
            >
              <LogOut className="w-[15px] h-[15px]" />
            </button>
          </div>
        </div>
      </aside>
    </>
  );
}
