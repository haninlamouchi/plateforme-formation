// Shared "visual" shell for every chart on the admin Dashboard and Analytics pages — a Power BI
// report-visual container: sharp corners, a hairline header divider, plain title (no accent bar,
// no uppercase — real Power BI visual titles are quiet sentence-case text).
export default function ChartCard({ title, children, loading }) {
  return (
    <div className="bi-visual">
      <div className="bi-visual__header">
        <h3 className="bi-visual__title">{title}</h3>
      </div>
      <div className="bi-visual__body">
        {loading ? (
          <div className="flex items-center justify-center py-8">
            <div className="w-5 h-5 rounded-full border-2 animate-spin" style={{ borderColor: 'var(--bi-grid-line)', borderTopColor: 'var(--bi-text-muted)' }} />
          </div>
        ) : children}
      </div>
    </div>
  );
}
