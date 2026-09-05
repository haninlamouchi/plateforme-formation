import { useState, useEffect, useRef, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { MessageSquare, Send, Search, X, Users as UsersIcon, Plus } from 'lucide-react';
import { getChatUsers, getGroupHistory, getPrivateHistory, getConversations } from '../services/chatService';
import { resolvePhotoUrl } from '../services/uploadService';
import { useAuth } from '../context/AuthContext';
import { useChat } from '../context/ChatContext';
import { useLanguage } from '../context/LanguageContext';
import './Chat.css';

function Avatar({ nom, photoUrl, size = 38 }) {
  const initials = nom ? nom.split(' ').map(x => x[0]).join('').toUpperCase().slice(0, 2) : '?';
  const photo = resolvePhotoUrl(photoUrl);
  return (
    <div className="chat-avatar" style={{ width: size, height: size }}>
      {photo
        ? <img src={photo} alt={nom} />
        : <span style={{ fontSize: size * 0.36 }}>{initials}</span>}
    </div>
  );
}

function formatTime(date) {
  return new Date(date).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

const GROUP_ID = 'group';

export default function Chat() {
  const { user } = useAuth();
  const { connection, connected } = useChat();
  const { lang } = useLanguage();
  const [searchParams, setSearchParams] = useSearchParams();
  const meId = Number(user?.id);

  const [conversations, setConversations] = useState([]);
  const [activeId, setActiveId] = useState(() => searchParams.get('dest') ?? GROUP_ID);
  const [messages, setMessages] = useState([]);
  const [loadingMessages, setLoadingMessages] = useState(true);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);

  const [newChatOpen, setNewChatOpen] = useState(false);
  const [chatUsers, setChatUsers] = useState([]);
  const [userSearch, setUserSearch] = useState('');

  const messagesEndRef = useRef(null);
  const activeIdRef = useRef(activeId);
  useEffect(() => { activeIdRef.current = activeId; }, [activeId]);

  // A notification click can deep-link into a private thread via ?dest=<userId>
  useEffect(() => {
    const dest = searchParams.get('dest');
    if (dest) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- deriving initial active thread from the URL on mount
      setActiveId(dest);
      setSearchParams({}, { replace: true });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const activeConversation = activeId === GROUP_ID
    ? null
    : conversations.find(c => String(c.userId) === activeId);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  const loadConversations = useCallback(async () => {
    try {
      const data = await getConversations();
      setConversations(data);
    } catch {
      // non-blocking
    }
  }, []);

  // Subscribe to the app-wide SignalR connection (shared with the notification bell)
  useEffect(() => {
    if (!connection) return;

    function onGroupMessage(dto) {
      if (activeIdRef.current === GROUP_ID) {
        setMessages(prev => [...prev, dto]);
      }
    }
    function onPrivateMessage(dto) {
      const partnerId = dto.expediteurId === meId ? dto.destinataireId : dto.expediteurId;
      if (activeIdRef.current === String(partnerId)) {
        setMessages(prev => [...prev, dto]);
      }
      loadConversations();
    }

    connection.on('ReceiveGroupMessage', onGroupMessage);
    connection.on('ReceivePrivateMessage', onPrivateMessage);

    return () => {
      connection.off('ReceiveGroupMessage', onGroupMessage);
      connection.off('ReceivePrivateMessage', onPrivateMessage);
    };
  }, [connection, meId, loadConversations]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- initial data fetch on mount
    loadConversations();
  }, [loadConversations]);

  // Load history whenever the active chat changes
  useEffect(() => {
    let cancelled = false;
    // eslint-disable-next-line react-hooks/set-state-in-effect -- entering a loading state before an async fetch
    setLoadingMessages(true);
    const load = activeId === GROUP_ID
      ? getGroupHistory()
      : getPrivateHistory(Number(activeId));

    load
      .then(data => { if (!cancelled) setMessages(data); })
      .catch(() => { if (!cancelled) setMessages([]); })
      .finally(() => { if (!cancelled) setLoadingMessages(false); });

    if (activeId !== GROUP_ID) {
      // opening a private thread clears its unread badge locally
      setConversations(prev => prev.map(c =>
        String(c.userId) === activeId ? { ...c, nonLus: 0 } : c));
    }

    return () => { cancelled = true; };
  }, [activeId]);

  useEffect(() => { scrollToBottom(); }, [messages, scrollToBottom]);

  async function handleSend(e) {
    e.preventDefault();
    const contenu = draft.trim();
    if (!contenu || !connection || sending) return;

    setSending(true);
    setDraft('');
    try {
      if (activeId === GROUP_ID) {
        await connection.invoke('SendGroupMessage', contenu);
      } else {
        await connection.invoke('SendPrivateMessage', Number(activeId), contenu);
      }
    } catch {
      setDraft(contenu);
    } finally {
      setSending(false);
    }
  }

  async function openNewChat() {
    try {
      const users = await getChatUsers();
      setChatUsers(users);
      setUserSearch('');
      setNewChatOpen(true);
    } catch {
      // non-blocking
    }
  }

  function startPrivateChat(u) {
    setNewChatOpen(false);
    setActiveId(String(u.id));
    setConversations(prev => prev.some(c => c.userId === u.id)
      ? prev
      : [{ userId: u.id, nom: u.nom, photoUrl: u.photoUrl, dernierMessage: null, dateDernierMessage: null, nonLus: 0 }, ...prev]);
  }

  const filteredUsers = chatUsers.filter(u =>
    u.nom.toLowerCase().includes(userSearch.toLowerCase()));

  const headerTitle = activeId === GROUP_ID
    ? (lang === 'fr' ? 'Salon général' : 'General channel')
    : activeConversation?.nom ?? '';

  return (
    <div className="chat">
      <motion.div className="chat-header"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22 }}>
        <MessageSquare className="chat-header__icon" />
        <div>
          <h1 className="chat-header__title">{lang === 'fr' ? 'Messagerie' : 'Chat'}</h1>
          <p className="chat-header__sub">
            {connected
              ? (lang === 'fr' ? 'Connecté' : 'Connected')
              : (lang === 'fr' ? 'Connexion...' : 'Connecting...')}
          </p>
        </div>
      </motion.div>

      <motion.div className="chat-body"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22, delay: 0.05 }}>

        {/* Conversation list */}
        <aside className="chat-sidebar">
          <button className="chat-new-btn" onClick={openNewChat}>
            <Plus className="w-4 h-4" />
            {lang === 'fr' ? 'Nouveau message' : 'New message'}
          </button>

          <button
            className={`chat-conv-item ${activeId === GROUP_ID ? 'chat-conv-item--active' : ''}`}
            onClick={() => setActiveId(GROUP_ID)}>
            <div className="chat-avatar chat-avatar--group"><UsersIcon className="w-[18px] h-[18px]" /></div>
            <div className="chat-conv-item__body">
              <span className="chat-conv-item__name">{lang === 'fr' ? 'Salon général' : 'General channel'}</span>
              <span className="chat-conv-item__preview">{lang === 'fr' ? 'Tout le monde' : 'Everyone'}</span>
            </div>
          </button>

          {conversations.map(c => (
            <button key={c.userId}
              className={`chat-conv-item ${activeId === String(c.userId) ? 'chat-conv-item--active' : ''}`}
              onClick={() => setActiveId(String(c.userId))}>
              <Avatar nom={c.nom} photoUrl={c.photoUrl} size={38} />
              <div className="chat-conv-item__body">
                <span className="chat-conv-item__name">{c.nom}</span>
                <span className="chat-conv-item__preview">{c.dernierMessage || '—'}</span>
              </div>
              {c.nonLus > 0 && <span className="chat-conv-item__badge">{c.nonLus}</span>}
            </button>
          ))}
        </aside>

        {/* Thread */}
        <section className="chat-thread">
          <div className="chat-thread__header">{headerTitle}</div>

          <div className="chat-thread__messages">
            {loadingMessages ? (
              <div className="chat-loading"><div className="chat-spinner" /></div>
            ) : messages.length === 0 ? (
              <div className="chat-empty">
                {lang === 'fr' ? 'Aucun message pour le moment.' : 'No messages yet.'}
              </div>
            ) : messages.map(m => {
              const mine = m.expediteurId === meId;
              return (
                <div key={m.id} className={`chat-msg ${mine ? 'chat-msg--mine' : ''}`}>
                  {!mine && <Avatar nom={m.expediteurNom} photoUrl={m.expediteurPhotoUrl} size={28} />}
                  <div className="chat-msg__bubble">
                    {!mine && activeId === GROUP_ID && (
                      <span className="chat-msg__author">{m.expediteurNom}</span>
                    )}
                    <p className="chat-msg__text">{m.contenu}</p>
                    <span className="chat-msg__time">{formatTime(m.dateEnvoi)}</span>
                  </div>
                </div>
              );
            })}
            <div ref={messagesEndRef} />
          </div>

          <form className="chat-composer" onSubmit={handleSend}>
            <input
              className="chat-composer__input"
              placeholder={lang === 'fr' ? 'Écrire un message...' : 'Write a message...'}
              value={draft}
              onChange={e => setDraft(e.target.value)}
              disabled={!connected}
            />
            <button type="submit" className="chat-composer__send" disabled={!draft.trim() || sending || !connected}>
              <Send className="w-4 h-4" />
            </button>
          </form>
        </section>
      </motion.div>

      {/* New chat modal */}
      <AnimatePresence>
        {newChatOpen && (
          <motion.div className="chat-overlay" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            onClick={() => setNewChatOpen(false)}>
            <motion.div className="chat-modal"
              initial={{ opacity: 0, scale: 0.95, y: 16 }} animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 16 }}
              transition={{ type: 'spring', stiffness: 320, damping: 28 }}
              onClick={e => e.stopPropagation()}>
              <div className="chat-modal__header">
                <h2 className="chat-modal__title">{lang === 'fr' ? 'Nouveau message' : 'New message'}</h2>
                <button className="chat-modal__close" onClick={() => setNewChatOpen(false)}><X className="w-5 h-5" /></button>
              </div>
              <div className="chat-modal__search">
                <Search className="w-4 h-4" />
                <input autoFocus
                  placeholder={lang === 'fr' ? 'Rechercher une personne...' : 'Search a person...'}
                  value={userSearch}
                  onChange={e => setUserSearch(e.target.value)} />
              </div>
              <div className="chat-modal__list">
                {filteredUsers.length === 0 ? (
                  <p className="chat-modal__empty">{lang === 'fr' ? 'Aucun résultat.' : 'No results.'}</p>
                ) : filteredUsers.map(u => (
                  <button key={u.id} className="chat-modal__item" onClick={() => startPrivateChat(u)}>
                    <Avatar nom={u.nom} photoUrl={u.photoUrl} size={34} />
                    <div className="chat-modal__item-body">
                      <span className="chat-modal__item-name">{u.nom}</span>
                      <span className="chat-modal__item-email">{u.email}</span>
                    </div>
                  </button>
                ))}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
