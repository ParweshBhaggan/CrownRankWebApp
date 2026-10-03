import type { PaymentSettings } from "../api/models/payment"
import { defaultPaymentSettings } from "../format/currency"
import type { ApiCategory } from '../api/models/category'
import type { ApiSocialMediaDefault } from '../api/models/socialMediaDefault'
import { createContext, useContext } from 'react'

export const LookupContext = createContext<{
  categories: readonly ApiCategory[]
  platforms: readonly ApiSocialMediaDefault[]
  payments: PaymentSettings
  loading: boolean
  error: string
  retry: () => void
}>({ categories: [], platforms: [], payments: defaultPaymentSettings, loading: true, error: '', retry: () =>
{} })
export function useLookups()
{
  return useContext(LookupContext)
}

