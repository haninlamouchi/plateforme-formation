import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Bell, CheckCheck } from 'lucide-react';
import { useChat } from '../../context/ChatContext';
import { useLanguage } from '../../context/LanguageContext';

function timeAgo(date, lang) {
  const seconds = Math.floor((Date.now() - new Date(date).getTime()) / 1000);
  if (seconds < 60) return lang === 'fr' ? "À l'instant" : 'Just now';
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} h`;
  const days = Math.floor(hours / 24);
  return `${days} j`;
}

export default function NotificationBell() {
  const { notifications, unreadCount, markRead, markAllRead } = useChat();
  const { lang } = useLanguage();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const ref = useRef();

  useEffect(() => {
    function onClickOutside(e) {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    }
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, []);

  function handleClick(notif) {
    setOpen(false);
    if (!notif.lue) markRead(notif.id);
    if (notif.lien) navigate(notif.lien);
  }

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen(o => !o)}
        className="relative p-2 rounded-xl text-[var(--color-ink-muted)] hover:bg-[var(--color-surface-2)] hover:text-[var(--color-ink)] transition-colors"
      >
        <Bell className="w-[18px] h-[18px]" />
        {unreadCount > 0 && (
          <span className="absolute top-0.5 right-0.5 min-w-[16px] h-[16px] px-1 rounded-full bg-[var(--color-primary)] text-white text-[9.5px] font-bold flex items-center justify-center">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ opacity: 0, y: -8, scale: 0.97 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -8, scale: 0.97 }}
            transition={{ type: 'spring', stiffness: 380, damping: 30 }}
            className="absolute right-0 mt-2 w-[320px] max-h-[420px] flex flex-col rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface-1)] shadow-[0_16px_40px_rgba(0,0,0,0.18)] overflow-hidden z-50"
          >
            <div className="flex items-center justify-between px-4 py-3 border-b border-[var(--color-border)]">
              <span className="text-[13.5px] font-bold text-[var(--color-ink)]">
                {lang === 'fr' ? 'Notifications' : 'Notifications'}
              </span>
              {unreadCount > 0 && (
                <button
                  onClick={markAllRead}
                  className="flex items-center gap-1 text-[11px] font-semibold text-[var(--color-primary)] hover:opacity-80"
                >
                  <CheckCheck className="w-3.5 h-3.5" />
                  {lang === 'fr' ? 'Tout marquer lu' : 'Mark all read'}
                </button>
              )}
            </div>

            <div className="flex-1 overflow-y-auto">
              {notifications.length === 0 ? (
                <p className="text-center text-[13px] text-[var(--color-ink-muted)] py-10">
                  {lang === 'fr' ? 'Aucune notification.' : 'No notifications.'}
                </p>
              ) : notifications.map(n => (
                <button
                  key={n.id}
                  onClick={() => handleClick(n)}
                  className="w-full flex items-start gap-2.5 px-4 py-3 text-left border-b border-[var(--color-border)] last:border-0 hover:bg-[var(--color-surface-2)] transition-colors"
                >
                  {!n.lue && <span className="mt-1.5 w-2 h-2 rounded-full bg-[var(--color-primary)] shrink-0" />}
                  <div className={`flex-1 min-w-0 ${n.lue ? 'ml-[18px]' : ''}`}>
                    <p className="text-[12.5px] leading-snug text-[var(--color-ink)]" style={{ fontWeight: n.lue ? 400 : 600 }}>
                      {n.contenu}
                    </p>
                    <span className="text-[10.5px] text-[var(--color-ink-muted)]">{timeAgo(n.dateCreation, lang)}</span>
                  </div>
                </button>
              ))}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
