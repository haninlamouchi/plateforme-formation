import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5092/api';
const STORAGE_KEY = 'pf_auth';

const api = axios.create({ baseURL: API_BASE_URL });

export function setAuthToken(token) {
  if (token) {
    api.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  } else {
    delete api.defaults.headers.common['Authorization'];
  }
}

function getStoredAuth() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY) || sessionStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function updateStoredTokens(token, refreshToken) {
  for (const storage of [localStorage, sessionStorage]) {
    const raw = storage.getItem(STORAGE_KEY);
    if (raw) {
      try {
        storage.setItem(STORAGE_KEY, JSON.stringify({ ...JSON.parse(raw), token, refreshToken }));
      } catch {
        // Corrupted or quota-limited storage entry: ignore, next login will overwrite it.
      }
    }
  }
}

// Singleton promise so concurrent 401s share one refresh call instead of racing
let refreshPromise = null;

api.interceptors.response.use(
  response => response,
  async error => {
    const original = error.config;
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true;

      if (!refreshPromise) {
        refreshPromise = (async () => {
          try {
            const stored = getStoredAuth();
            if (!stored?.refreshToken) throw new Error('No refresh token');

            const { data } = await axios.post(`${API_BASE_URL}/auth/refresh`, {
              refreshToken: stored.refreshToken,
            });

            updateStoredTokens(data.token, data.refreshToken);
            setAuthToken(data.token);
            return data.token;
          } catch {
            localStorage.removeItem(STORAGE_KEY);
            sessionStorage.removeItem(STORAGE_KEY);
            setAuthToken(null);
            // Notify AuthContext to clear state without a hard page redirect
            window.dispatchEvent(new Event('auth:logout'));
            throw new Error('Session expired');
          } finally {
            refreshPromise = null;
          }
        })();
      }

      try {
        const token = await refreshPromise;
        original.headers['Authorization'] = `Bearer ${token}`;
        return api(original);
      } catch {
        return Promise.reject(error);
      }
    }
    return Promise.reject(error);
  }
);

export default api;
