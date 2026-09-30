import { createContext, useContext } from 'react'
import type { ApiCategory, ApiSocialMediaDefault } from '../../features/leaderboard/data/ApiLeaderboardRepository'

export const LookupContext = createContext<{ categories: readonly ApiCategory[]; platforms: readonly ApiSocialMediaDefault[]; loading: boolean; error: string; retry: () => void }>({ categories: [], platforms: [], loading: true, error: '', retry: () => {} })
export function useLookups() { return useContext(LookupContext) }
