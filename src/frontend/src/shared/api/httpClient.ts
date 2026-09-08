export const apiBaseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

export class ApiError extends Error {
  constructor(message: string, readonly status: number) { super(message) }
}

export async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, init)
  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as { error?: string; detail?: string; errors?: Record<string, string[]> }
    const message = problem.errors ? Object.values(problem.errors).flat().join(' ') : problem.error ?? problem.detail
    throw new ApiError(message || 'The request could not be completed. Please try again.', response.status)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export function resolveApiAsset(url: string): string {
  return url.startsWith('/') ? `${apiBaseUrl}${url}` : url
}
