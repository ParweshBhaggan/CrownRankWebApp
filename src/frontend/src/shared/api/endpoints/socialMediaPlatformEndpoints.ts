export const get_all_social_media_platform_endpoint = '/api/SocialMediaPlatform'

export function get_by_id_social_media_platform_endpoint(id: string): string
{
  return `/api/SocialMediaPlatform/${encodeURIComponent(id)}`
}

export const create_social_media_platform_endpoint = '/api/SocialMediaPlatform'

export function delete_social_media_platform_endpoint(id: string): string
{
  return `/api/SocialMediaPlatform/${encodeURIComponent(id)}`
}
