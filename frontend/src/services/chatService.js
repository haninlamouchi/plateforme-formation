import api from './api';

export async function getChatUsers() {
  const r = await api.get('/chat/users');
  return r.data;
}

export async function getGroupHistory(params = {}) {
  const r = await api.get('/chat/group', { params });
  return r.data;
}

export async function getPrivateHistory(userId, params = {}) {
  const r = await api.get(`/chat/private/${userId}`, { params });
  return r.data;
}

export async function getConversations() {
  const r = await api.get('/chat/conversations');
  return r.data;
}
