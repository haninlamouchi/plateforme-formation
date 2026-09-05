import api from './api';

export async function fetchAdminStats() {
  const response = await api.get('/admin/stats');
  console.log('[adminStats] response:', response.data);
  return response.data;
}

export async function fetchAdminCharts() {
  const response = await api.get('/admin/charts');
  return response.data;
}

export async function fetchPendingUsers() {
  const response = await api.get('/admin/utilisateurs/en-attente');
  return response.data;
}

export async function validateUser(userId) {
  const response = await api.put(`/admin/utilisateurs/${userId}/valider`);
  return response.data;
}

export async function rejectUser(userId) {
  const response = await api.put(`/admin/utilisateurs/${userId}/refuser`);
  return response.data;
}

export async function fetchAuditLog(params = {}) {
  const response = await api.get('/admin/audit-log', { params });
  return response.data;
}

export async function fetchAuditLogActions() {
  const response = await api.get('/admin/audit-log/actions');
  return response.data;
}