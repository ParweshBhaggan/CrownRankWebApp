const apiBaseUrl = import.meta.env.VITE_API_URL ?? ''

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, init)
  if (!response.ok) throw new Error(`API request failed with status ${response.status}`)
  return response.json() as Promise<T>
}

