import type { ApiCategory } from '../api/models/category'
import type { ApiSocialMediaDefault } from '../api/models/socialMediaDefault'
import type { PaymentSettings } from '../api/models/payment'
import { defaultPaymentSettings } from '../format/currency'
import { createContext, useContext } from 'react'

export const LookupContext = createContext<{
  paymentSettings: PaymentSettings
  categories: readonly ApiCategory[]
  platforms: readonly ApiSocialMediaDefault[]
  loading: boolean
  error: string
  retry: () => void
}>({ paymentSettings: defaultPaymentSettings, categories: [], platforms: [], loading: true, error: '', retry: () =>
{} })
export function useLookups()
{
  return useContext(LookupContext)
}

