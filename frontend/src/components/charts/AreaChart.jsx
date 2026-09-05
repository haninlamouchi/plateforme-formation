// Hand-rolled inline SVG area/sparkline chart — no charting library in this project. Restyled to
// read as a Power BI line/area visual: solid gridlines + a real axis baseline (BI charts don't use
// dashed rules), and dark (not series-colored) data labels — Power BI's default label color is the
// report's text color, not the series color.
function smoothPath(pts) {
  if (pts.length < 2) return '';
  let d = `M${pts[0].x},${pts[0].y}`;
  for (let i = 1; i < pts.length; i++) {
    const cp = (pts[i - 1].x + pts[i].x) / 2;
    d += ` C${cp},${pts[i - 1].y} ${cp},${pts[i].y} ${pts[i].x},${pts[i].y}`;
  }
  return d;
}

// `data` items use `.month` as the x-axis label field (kept as-is for drop-in compatibility with the
// original caller) and `.count` as the value — pass day labels (e.g. "12/08") in `.month` for a daily
// series, it's just a label.
//
// Built for a ~6-point monthly series originally. A longer series (e.g. 30 daily points) crammed into
// the same width made every axis label overlap into illegible mush — so only ~6 evenly-spaced axis
// labels are shown regardless of point count, and dot/line size shrinks a bit as points pack in.
export default function AreaChart({ data, color = '#01B8AA' }) {
  const W = 300, H = 110, PX = 10, PY = 18;
  const n = data.length;
  const dense = n > 10;
  const dotR = n > 20 ? 2 : n > 10 ? 3 : 4;
  const labelStep = Math.max(1, Math.ceil(n / 6));

  const max = Math.max(...data.map(d => d.count), 1);
  const pts = data.map((d, i) => ({
    x: PX + (i / Math.max(1, n - 1)) * (W - PX * 2),
    y: PY + (1 - d.count / max) * (H - PY * 2),
  }));
  const line = smoothPath(pts);
  const baseline = H - PY - 4;
  const area = `${line} L${pts[pts.length - 1].x},${baseline} L${pts[0].x},${baseline} Z`;
  const gradientId = `ag-${color.replace('#', '')}`;

  return (
    <svg viewBox={`0 0 ${W} ${H}`} width="100%" style={{ overflow: 'visible' }}>
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.16} />
          <stop offset="100%" stopColor={color} stopOpacity={0} />
        </linearGradient>
      </defs>
      {[0.25, 0.5, 0.75].map(f => {
        const y = PY + (1 - f) * (H - PY * 2);
        return <line key={f} x1={PX} x2={W - PX} y1={y} y2={y} stroke="var(--bi-grid-line)" strokeWidth={1} />;
      })}
      <line x1={PX} x2={W - PX} y1={baseline} y2={baseline} stroke="var(--bi-grid-line)" strokeWidth={1.2} />
      <path d={area} fill={`url(#${gradientId})`} />
      <path d={line} fill="none" stroke={color} strokeWidth={dense ? 1.5 : 2} strokeLinejoin="round" strokeLinecap="round" />
      {pts.map((p, i) => (
        <circle key={i} cx={p.x} cy={p.y} r={dotR} fill="var(--bi-visual-bg)" stroke={color} strokeWidth={dense ? 1.2 : 2} />
      ))}
      {pts.map((p, i) => i % labelStep === 0 && (
        <text key={`l${i}`} x={p.x} y={H - 2} textAnchor="middle" fontSize={8} fill="var(--bi-text-muted)">{data[i].month}</text>
      ))}
      {pts.map((p, i) => data[i].count > 0 && (
        <text key={`v${i}`} x={p.x} y={p.y - 7} textAnchor="middle" fontSize={8.5} fontWeight={600} fill="var(--bi-text)">{data[i].count}</text>
      ))}
    </svg>
  );
}
