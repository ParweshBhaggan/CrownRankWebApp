import { useEffect, useState } from 'react'
import type { Creator } from '../domain/creator'
import { startReadRequest } from '../../../shared/api/startReadRequest'
import { appServices } from '../../../shared/config/appServices'

const repository = appServices.leaderboardRepository

export function useCreators(dailyDate?: string, revision = 0)
{
  const selection = dailyDate ?? 'all-time'

  const [state, setState] = useState<{ key: string; selection: string; creators: readonly Creator[]; error: string }>({
    key: '',
    selection: '',
    creators: [],
    error: '',
  })
  const [retry, setRetry] = useState(0)
  const key = `${selection}:${revision}:${retry}`

  useEffect(
    () =>
      startReadRequest(
        () => (dailyDate ? repository.getDaily(dailyDate) : repository.getAll()),
        (creators) => setState({ key, selection, creators, error: '' }),
        (error) =>
          setState({
            key,
            selection,
            creators: [],
            error: error instanceof Error ? error.message : 'Could not load rankings.',
          }),
      ),
    [selection, key, dailyDate],
  )

  return {
    creators: state.creators,
    error: state.selection === selection ? state.error : '',
    loading: state.selection !== selection,
    retry: () => setRetry((x) => x + 1),
  }
}
