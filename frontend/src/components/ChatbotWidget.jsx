import { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { MessageCircle, X, Send, Bot, User, Sparkles, ChevronDown, ChevronUp, FileText } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useLanguage } from '../context/LanguageContext';
import { askChatbot } from '../services/chatbotService';
import CompetencesModal from './CompetencesModal';
import MarkdownText from './MarkdownText';

export default function ChatbotWidget() {
  const { isAuthenticated } = useAuth();
  const { lang } = useLanguage();
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [competencesModal, setCompetencesModal] = useState(null);
  const [openSources, setOpenSources] = useState({});
  const scrollRef = useRef();

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [messages, loading, open]);

  if (!isAuthenticated) return null;

  async function handleSend(e) {
    e.preventDefault();
    const question = input.trim();
    if (!question || loading) return;

    const history = messages
      .filter(m => !m.isError)
      .map(m => ({ role: m.role, content: m.content }));

    setMessages(prev => [...prev, { role: 'user', content: question }]);
    setInput('');
    setLoading(true);

    try {
      const data = await askChatbot({ question, history });
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: data.answer,
        sources: data.sources,
        mode: data.mode || 'documents',
        competences: data.competences?.length ? data.competences : null,
      }]);
    } catch {
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: lang === 'fr'
          ? "Désolé, une erreur s'est produite. Réessayez dans un instant."
          : 'Sorry, something went wrong. Please try again in a moment.',
        isError: true,
      }]);
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <motion.button
        onClick={() => setOpen(o => !o)}
        whileHover={{ scale: 1.06 }}
        whileTap={{ scale: 0.94 }}
        className="fixed bottom-6 right-6 z-50 w-14 h-14 rounded-full bg-[var(--color-primary)] text-white shadow-[0_12px_28px_rgba(0,0,0,0.25)] flex items-center justify-center"
        aria-label={lang === 'fr' ? 'Assistant IA' : 'AI assistant'}
      >
        <AnimatePresence mode="wait" initial={false}>
          <motion.span
            key={open ? 'close' : 'open'}
            initial={{ opacity: 0, rotate: -45 }}
            animate={{ opacity: 1, rotate: 0 }}
            exit={{ opacity: 0, rotate: 45 }}
            transition={{ duration: 0.15 }}
            className="flex items-center justify-center"
          >
            {open ? <X className="w-6 h-6" /> : <MessageCircle className="w-6 h-6" />}
          </motion.span>
        </AnimatePresence>
      </motion.button>

      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ opacity: 0, y: 16, scale: 0.97 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 16, scale: 0.97 }}
            transition={{ type: 'spring', stiffness: 380, damping: 30 }}
            className="fixed bottom-24 right-6 z-50 w-[min(380px,calc(100vw-2rem))] h-[min(560px,calc(100vh-8rem))] flex flex-col rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] shadow-[0_24px_60px_rgba(0,0,0,0.28)] overflow-hidden"
          >
            <div className="flex items-center gap-2.5 px-4 py-3.5 border-b border-[var(--color-border)] bg-[var(--color-surface-2)]">
              <div className="w-8 h-8 rounded-full bg-[var(--color-primary)] flex items-center justify-center shrink-0">
                <Bot className="w-4.5 h-4.5 text-white" />
              </div>
              <div className="min-w-0">
                <p className="text-[13.5px] font-bold text-[var(--color-ink)] leading-tight">
                  {lang === 'fr' ? 'Assistant IA' : 'AI Assistant'}
                </p>
                <p className="text-[11px] text-[var(--color-ink-muted)] leading-tight">
                  {lang === 'fr' ? 'Basé sur vos documents' : 'Powered by your documents'}
                </p>
              </div>
            </div>

            <div ref={scrollRef} className="flex-1 overflow-y-auto px-4 py-4 flex flex-col gap-3">
              {messages.length === 0 && (
                <p className="text-center text-[12.5px] text-[var(--color-ink-muted)] mt-6 px-4">
                  {lang === 'fr'
                    ? 'Posez une question sur vos documents de formation.'
                    : 'Ask a question about your training documents.'}
                </p>
              )}

              {messages.map((m, i) => (
                <div key={i} className={`flex flex-col ${m.role === 'user' ? 'items-end' : 'items-start'}`}>
                  <div className="flex items-end gap-2 max-w-[85%]">
                    {m.role === 'assistant' && (
                      <div className="w-6 h-6 rounded-full bg-[var(--color-primary)] flex items-center justify-center shrink-0">
                        <Bot className="w-3.5 h-3.5 text-white" />
                      </div>
                    )}
                    <div
                      className={`px-3.5 py-2.5 rounded-2xl text-[13px] leading-snug ${
                        m.role === 'user'
                          ? 'bg-[var(--color-primary)] text-white whitespace-pre-wrap rounded-br-md'
                          : m.isError
                          ? 'bg-red-500/10 text-red-500 whitespace-pre-wrap rounded-bl-md'
                          : 'bg-[var(--color-surface-2)] text-[var(--color-ink)] rounded-bl-md'
                      }`}
                    >
                      {m.role === 'assistant' && !m.isError ? <MarkdownText text={m.content} /> : m.content}
                    </div>
                    {m.role === 'user' && (
                      <div className="w-6 h-6 rounded-full bg-[var(--color-surface-2)] flex items-center justify-center shrink-0">
                        <User className="w-3.5 h-3.5 text-[var(--color-ink-muted)]" />
                      </div>
                    )}
                  </div>
                  {m.competences && (
                    <button
                      onClick={() => setCompetencesModal(m.competences)}
                      className="mt-1.5 ml-8 flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[12px] font-medium text-[var(--color-primary)] bg-[var(--color-primary)]/10 hover:bg-[var(--color-primary)]/16 transition-colors"
                    >
                      <Sparkles className="w-3.5 h-3.5" />
                      {lang === 'fr'
                        ? `Voir les compétences (${m.competences.length})`
                        : `View competences (${m.competences.length})`}
                    </button>
                  )}
                  {m.role === 'assistant' && m.sources?.length > 0 && (
                    <div className="mt-1.5 ml-8 max-w-[calc(85%-2rem)]">
                      <button
                        onClick={() => setOpenSources(prev => ({ ...prev, [i]: !prev[i] }))}
                        className="flex items-center gap-1.5 text-[11px] font-medium text-[var(--color-primary)] hover:underline"
                      >
                        <FileText className="w-3.5 h-3.5" />
                        {lang === 'fr' ? `Voir les sources (${m.sources.length})` : `View sources (${m.sources.length})`}
                        {openSources[i] ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
                      </button>
                      {openSources[i] && (
                        <div className="mt-1.5 space-y-1.5">
                          {m.sources.map((source, sourceIndex) => (
                            <div key={`${source.documentId}-${source.ordre}-${sourceIndex}`} className="rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-1)] px-2.5 py-2 text-[11px] leading-snug text-[var(--color-ink-muted)]">
                              <p className="font-semibold text-[var(--color-ink)]">
                                {source.documentTitre}
                                {source.numeroPage ? ` — ${lang === 'fr' ? 'page' : 'page'} ${source.numeroPage}` : ''}
                              </p>
                              {source.extrait && <p className="mt-1">{source.extrait}</p>}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                  {m.role === 'assistant' && !m.isError && m.mode === 'general' && (
                    <span className="mt-1.5 ml-8 text-[10px] text-[var(--color-ink-muted)]">
                      {lang === 'fr' ? 'Réponse générale — non basée sur un document importé' : 'General answer — not based on an uploaded document'}
                    </span>
                  )}
                </div>
              ))}

              {loading && (
                <div className="flex justify-start">
                  <div className="flex items-end gap-2">
                    <div className="w-6 h-6 rounded-full bg-[var(--color-primary)] flex items-center justify-center shrink-0">
                      <Bot className="w-3.5 h-3.5 text-white" />
                    </div>
                    <div className="px-3.5 py-2.5 rounded-2xl rounded-bl-md bg-[var(--color-surface-2)] flex items-center gap-1">
                      <span className="w-1.5 h-1.5 rounded-full bg-[var(--color-ink-muted)] animate-bounce [animation-delay:-0.3s]" />
                      <span className="w-1.5 h-1.5 rounded-full bg-[var(--color-ink-muted)] animate-bounce [animation-delay:-0.15s]" />
                      <span className="w-1.5 h-1.5 rounded-full bg-[var(--color-ink-muted)] animate-bounce" />
                    </div>
                  </div>
                </div>
              )}
            </div>

            <form onSubmit={handleSend} className="flex items-center gap-2 px-3 py-3 border-t border-[var(--color-border)]">
              <input
                value={input}
                onChange={e => setInput(e.target.value)}
                placeholder={lang === 'fr' ? 'Votre question...' : 'Your question...'}
                disabled={loading}
                className="flex-1 min-w-0 px-3.5 py-2.5 rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-0)] text-[13px] text-[var(--color-ink)] placeholder:text-[var(--color-ink-muted)] focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/40"
              />
              <button
                type="submit"
                disabled={loading || !input.trim()}
                className="w-9 h-9 rounded-xl bg-[var(--color-primary)] text-white flex items-center justify-center shrink-0 disabled:opacity-40 transition-opacity"
              >
                <Send className="w-4 h-4" />
              </button>
            </form>
          </motion.div>
        )}
      </AnimatePresence>

      <AnimatePresence>
        {competencesModal && (
          <CompetencesModal
            competences={competencesModal}
            onClose={() => setCompetencesModal(null)}
            lang={lang}
          />
        )}
      </AnimatePresence>
    </>
  );
}
