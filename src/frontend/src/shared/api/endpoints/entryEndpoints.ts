export const get_all_entry_endpoint = '/api/Entry'

export function get_by_id_entry_endpoint(id: string): string
{
  return `/api/Entry/${encodeURIComponent(id)}`
}

export const create_entry_endpoint = '/api/Entry'

export function delete_entry_endpoint(id: string): string
{
  return `/api/Entry/${encodeURIComponent(id)}`
}

export function boost_entry_endpoint(id: string): string
{
  return `/api/Entry/${encodeURIComponent(id)}/boost`
}

export function daily_entry_endpoint(date: string): string
{
  return `/api/Entry/daily?date=${encodeURIComponent(date)}`
}
