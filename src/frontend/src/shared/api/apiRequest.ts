import { apiRequest, testApiRequest } from './httpClient'

// Options can supply headers, credentials, or a cancellation signal.
// Each helper controls its own HTTP method and serialized body.
export type ApiRequestOptions = Omit<RequestInit, 'method' | 'body'>

export function apiGet<T>(endpoint: string, options?: ApiRequestOptions): Promise<T>
{
  return apiRequest<T>(endpoint, { ...options, method: 'GET' })
}

export function apiPost<T = void>(endpoint: string, body: unknown, options?: ApiRequestOptions): Promise<T>
{
  return sendJson<T>('POST', endpoint, body, options)
}

export function apiPut<T = void>(endpoint: string, body: unknown, options?: ApiRequestOptions): Promise<T>
{
  return sendJson<T>('PUT', endpoint, body, options)
}

export function apiDelete<T = void>(endpoint: string, options?: ApiRequestOptions): Promise<T>
{
  return apiRequest<T>(endpoint, { ...options, method: 'DELETE' })
}

export function testApiPost<T = void>(endpoint: string, body: unknown, options?: ApiRequestOptions): Promise<T>
{
  return sendTestJson<T>('POST', endpoint, body, options)
}

function sendJson<T>(method: string, endpoint: string, body: unknown, options?: ApiRequestOptions): Promise<T>
{
  const headers = { 'Content-Type': 'application/json', ...Object.fromEntries(new Headers(options?.headers)) }

  return apiRequest<T>(endpoint, {
    ...options,
    method,
    headers,
    body: JSON.stringify(body),
  })
}

function sendTestJson<T>(method: string, endpoint: string, body: unknown, options?: ApiRequestOptions): Promise<T>
{
  const headers = { 'Content-Type': 'application/json', ...Object.fromEntries(new Headers(options?.headers)) }

  return testApiRequest<T>(endpoint, {
    ...options,
    method,
    headers,
    body: JSON.stringify(body),
  })
}
