import { apiRequest } from '../httpClient'

export async function uploadProfileImage(file: File, referenceId: string): Promise<string>
{
  const body = new FormData()
  body.append('file', file)
  const result = await apiRequest<{ url: string }>(`/api/profile-images/${encodeURIComponent(referenceId)}`, { method: 'POST', body })
  return result.url
}
