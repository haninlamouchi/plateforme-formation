import api from './api';

export async function getAllDocuments(params = {}) {
  const r = await api.get('/documents', { params });
  // Returns { items, total, page, pageSize }
  return r.data;
}

export async function getDocumentById(id) {
  const r = await api.get(`/documents/${id}`);
  return r.data;
}

export async function uploadDocument({ file, titre, categorieId, typeDocument, langue }) {
  const form = new FormData();
  form.append('File', file);
  form.append('Titre', titre);
  if (categorieId) form.append('CategorieId', categorieId);
  if (typeDocument) form.append('TypeDocument', typeDocument);
  if (langue) form.append('Langue', langue);
  const r = await api.post('/documents/upload', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return r.data;
}

export async function updateDocument(id, data) {
  const r = await api.put(`/documents/${id}`, data);
  return r.data;
}

export async function deleteDocument(id) {
  const r = await api.delete(`/documents/${id}`);
  return r.data;
}

export async function getDocumentCompetences(id) {
  const r = await api.get(`/documents/${id}/competences`);
  return r.data;
}
