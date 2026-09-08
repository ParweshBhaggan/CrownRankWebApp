import { useEffect, useState } from 'react'
import type { Creator } from '../domain/creator'
import { apiRequest, resolveApiAsset } from '../../../shared/api/httpClient'

export function useCreators(path: string, revision = 0) {
  const [state, setState] = useState<{ key: string; path: string; creators: readonly Creator[]; error: string }>({ key: '', path: '', creators: [], error: '' })
  const [retry, setRetry] = useState(0)
  const key = `${path}:${revision}:${retry}`
  useEffect(() => {
    const controller = new AbortController()
    apiRequest<readonly Creator[]>(path, { signal: controller.signal }).then(creators => {
      if (!controller.signal.aborted) setState({ key, path, creators: creators.map(c => ({ ...c, imageUrl: resolveApiAsset(c.imageUrl) })), error: '' })
    }).catch(error => {
      if (!controller.signal.aborted) setState({ key, path, creators: [], error: error instanceof Error ? error.message : 'Could not load rankings.' })
    })
    return () => controller.abort()
  }, [path, key])
  return { creators: state.creators, error: state.path === path ? state.error : '', loading: state.path !== path, retry: () => setRetry(x => x + 1) }
}
