import api from './api';

export async function getAllFormations(params = {}) {
  const r = await api.get('/formations', { params });
  return r.data;
}

export async function getFormationById(id) {
  const r = await api.get(`/formations/${id}`);
  return r.data;
}

export async function getFormationQualite(id) {
  const r = await api.get(`/formations/${id}/qualite`);
  return r.data;
}

export async function attachFormationTraces(id) {
  const r = await api.post(`/formations/${id}/traces`);
  return r.data;
}

export async function previewFormationCorrection(id) {
  const r = await api.post(`/formations/${id}/corriger`);
  return r.data;
}

export async function regenerateFormationModule(id, numero) {
  const r = await api.post(`/formations/${id}/modules/${numero}/regenerer`);
  return r.data;
}

export async function generateFormation({ objectif, documentIds }) {
  const r = await api.post('/formations/generate', { objectif, documentIds });
  return r.data;
}

export async function updateFormation(id, data) {
  const r = await api.put(`/formations/${id}`, data);
  return r.data;
}

export async function updateFormationStatut(id, statut) {
  const r = await api.put(`/formations/${id}/statut`, { statut });
  return r.data;
}

export async function deleteFormation(id) {
  const r = await api.delete(`/formations/${id}`);
  return r.data;
}

export async function exportFormationPdf(id, titre) {
  const r = await api.get(`/formations/${id}/export`, { responseType: 'blob' });
  const url = window.URL.createObjectURL(new Blob([r.data], { type: 'application/pdf' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = `${(titre || 'formation').replace(/[^\w\- ]+/g, '')}.pdf`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
}

export async function exportFormationPptx(id, titre) {
  const r = await api.get(`/formations/${id}/export/pptx`, { responseType: 'blob' });
  const url = window.URL.createObjectURL(new Blob([r.data], {
    type: 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
  }));
  const link = document.createElement('a');
  link.href = url;
  link.download = `${(titre || 'formation').replace(/[^\w\- ]+/g, '')}.pptx`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
}
