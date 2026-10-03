export const get_all_social_media_default_endpoint = '/api/SocialMediaDefault'

export function get_by_id_social_media_default_endpoint(id: string): string
{
  return `/api/SocialMediaDefault/${encodeURIComponent(id)}`
}

export const create_social_media_default_endpoint = '/api/SocialMediaDefault'

export function update_social_media_default_endpoint(id: string): string
{
  return `/api/SocialMediaDefault/${encodeURIComponent(id)}`
}

export function delete_social_media_default_endpoint(id: string): string
{
  return `/api/SocialMediaDefault/${encodeURIComponent(id)}`
}
