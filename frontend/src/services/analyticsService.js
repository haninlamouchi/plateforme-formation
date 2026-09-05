import api from './api';

export async function fetchAnalyticsSummary() {
  const response = await api.get('/admin/analytics/summary');
  return response.data;
}

export async function fetchAnalyticsCharts() {
  const response = await api.get('/admin/analytics/charts');
  return response.data;
}
