import api from './api';

export async function askChatbot(payload) {
  const r = await api.post('/chatbot/ask', payload);
  return r.data;
}
