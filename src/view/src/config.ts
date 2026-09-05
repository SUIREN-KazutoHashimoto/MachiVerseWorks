export interface ClientConfig {
  readonly serverUrl: string;
  readonly reconnectMinimumDelayMs: number;
  readonly reconnectMaximumDelayMs: number;
  readonly subscriptionRefreshMs: number;
}

export function loadClientConfig(): ClientConfig {
  const configuredUrl = import.meta.env.VITE_SERVER_URL?.trim();
  const serverUrl = configuredUrl && configuredUrl.length > 0
    ? configuredUrl
    : 'ws://127.0.0.1:5080/ws';

  const parsedUrl = new URL(serverUrl);
  if (parsedUrl.protocol !== 'ws:' && parsedUrl.protocol !== 'wss:') {
    throw new Error('VITE_SERVER_URL must use ws:// or wss://.');
  }

  return {
    serverUrl: parsedUrl.toString(),
    reconnectMinimumDelayMs: 1_000,
    reconnectMaximumDelayMs: 5_000,
    subscriptionRefreshMs: 200,
  };
}
