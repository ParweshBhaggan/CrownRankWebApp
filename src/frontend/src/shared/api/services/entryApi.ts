import { apiGet, apiPost } from '../apiRequest'
import {
  get_all_entry_endpoint,
  daily_entry_endpoint,
  create_entry_endpoint,
} from '../endpoints/entryEndpoints'
import type { ApiEntry, ApiDailyEntry, PaidEntryRequest } from '../models/entry'

export function getEntries(): Promise<readonly ApiEntry[]>
{
  return apiGet<readonly ApiEntry[]>(get_all_entry_endpoint)
}

export function getDailyEntries(date: string): Promise<readonly ApiDailyEntry[]>
{
  return apiGet<readonly ApiDailyEntry[]>(daily_entry_endpoint(date))
}


import type { PaymentResponse } from '../models/payment'

export function postEntry(request: PaidEntryRequest, paymentId: string): Promise<PaymentResponse>
{
  return apiPost(create_entry_endpoint(paymentId), request)
}
