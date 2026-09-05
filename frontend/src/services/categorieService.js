import api from './api';

export async function getAllCategories() {
  const r = await api.get('/categories');
  return r.data;
}

export async function createCategorie(data) {
  const r = await api.post('/categories', data);
  return r.data;
}

export async function updateCategorie(id, data) {
  const r = await api.put(`/categories/${id}`, data);
  return r.data;
}

export async function deleteCategorie(id) {
  const r = await api.delete(`/categories/${id}`);
  return r.data;
}

export async function suggestCategories() {
  const r = await api.get('/categories/suggest');
  return r.data;
}

export async function suggestCategoryForDocument(file) {
  const form = new FormData();
  form.append('File', file);
  const r = await api.post('/categories/suggest-for-document', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return r.data;
}
