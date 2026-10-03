import { apiGet } from '../apiRequest'
import {
  get_all_entry_endpoint,
  daily_entry_endpoint,
} from '../endpoints/entryEndpoints'
import type { ApiEntry, ApiDailyEntry } from '../models/entry'

export function getEntries(): Promise<readonly ApiEntry[]>
{
  return apiGet<readonly ApiEntry[]>(get_all_entry_endpoint)
}

export function getDailyEntries(date: string): Promise<readonly ApiDailyEntry[]>
{
  return apiGet<readonly ApiDailyEntry[]>(daily_entry_endpoint(date))
}

