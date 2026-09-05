import { Link } from 'react-router-dom';
import { motion, useReducedMotion } from 'framer-motion';
import { BookOpen, FileText, UserCircle, ArrowRight, MessageSquare } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useLanguage } from '../context/LanguageContext';

const ROLE_COLOR = {
  RESPONSABLE_PEDAGOGIQUE: '#3A6E4A',
};

function getGreeting(t) {
  const h = new Date().getHours();
  return h < 12 ? t('dashboardGreetingMorning') : h < 18 ? t('dashboardGreetingAfternoon') : t('dashboardGreetingEvening');
}

function FeatureTile({ icon: Icon, title, description, to, color, delay }) {
  const reduced = useReducedMotion();
  return (
    <Link to={to} className="block">
      <motion.div
        className="group relative flex flex-col gap-4 p-6 rounded-2xl bg-[var(--color-surface-1)] border border-[var(--color-border-subtle)] cursor-pointer overflow-hidden hover:border-[var(--color-border)]"
        style={{ boxShadow: '0 1px 3px rgba(0,0,0,0.07), 0 6px 18px rgba(0,0,0,0.05)', transition: 'box-shadow 0.2s, border-color 0.2s' }}
        initial={reduced ? false : { opacity: 0, y: 18 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay, type: 'spring', stiffness: 260, damping: 22 }}
        whileHover={reduced ? undefined : { y: -3 }}
      >
        <div
          className="absolute top-0 right-0 w-[160px] h-[160px] rounded-full opacity-[0.05] blur-2xl pointer-events-none transition-opacity duration-300 group-hover:opacity-[0.10]"
          style={{ background: color, transform: 'translate(40%, -40%)' }}
        />
        <div
          className="absolute left-0 top-4 bottom-4 w-[3px] rounded-r-full opacity-0 group-hover:opacity-100 group-hover:top-2 group-hover:bottom-2 transition-all duration-300"
          style={{ background: color }}
        />
        <div
          className="w-12 h-12 rounded-2xl flex items-center justify-center transition-all duration-300 group-hover:scale-110 shrink-0"
          style={{ background: `${color}18` }}
        >
          <Icon className="w-6 h-6" style={{ color }} />
        </div>
        <div className="flex-1">
          <div className="text-[15px] font-bold text-[var(--color-ink)] mb-1 tracking-tight">{title}</div>
          <div className="text-[13px] text-[var(--color-ink-muted)] leading-relaxed">{description}</div>
        </div>
        <ArrowRight
          className="w-4 h-4 shrink-0 opacity-0 group-hover:opacity-100 transition-all duration-200 group-hover:translate-x-1 self-end"
          style={{ color }}
        />
      </motion.div>
    </Link>
  );
}

