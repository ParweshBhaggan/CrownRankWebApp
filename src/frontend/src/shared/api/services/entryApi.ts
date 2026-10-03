import { apiGet, apiPost, testApiPost } from '../apiRequest'
import {
  get_all_entry_endpoint,
  daily_entry_endpoint,
  create_entry_endpoint,
  boost_entry_endpoint,
} from '../endpoints/entryEndpoints'
import type { ApiEntry, ApiDailyEntry, CreateEntryRequest, BoostEntryRequest, TestPaymentRequest, TestPaymentResponse } from '../models/entry'

export function getEntries(): Promise<readonly ApiEntry[]>
{
  return apiGet<readonly ApiEntry[]>(get_all_entry_endpoint)
}

export function getDailyEntries(date: string): Promise<readonly ApiDailyEntry[]>
{
  return apiGet<readonly ApiDailyEntry[]>(daily_entry_endpoint(date))
}

export function postEntry(request: CreateEntryRequest): Promise<ApiEntry>
{
  return apiPost<ApiEntry>(create_entry_endpoint, request)
}


export function boostEntry(id: string, request: BoostEntryRequest): Promise<ApiEntry>
{
  return apiPost<ApiEntry>(boost_entry_endpoint(id), request)
}

export function testPayment(request: TestPaymentRequest): Promise<TestPaymentResponse>
{
  
  return testApiPost<TestPaymentResponse>('/api/checkout', request)
}
