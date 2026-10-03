import { apiGet, apiPost } from '../apiRequest'
import {
  get_all_entry_endpoint,
  daily_entry_endpoint,
  create_entry_endpoint,
  boost_entry_endpoint,
} from '../endpoints/entryEndpoints'
import type { ApiEntry, ApiDailyEntry, CreateEntryRequest, BoostEntryRequest } from '../models/entry'

export function getEntries(): Promise<readonly ApiEntry[]>
{
  return apiGet<readonly ApiEntry[]>(get_all_entry_endpoint)
}

export function getDailyEntries(date: string): Promise<readonly ApiDailyEntry[]>
{
  return apiGet<readonly ApiDailyEntry[]>(daily_entry_endpoint(date))
}

export function postEntry(request: CreateEntryRequest, paymentId: string): Promise<ApiEntry>
{
  return apiPost<ApiEntry>(`${create_entry_endpoint}?paymentId=${encodeURIComponent(paymentId)}`, request)
}


export function boostEntry(id: string, request: BoostEntryRequest, paymentId: string): Promise<ApiEntry>
{
  return apiPost<ApiEntry>(`${boost_entry_endpoint(id)}?paymentId=${encodeURIComponent(paymentId)}`, request)
}