export default function UserDashboard() {
  const { user } = useAuth();
  const { t, lang } = useLanguage();
  const reduced = useReducedMotion();

  const accent = ROLE_COLOR[user?.role] ?? '#1E6E8A';

  const greeting = getGreeting(t);
  const dateLabel = new Date().toLocaleDateString(
    lang === 'fr' ? 'fr-FR' : 'en-US',
    { weekday: 'long', day: 'numeric', month: 'long' }
  );

  const roleLabel = t('dashboardRoleResp');

  const initials = user?.nom
    ? user.nom.split(' ').map(x => x[0]).join('').toUpperCase().slice(0, 2)
    : '?';

  const tiles = [
    { icon: BookOpen, titleKey: 'userDashMyFormations', descKey: 'userDashMyFormationsDesc', to: '/formations', color: accent },
    { icon: FileText, titleKey: 'userDashMyDocs', descKey: 'userDashMyDocsDesc', to: '/documents', color: '#4A6E3A' },
    { icon: MessageSquare, titleKey: 'userDashChat', descKey: 'userDashChatDesc', to: '/chat', color: '#8A5A2E' },
    { icon: UserCircle, titleKey: 'userDashMyProfile', descKey: 'userDashMyProfileDesc', to: '/profile', color: '#3A4A6E' },
  ];

  return (
    <div className="max-w-[860px] mx-auto space-y-8">

      {/* Greeting card — light, warm, elegant */}
      <motion.div
        className="relative rounded-2xl overflow-hidden bg-[var(--color-surface-1)]"
        style={{
          border: '1px solid var(--color-border-subtle)',
          boxShadow: '0 1px 3px rgba(0,0,0,0.08), 0 8px 32px rgba(0,0,0,0.08)',
        }}
        initial={reduced ? false : { opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.05, type: 'spring', stiffness: 220, damping: 22 }}
      >
        {/* Soft role-color blush at the top */}
        <div
          className="absolute top-0 left-0 right-0 pointer-events-none"
          style={{
            height: '160px',
            background: `linear-gradient(180deg, ${accent}0D 0%, transparent 100%)`,
          }}
        />

        {/* Thin role-color top edge */}
        <div
          className="absolute top-0 left-[12%] right-[12%] h-[1px] pointer-events-none"
          style={{ background: `linear-gradient(90deg, transparent, ${accent}66, transparent)` }}
        />

        <div className="flex items-center" style={{ minHeight: '188px' }}>

          {/* Left: text */}
          <div className="flex-1 px-10 lg:px-14 py-9">

            <motion.p
              className="text-[var(--color-ink-muted)] mb-4"
              style={{
                fontSize: '11px',
                fontFamily: 'var(--font-mono)',
                letterSpacing: '0.14em',
                textTransform: 'uppercase',
              }}
              initial={reduced ? false : { opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.13 }}
            >
              {greeting}&nbsp;&nbsp;·&nbsp;&nbsp;
              <span style={{ textTransform: 'capitalize', opacity: 0.7 }}>{dateLabel}</span>
            </motion.p>

            <motion.h1
              className="text-[var(--color-ink)] font-extrabold leading-none mb-5"
              style={{ fontSize: 'clamp(28px, 3vw, 44px)', letterSpacing: '-0.035em' }}
              initial={reduced ? false : { opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.20 }}
            >
              {user?.nom}
            </motion.h1>

            <div className="flex items-center gap-3">
              <motion.span
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '7px',
                  padding: '5px 14px',
                  borderRadius: '20px',
                  fontSize: '11px',
                  fontWeight: 600,
                  letterSpacing: '0.02em',
                  color: accent,
                  background: `${accent}12`,
                  border: `1px solid ${accent}22`,
                }}
                initial={reduced ? false : { opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: 0.26 }}
              >
                {roleLabel}
              </motion.span>

              <motion.div
                initial={reduced ? false : { opacity: 0 }}
                animate={{ opacity: 1 }}
                transition={{ delay: 0.32 }}
              >
                <Link
                  to="/profile"
                  className="flex items-center gap-1 text-[11px] font-semibold transition-opacity duration-150"
                  style={{ color: 'var(--color-ink-muted)', opacity: 0.6 }}
                  onMouseEnter={e => e.currentTarget.style.opacity = '1'}
                  onMouseLeave={e => e.currentTarget.style.opacity = '0.6'}
                >
                  {t('userDashEditProfile')}
                  <ArrowRight className="w-3 h-3" />
                </Link>
              </motion.div>
            </div>
          </div>

          {/* Right: decorative initials orb */}
          <div
            className="hidden lg:flex items-center justify-center shrink-0"
            style={{ width: '220px', paddingRight: '44px' }}
          >
            <motion.div
              style={{
                width: '108px',
                height: '108px',
                borderRadius: '50%',
                background: `radial-gradient(circle at 38% 32%, ${accent}22 0%, ${accent}0D 55%, ${accent}03 100%)`,
                border: `1px solid ${accent}1C`,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
              initial={reduced ? false : { opacity: 0, scale: 0.82 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ delay: 0.30, type: 'spring', stiffness: 260, damping: 22 }}
            >
              <span
                style={{
                  fontSize: '36px',
                  fontWeight: 900,
                  color: `${accent}80`,
                  letterSpacing: '-0.03em',
                  lineHeight: 1,
                  userSelect: 'none',
                }}
              >
                {initials}
              </span>
            </motion.div>
          </div>
        </div>
      </motion.div>

      {/* My space tiles */}
      <div>
        <h3
          className="text-[var(--color-ink-muted)] uppercase mb-4"
          style={{ fontSize: '12px', fontFamily: 'var(--font-mono)', letterSpacing: '0.14em' }}
        >
          {t('userDashSpaceTitle')}
        </h3>
        <div className="grid sm:grid-cols-2 gap-3">
          {tiles.map(({ icon, titleKey, descKey, to, color }, i) => (
            <FeatureTile
              key={to}
              icon={icon}
              title={t(titleKey)}
              description={t(descKey)}
              to={to}
              color={color}
              delay={0.1 + i * 0.07}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
