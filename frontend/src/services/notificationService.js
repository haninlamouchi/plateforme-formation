import api from './api';

export async function getNotifications(params = {}) {
  const r = await api.get('/notifications', { params });
  return r.data;
}

export async function getUnreadCount() {
  const r = await api.get('/notifications/unread-count');
  return r.data;
}

export async function markNotificationRead(id) {
  const r = await api.put(`/notifications/${id}/read`);
  return r.data;
}

export async function markAllNotificationsRead() {
  const r = await api.put('/notifications/read-all');
  return r.data;
}
