import { useEffect, useState } from 'react'
import type { Creator } from '../domain/creator'
import { startReadRequest } from '../../../shared/api/startReadRequest'
import { apiRequest, resolveApiAsset } from '../../../shared/api/httpClient'

export function useCreators(path: string, revision = 0) {
  const [state, setState] = useState<{ key: string; path: string; creators: readonly Creator[]; error: string }>({ key: '', path: '', creators: [], error: '' })
  const [retry, setRetry] = useState(0)
  const key = `${path}:${revision}:${retry}`
  useEffect(() => startReadRequest(
    signal => apiRequest<readonly Creator[]>(path, { signal }),
    creators => setState({ key, path, creators: creators.map(c => ({ ...c, imageUrl: resolveApiAsset(c.imageUrl) })), error: '' }),
    error => setState({ key, path, creators: [], error: error instanceof Error ? error.message : 'Could not load rankings.' }),
  ), [path, key])
  return { creators: state.creators, error: state.path === path ? state.error : '', loading: state.path !== path, retry: () => setRetry(x => x + 1) }
}
