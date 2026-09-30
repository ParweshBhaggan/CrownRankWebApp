import { useEffect, useState, type ReactNode } from 'react'
import { getCategories, getSocialMediaDefaults, type ApiCategory, type ApiSocialMediaDefault } from '../../features/leaderboard/data/ApiLeaderboardRepository'
import { startReadRequest } from '../api/startReadRequest'

import { LookupContext } from './useLookups'

export function LookupProvider({ children }: { children: ReactNode }) {
  const [revision, setRevision] = useState(0)
  const [state, setState] = useState({ categories: [] as readonly ApiCategory[], platforms: [] as readonly ApiSocialMediaDefault[], loading: true, error: '' })
  useEffect(() => startReadRequest(
    () => Promise.all([getCategories(), getSocialMediaDefaults()]),
    ([categories, platforms]) => setState({ categories, platforms, loading: false, error: '' }),
    error => setState({ categories: [], platforms: [], loading: false, error: error instanceof Error ? error.message : 'Could not load entry options.' }),
  ), [revision])
  return <LookupContext.Provider value={{ ...state, retry: () => setRevision(value => value + 1) }}>{children}</LookupContext.Provider>
}
