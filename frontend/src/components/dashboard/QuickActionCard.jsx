import { motion } from 'framer-motion';
import { ArrowRight } from 'lucide-react';

export default function QuickActionCard({ icon: Icon, title, description, color, delay = 0 }) {
  return (
    <motion.div
      className="group relative flex items-center gap-4 p-5 rounded-2xl bg-[var(--color-surface-1)] border border-[var(--color-border-subtle)] cursor-pointer hover:border-[var(--color-border)] overflow-hidden"
      style={{ boxShadow: '0 1px 3px rgba(0,0,0,0.07), 0 6px 18px rgba(0,0,0,0.05)', transition: 'box-shadow 0.2s, border-color 0.2s' }}
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay, type: 'spring', stiffness: 260, damping: 22 }}
      whileHover={{ y: -2 }}
    >
      {/* Left accent bar — slides down from top on hover */}
      <div
        className="absolute left-0 top-4 bottom-4 w-[3px] rounded-r-full opacity-0 group-hover:opacity-100 group-hover:top-2 group-hover:bottom-2 transition-all duration-300"
        style={{ background: color }}
      />

      {/* Icon */}
      <div
        className="w-11 h-11 rounded-2xl flex items-center justify-center shrink-0 transition-all duration-300 group-hover:scale-110"
        style={{ background: `${color}15` }}
      >
        <Icon className="w-5 h-5 transition-colors duration-200" style={{ color }} />
      </div>

      {/* Text */}
      <div className="flex-1 min-w-0">
        <div className="text-[14px] font-semibold text-[var(--color-ink)] mb-0.5 transition-colors duration-200">{title}</div>
        <div className="text-[12px] text-[var(--color-ink-muted)] leading-relaxed">{description}</div>
      </div>

      {/* Arrow — colored with card's accent on hover */}
      <ArrowRight
        className="w-4 h-4 shrink-0 opacity-0 group-hover:opacity-100 transition-all duration-200 group-hover:translate-x-1"
        style={{ color }}
      />
    </motion.div>
  );
}
