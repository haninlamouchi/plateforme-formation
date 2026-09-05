import { motion } from 'framer-motion';

// Animated horizontal bar chart, restyled as a Power BI bar visual: a single-measure bar chart in
// Power BI uses one solid series color for every bar (not a per-bar opacity ramp), sharp/lightly
// rounded ends instead of full pills, and dark (not series-colored) value labels.
export default function HorizBarChart({ data, color = '#01B8AA' }) {
  const max = Math.max(...data.map(d => d.count), 1);
  return (
    <div className="flex flex-col gap-2.5">
      {data.map((d, i) => (
        <div key={i} className="flex items-center gap-3">
          <span className="text-[12px] shrink-0 w-28 truncate" style={{ color: 'var(--bi-text-muted)' }}>{d.name}</span>
          <div className="flex-1 h-[10px] overflow-hidden" style={{ background: 'var(--bi-grid-line)', borderRadius: 2 }}>
            <motion.div
              className="h-full"
              style={{ background: color, borderRadius: 2 }}
              initial={{ width: 0 }}
              animate={{ width: `${(d.count / max) * 100}%` }}
              transition={{ delay: i * 0.05, duration: 0.5, ease: [0.16, 1, 0.3, 1] }}
            />
          </div>
          <span className="text-[12px] font-semibold w-6 text-right shrink-0" style={{ color: 'var(--bi-text)' }}>{d.count}</span>
        </div>
      ))}
    </div>
  );
}
