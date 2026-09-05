import { motion, useReducedMotion } from 'framer-motion';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { resolvePhotoUrl } from '../../services/uploadService';

function getGreeting(t) {
  const h = new Date().getHours();
  return h < 12 ? t('dashboardGreetingMorning') : h < 18 ? t('dashboardGreetingAfternoon') : t('dashboardGreetingEvening');
}

export default function HeroBanner() {
  const { user } = useAuth();
  const { t, lang } = useLanguage();
  const reduced = useReducedMotion();

  const greeting = getGreeting(t);
  const dateLabel = new Date().toLocaleDateString(
    lang === 'fr' ? 'fr-FR' : 'en-US',
    { weekday: 'long', day: 'numeric', month: 'long' }
  );
  const roleLabel = t(
    user?.role === 'ADMINISTRATEUR' ? 'dashboardRoleAdmin' : 'dashboardRoleResp'
  );
  const initials = user?.nom
    ? user.nom.split(' ').map(x => x[0]).join('').toUpperCase().slice(0, 2)
    : '?';
  const photo = resolvePhotoUrl(user?.photoUrl);

  return (
    <motion.div
      className="relative rounded-2xl overflow-hidden bg-[var(--color-surface-1)]"
      style={{
        border: '1px solid var(--color-border-subtle)',
        boxShadow: 'var(--shadow-card)',
      }}
      initial={reduced ? false : { opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ type: 'spring', stiffness: 220, damping: 22, delay: 0.05 }}
    >
      {/* Soft warm blush at the top — fades to nothing */}
      <div
        className="absolute top-0 left-0 right-0 pointer-events-none"
        style={{
          height: '160px',
          background: 'linear-gradient(180deg, rgba(155,17,30,0.05) 0%, transparent 100%)',
        }}
      />

      {/* Thin crimson top edge — brand mark, not a band */}
      <div
        className="absolute top-0 left-[12%] right-[12%] h-[1px] pointer-events-none"
        style={{ background: 'linear-gradient(90deg, transparent, rgba(155,17,30,0.40), transparent)' }}
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
              color: 'var(--color-primary)',
              background: 'rgba(155,17,30,0.07)',
              border: '1px solid rgba(155,17,30,0.14)',
            }}
            initial={reduced ? false : { opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.26 }}
          >
            {roleLabel}
          </motion.span>
        </div>

        {/* Right: profile photo or initials orb */}
        <div
          className="hidden lg:flex items-center justify-center shrink-0"
          style={{ width: '220px', paddingRight: '44px' }}
        >
          <motion.div
            style={{
              width: '108px',
              height: '108px',
              borderRadius: '50%',
              overflow: 'hidden',
              background: photo
                ? 'var(--color-surface-2)'
                : 'radial-gradient(circle at 38% 32%, rgba(155,17,30,0.13) 0%, rgba(155,17,30,0.05) 55%, rgba(155,17,30,0.01) 100%)',
              border: '1px solid rgba(155,17,30,0.11)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
            initial={reduced ? false : { opacity: 0, scale: 0.82 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ delay: 0.30, type: 'spring', stiffness: 260, damping: 22 }}
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
                fontSize: '36px',
                fontWeight: 900,
                color: 'rgba(155,17,30,0.50)',
                letterSpacing: '-0.03em',
                lineHeight: 1,
                userSelect: 'none',
                alignItems: 'center',
                justifyContent: 'center',
                width: '100%',
                height: '100%',
              }}
            >
              {initials}
            </span>
          </motion.div>
        </div>
      </div>
    </motion.div>
  );
}
