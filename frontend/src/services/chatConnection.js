import * as signalR from '@microsoft/signalr';

const API_ORIGIN = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5092/api')
  .replace(/\/api\/?$/, '');

export function createChatConnection(getToken) {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${API_ORIGIN}/hub/chat`, { accessTokenFactory: getToken })
    .withAutomaticReconnect()
    .build();
}
