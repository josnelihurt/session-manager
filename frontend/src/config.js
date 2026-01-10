export const config = {
  api: {
    baseUrl: import.meta.env.VITE_API_URL || '/api',
  },
  ui: {
    refreshIntervalMs: parseInt(import.meta.env.VITE_REFRESH_INTERVAL || '30000', 10),
    messageTimeoutMs: parseInt(import.meta.env.VITE_MESSAGE_TIMEOUT || '3000', 10),
  },
}
