import type { ApiCategory } from '../api/models/category'
import type { ApiSocialMediaDefault } from '../api/models/socialMediaDefault'
import { createContext, useContext } from 'react'

export const LookupContext = createContext<{
  categories: readonly ApiCategory[]
  platforms: readonly ApiSocialMediaDefault[]
  loading: boolean
  error: string
  retry: () => void
}>({ categories: [], platforms: [], loading: true, error: '', retry: () =>
{} })
export function useLookups()
{
  return useContext(LookupContext)
}
