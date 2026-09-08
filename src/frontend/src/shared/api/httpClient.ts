export const apiBaseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, init)
  if (!response.ok) throw new Error(`API request failed with status ${response.status}`)
  return response.json() as Promise<T>
}

export function resolveApiAsset(url: string): string {
  return url.startsWith('/') ? `${apiBaseUrl}${url}` : url
}
