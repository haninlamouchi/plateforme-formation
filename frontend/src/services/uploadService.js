import api from './api';

const API_ORIGIN = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5092/api')
  .replace(/\/api\/?$/, '');

export function resolvePhotoUrl(url) {
  if (!url) return null;
  if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('blob:')) return url;
  return `${API_ORIGIN}${url}`;
}

export async function uploadAvatar(file) {
  const formData = new FormData();
  formData.append('file', file);

  const response = await api.post('/upload/avatar', formData);

  return resolvePhotoUrl(response.data.url);
}