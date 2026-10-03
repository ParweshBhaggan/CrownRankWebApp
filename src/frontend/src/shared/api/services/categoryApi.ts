import { apiGet } from '../apiRequest'
import { get_all_category_endpoint } from '../endpoints/categoryEndpoints'
import type { ApiCategory } from '../models/category'

export function getCategories(): Promise<readonly ApiCategory[]>
{
  return apiGet<readonly ApiCategory[]>(get_all_category_endpoint)
}
