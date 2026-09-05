import api from './api';

export async function getMe() {
  const r = await api.get('/profile/me');
  return r.data;
}

export async function updateMe(data) {
  const r = await api.put('/profile/me', data);
  return r.data;
}

export async function changePassword(data) {
  const r = await api.put('/profile/me/change-password', data);
  return r.data;
}

export async function getAllUsers(params = {}) {
  const r = await api.get('/admin/utilisateurs', { params });
  return r.data;
}

export async function getUserById(id) {
  const r = await api.get(`/admin/utilisateurs/${id}`);
  return r.data;
}

export async function updateUser(id, data) {
  const r = await api.put(`/admin/utilisateurs/${id}`, data);
  return r.data;
}

export async function deactivateUser(id) {
  const r = await api.put(`/admin/utilisateurs/${id}/desactiver`);
  return r.data;
}

export async function reactivateUser(id) {
  const r = await api.put(`/admin/utilisateurs/${id}/reactiver`);
  return r.data;
}
