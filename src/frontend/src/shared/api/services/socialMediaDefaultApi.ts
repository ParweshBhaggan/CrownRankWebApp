import { apiGet } from '../apiRequest'
import { get_all_social_media_default_endpoint } from '../endpoints/socialMediaDefaultEndpoints'
import type { ApiSocialMediaDefault } from '../models/socialMediaDefault'

export function getSocialMediaDefaults(): Promise<readonly ApiSocialMediaDefault[]>
{
  return apiGet<readonly ApiSocialMediaDefault[]>(get_all_social_media_default_endpoint)
}
