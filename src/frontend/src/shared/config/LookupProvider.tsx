import { getPaymentSettings } from "../api/services/paymentApi"
import { configurePaymentSettings, defaultPaymentSettings } from "../format/currency"
import type { ApiCategory } from '../api/models/category'
import type { ApiSocialMediaDefault } from '../api/models/socialMediaDefault'
import { getCategories } from '../api/services/categoryApi'
import { getSocialMediaDefaults } from '../api/services/socialMediaDefaultApi'
import { useEffect, useState, type ReactNode } from 'react'
import { startReadRequest } from '../api/startReadRequest'

import { LookupContext } from './useLookups'

export function LookupProvider({ children }: { children: ReactNode })
{
  const [revision, setRevision] = useState(0)
  const [state, setState] = useState({
    categories: [] as readonly ApiCategory[],
    platforms: [] as readonly ApiSocialMediaDefault[],
    payments: defaultPaymentSettings,
    loading: true,
    error: '',
  })
  useEffect(
    () =>
      startReadRequest(
        () => Promise.all([getCategories(), getSocialMediaDefaults(), getPaymentSettings()]),
        ([categories, platforms, payments]) =>
        {
          configurePaymentSettings(payments)
          setState({ categories, platforms, payments, loading: false, error: '' })
        },
        (error) =>
          setState({
            categories: [],
            platforms: [],
            payments: defaultPaymentSettings,
            loading: false,
            error: error instanceof Error ? error.message : 'Could not load entry options.',
          }),
      ),
    [revision],
  )
  return (
    <LookupContext.Provider value={{ ...state, retry: () => setRevision((value) => value + 1) }}>
      {children}
    </LookupContext.Provider>
  )
}

