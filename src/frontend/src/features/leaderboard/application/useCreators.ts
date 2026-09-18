import { useEffect, useState } from 'react'
import type { Creator } from '../domain/creator'
import { startReadRequest } from '../../../shared/api/startReadRequest'
import { ApiLeaderboardRepository } from '../data/ApiLeaderboardRepository'

const repository = new ApiLeaderboardRepository()

export function useCreators(path: string, revision = 0) {
  const [state, setState] = useState<{ key: string; path: string; creators: readonly Creator[]; error: string }>({ key: '', path: '', creators: [], error: '' })
  const [retry, setRetry] = useState(0)
  const key = `${path}:${revision}:${retry}`
  const dailyDate = path.match(/\/daily\/([^/?]+)/)?.[1]
  useEffect(() => startReadRequest(
    () => dailyDate ? repository.getDaily(dailyDate) : repository.getAll(),
    creators => setState({ key, path, creators, error: '' }),
    error => setState({ key, path, creators: [], error: error instanceof Error ? error.message : 'Could not load rankings.' }),
  ), [path, key, dailyDate])
  return { creators: state.creators, error: state.path === path ? state.error : '', loading: state.path !== path, retry: () => setRetry(x => x + 1) }
}
