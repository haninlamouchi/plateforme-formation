import './PasswordStrengthMeter.css';

function getStrength(password) {
  if (!password) return { score: 0, label: '', checks: [] };
  const checks = [
    { label: '8+ characters', ok: password.length >= 8 },
    { label: 'Uppercase letter', ok: /[A-Z]/.test(password) },
    { label: 'Lowercase letter', ok: /[a-z]/.test(password) },
    { label: 'Digit', ok: /\d/.test(password) },
  ];
  const score = checks.filter(c => c.ok).length;
  const labels = ['', 'Weak', 'Fair', 'Good', 'Strong'];
  return { score, label: labels[score], checks };
}

export default function PasswordStrengthMeter({ password }) {
  if (!password) return null;
  const { score, label, checks } = getStrength(password);
  const colors = ['', '#ef4444', '#f97316', '#eab308', '#22c55e'];

  return (
    <div className="psm">
      <div className="psm__bar-row">
        {[1, 2, 3, 4].map(i => (
          <div
            key={i}
            className="psm__bar"
            style={{ background: i <= score ? colors[score] : 'var(--color-border)' }}
          />
        ))}
        <span className="psm__label" style={{ color: colors[score] }}>{label}</span>
      </div>
      <ul className="psm__checks">
        {checks.map(c => (
          <li key={c.label} className={`psm__check ${c.ok ? 'psm__check--ok' : ''}`}>
            <span className="psm__check-icon">{c.ok ? '✓' : '○'}</span>
            {c.label}
          </li>
        ))}
      </ul>
    </div>
  );
}
