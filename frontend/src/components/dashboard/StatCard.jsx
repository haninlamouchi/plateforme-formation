import { useRef, useEffect } from 'react';
import { motion, useMotionValue, useTransform, animate, useReducedMotion } from 'framer-motion';

function AnimatedNumber({ value, suffix = '' }) {
  const ref = useRef(null);
  const num = useMotionValue(0);
  const rounded = useTransform(num, (v) => Math.round(v));
  const display = useTransform(rounded, (v) => `${v}${suffix}`);

  useEffect(() => {
    const c = animate(num, value, { duration: 1, ease: [0.16, 1, 0.3, 1] });
    return c.stop;
  }, [value, num]);

  useEffect(() => {
    const unsub = display.on('change', (v) => { if (ref.current) ref.current.textContent = v; });
    return unsub;
  }, [display]);

  return <span ref={ref}>0{suffix}</span>;
}

// Power BI "Card" visual: a thin accent rule (the closest a stock BI card gets to a color-coded
// series), a small-caps label, and one large tabular-figure number — no icon well, no glow, no lift.
export default function StatCard({ icon: Icon, value, suffix, label, color, delay = 0, loading = false }) {
  const reduced = useReducedMotion();

  if (loading) {
    return (
      <div className="bi-kpi">
        <style>{`@keyframes stat-shimmer{0%{transform:translateX(-100%)}100%{transform:translateX(100%)}}`}</style>
        <div className="bi-kpi__accent" style={{ background: 'var(--bi-grid-line)' }} />
        {[{ w: '50%', h: 11, mb: 10 }, { w: '65%', h: 30, mb: 0 }].map((s, i) => (
          <div key={i} className="relative overflow-hidden"
            style={{ width: s.w, height: s.h, borderRadius: 3, marginBottom: s.mb, background: 'var(--bi-grid-line)' }}>
            <div style={{
              position: 'absolute', inset: 0,
              background: 'linear-gradient(90deg, transparent 0%, rgba(255,255,255,0.35) 50%, transparent 100%)',
              animation: 'stat-shimmer 1.4s infinite',
            }} />
          </div>
        ))}
      </div>
    );
  }

  return (
    <motion.div
      className="bi-kpi"
      initial={reduced ? false : { opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay, duration: 0.35, ease: [0.16, 1, 0.3, 1] }}
    >
      <div className="bi-kpi__accent" style={{ background: color }} />
      <div className="flex items-start justify-between gap-2 mb-2">
        <div className="bi-kpi__label">{label}</div>
        {Icon && <Icon className="w-3.5 h-3.5 shrink-0 mt-0.5" style={{ color: 'var(--bi-text-muted)' }} />}
      </div>
      <div className="bi-kpi__value">
        <AnimatedNumber value={value} suffix={suffix} />
      </div>
    </motion.div>
  );
}
