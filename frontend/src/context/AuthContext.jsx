import { createContext, useContext, useState, useEffect } from 'react';
import { setAuthToken } from '../services/api';

const AuthContext = createContext(null);
const STORAGE_KEY = 'pf_auth';

function readStoredAuth() {
  try {
    // rememberMe → localStorage; session-only → sessionStorage
    const raw = localStorage.getItem(STORAGE_KEY) || sessionStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      setAuthToken(parsed.token);
      return { token: parsed.token, refreshToken: parsed.refreshToken, user: parsed.user };
    }
  } catch {
    localStorage.removeItem(STORAGE_KEY);
    sessionStorage.removeItem(STORAGE_KEY);
  }
  return { token: null, refreshToken: null, user: null };
}

export function AuthProvider({ children }) {
  const [{ user, token, refreshToken }, setAuth] = useState(readStoredAuth);

  // Listen for forced logout triggered by the Axios interceptor after a failed refresh
  useEffect(() => {
    function handleForcedLogout() {
      setAuth({ token: null, refreshToken: null, user: null });
    }
    window.addEventListener('auth:logout', handleForcedLogout);
    return () => window.removeEventListener('auth:logout', handleForcedLogout);
  }, []);

  function login(authResponse, rememberMe = false) {
    const nextUser = {
      id: authResponse.id,
      nom: authResponse.nom,
      email: authResponse.email,
      role: authResponse.role,
      photoUrl: authResponse.photoUrl ?? null,
    };

    setAuth({ token: authResponse.token, refreshToken: authResponse.refreshToken, user: nextUser });
    setAuthToken(authResponse.token);

    const payload = JSON.stringify({
      token: authResponse.token,
      refreshToken: authResponse.refreshToken,
      user: nextUser,
    });

    // Clear both storages first, then write to the right one
    localStorage.removeItem(STORAGE_KEY);
    sessionStorage.removeItem(STORAGE_KEY);
    if (rememberMe) {
      localStorage.setItem(STORAGE_KEY, payload);
    } else {
      // sessionStorage survives tab refreshes but is cleared when the tab closes
      sessionStorage.setItem(STORAGE_KEY, payload);
    }
  }

  function logout() {
    setAuth({ token: null, refreshToken: null, user: null });
    setAuthToken(null);
    localStorage.removeItem(STORAGE_KEY);
    sessionStorage.removeItem(STORAGE_KEY);
  }

  function updateUser(patch) {
    setAuth(prev => {
      const next = { ...prev, user: { ...prev.user, ...patch } };
      for (const storage of [localStorage, sessionStorage]) {
        const raw = storage.getItem(STORAGE_KEY);
        if (raw) {
          try {
            storage.setItem(STORAGE_KEY, JSON.stringify({ ...JSON.parse(raw), user: next.user }));
          } catch {
            // Corrupted or quota-limited storage entry: ignore, next login will overwrite it.
          }
        }
      }
      return next;
    });
  }

  return (
    <AuthContext.Provider value={{ user, token, refreshToken, isAuthenticated: Boolean(token), login, logout, updateUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within AuthProvider');
  return context;
}
