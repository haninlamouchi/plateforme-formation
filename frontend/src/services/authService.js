import api from './api';

export async function registerUser(payload) {
  const response = await api.post('/auth/register', payload);
  return response.data;
}

export async function loginUser(email, password, rememberMe = false) {
  const response = await api.post('/auth/login', { email, password, rememberMe });
  return response.data;
}

export async function googleLogin(credential) {
  const response = await api.post('/auth/google', { credential });
  return response.data;
}

export async function refreshTokens(refreshToken) {
  const response = await api.post('/auth/refresh', { refreshToken });
  return response.data;
}

export async function requestPasswordReset(email) {
  const response = await api.post('/auth/forgot-password', { email });
  return response.data;
}

export async function resetPassword(token, newPassword) {
  const response = await api.post('/auth/reset-password', { token, newPassword });
  return response.data;
}
