// Hand-rolled inline SVG donut chart, restyled as a Power BI donut visual: segments sit edge to edge
// (a real PBI donut has no artificial gap carved between slices) and labels use the report's text
// colors, not the series color, matching the other restyled chart components.
const DEFAULT_META = {
  RESPONSABLE_PEDAGOGIQUE: { label: 'Resp. pédagogique', color: '#01B8AA' },
};

export default function DonutChart({ data, meta = DEFAULT_META, centerLabel = 'utilisateurs', keyField = 'role' }) {
  const r = 44, CX = 60, CY = 60, T = 14;
  const circ = 2 * Math.PI * r;
  const total = data.reduce((s, d) => s + d.count, 0);

  const segs = [];
  let cum = 0;
  for (const d of data) {
    const info = meta[d[keyField]] ?? { color: '#5F6B6D', label: d[keyField] };
    const arcLen = total > 0 ? (d.count / total) * circ : 0;
    segs.push({ ...d, ...info, arcLen, offset: -cum });
    cum += arcLen;
  }

  return (
    <div className="flex items-center gap-5">
      <svg viewBox="0 0 120 120" width={110} height={110} style={{ flexShrink: 0 }}>
        <circle cx={CX} cy={CY} r={r} fill="none" stroke="var(--bi-grid-line)" strokeWidth={T} />
        {segs.map((seg, i) => (
          <circle key={i} cx={CX} cy={CY} r={r} fill="none"
            stroke={seg.color} strokeWidth={T}
            strokeDasharray={`${seg.arcLen} ${circ}`}
            strokeDashoffset={seg.offset}
            transform={`rotate(-90 ${CX} ${CY})`}
          />
        ))}
        <text x={CX} y={CY - 7} textAnchor="middle" fontSize={20} fontWeight={600} fill="var(--bi-text)">{total}</text>
        <text x={CX} y={CY + 10} textAnchor="middle" fontSize={8.5} fill="var(--bi-text-muted)">{centerLabel}</text>
      </svg>
      <div className="flex flex-col gap-2 min-w-0">
        {segs.map((seg, i) => (
          <div key={i} className="flex items-center gap-2">
            <span className="w-2.5 h-2.5 shrink-0" style={{ background: seg.color, borderRadius: 2 }} />
            <span className="text-[12px] truncate" style={{ color: 'var(--bi-text-muted)' }}>{seg.label}</span>
            <span className="ml-auto text-[12px] font-semibold" style={{ color: 'var(--bi-text)' }}>{seg.count}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
