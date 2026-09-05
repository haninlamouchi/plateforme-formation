import { createContext, useContext, useEffect, useRef, useState, useCallback } from 'react';
import { createChatConnection } from '../services/chatConnection';
import { getNotifications, getUnreadCount, markNotificationRead, markAllNotificationsRead } from '../services/notificationService';
import { useAuth } from './AuthContext';

const ChatContext = createContext(null);

export function ChatProvider({ children }) {
  const { token, isAuthenticated } = useAuth();
  const [connection, setConnection] = useState(null);
  const [connected, setConnected] = useState(false);
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const connectionRef = useRef(null);

  useEffect(() => {
    if (!isAuthenticated || !token) {
      connectionRef.current?.stop();
      connectionRef.current = null;
      // eslint-disable-next-line react-hooks/set-state-in-effect -- resetting local state to mirror auth teardown, not a render-time derivation
      setConnection(null);
      setConnected(false);
      setNotifications([]);
      setUnreadCount(0);
      return;
    }

    const conn = createChatConnection(() => token);
    connectionRef.current = conn;

    conn.on('ReceiveNotification', dto => {
      setNotifications(prev => [dto, ...prev]);
      setUnreadCount(prev => prev + 1);
    });

    conn.start()
      .then(() => { setConnected(true); setConnection(conn); })
      .catch(() => setConnected(false));

    getNotifications().then(setNotifications).catch(() => {});
    getUnreadCount().then(setUnreadCount).catch(() => {});

    return () => {
      conn.stop();
      connectionRef.current = null;
    };
  }, [isAuthenticated, token]);

  const markRead = useCallback(async id => {
    setNotifications(prev => prev.map(n => (n.id === id ? { ...n, lue: true } : n)));
    setUnreadCount(prev => Math.max(0, prev - 1));
    try { await markNotificationRead(id); } catch { /* local state already updated optimistically */ }
  }, []);

  const markAllRead = useCallback(async () => {
    setNotifications(prev => prev.map(n => ({ ...n, lue: true })));
    setUnreadCount(0);
    try { await markAllNotificationsRead(); } catch { /* local state already updated optimistically */ }
  }, []);

  return (
    <ChatContext.Provider value={{ connection, connected, notifications, unreadCount, markRead, markAllRead }}>
      {children}
    </ChatContext.Provider>
  );
}

export function useChat() {
  const context = useContext(ChatContext);
  if (!context) throw new Error('useChat must be used within ChatProvider');
  return context;
}
