// Groq answers use light markdown (bullet lists, **bold**) — render it instead of dumping
// raw '*'/'-' characters as plain text.
function renderInline(text) {
  return text.split(/(\*\*[^*]+\*\*)/g).map((part, i) =>
    part.startsWith('**') && part.endsWith('**')
      ? <strong key={i}>{part.slice(2, -2)}</strong>
      : <span key={i}>{part}</span>
  );
}

export default function MarkdownText({ text, className = '' }) {
  if (!text) return null;

  const lines = text.split('\n');
  const blocks = [];
  let list = [];

  const flushList = () => {
    if (list.length) {
      blocks.push(<ul key={blocks.length} className="list-disc pl-4 space-y-0.5 my-1">{list}</ul>);
      list = [];
    }
  };

  lines.forEach((rawLine, i) => {
    const line = rawLine.trim();
    if (/^[-*]\s+/.test(line)) {
      list.push(<li key={i}>{renderInline(line.replace(/^[-*]\s+/, ''))}</li>);
    } else if (line === '') {
      flushList();
    } else {
      flushList();
      blocks.push(<p key={blocks.length} className="my-1 first:mt-0 last:mb-0">{renderInline(line)}</p>);
    }
  });
  flushList();

  return <div className={className}>{blocks}</div>;
}
