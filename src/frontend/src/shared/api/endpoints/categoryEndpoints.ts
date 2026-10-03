export const get_all_category_endpoint = '/api/Category'

export function get_by_id_category_endpoint(id: string): string
{
  return `/api/Category/${encodeURIComponent(id)}`
}

export function get_by_name_category_endpoint(name: string): string
{
  return `/api/Category/name/${encodeURIComponent(name)}`
}

export const create_category_endpoint = '/api/Category'

export function update_category_endpoint(id: string): string
{
  return `/api/Category/${encodeURIComponent(id)}`
}

export function delete_category_endpoint(id: string): string
{
  return `/api/Category/${encodeURIComponent(id)}`
}
