import { apiRequest, resolveApiAsset } from '../../../shared/api/httpClient'
import type { Creator, LeaderboardRepository } from '../domain/creator'

export interface ApiCategory { readonly id: string; readonly name: string; readonly description?: string }
export interface ApiSocialMediaDefault { readonly id: string; readonly name: string }
interface ApiEntry {
  readonly id: string
  readonly name: string
  readonly username: string
  readonly imgUrl: string
  readonly score: number
  readonly createdDate: string
  readonly updatedDate?: string
  readonly categories: readonly { readonly name: string }[]
  readonly socialMediaPlatforms: readonly { readonly platformName: string; readonly url: string }[]
}
export function getCategories(): Promise<readonly ApiCategory[]> {
  return apiRequest('/api/Category')
}
export function getSocialMediaDefaults(): Promise<readonly ApiSocialMediaDefault[]> {
  return apiRequest('/api/SocialMediaDefault')
}
export class ApiLeaderboardRepository implements LeaderboardRepository {
  async getAll(): Promise<readonly Creator[]> {
    const [entries, categories] = await Promise.all([
      apiRequest<readonly ApiEntry[]>('/api/Entry'), getCategories(),
    ])
    const ids = new Map(categories.map(category => [category.name, category.id]))
    return entries.map(entry => {
      const [firstName, ...remainingName] = entry.name.trim().split(/\s+/)
      const categoryIds = entry.categories.map(category => ids.get(category.name) ?? category.name)
      return {
        id: entry.id, username: entry.username, displayName: entry.name,
        firstName, lastName: remainingName.join(' '), category: categoryIds[0] ?? '', categories: categoryIds,
        imageUrl: resolveApiAsset(entry.imgUrl),
        socialProfiles: entry.socialMediaPlatforms.map((link, index) => ({
          id: `${entry.id}-social-${index}`, platform: link.platformName, url: link.url,
        })),
        totalContributed: entry.score, dailyContributed: 0,
        scoreReachedAt: entry.updatedDate ?? entry.createdDate, joinedAt: entry.createdDate,
      }
    }).sort((a, b) => b.totalContributed - a.totalContributed || a.joinedAt.localeCompare(b.joinedAt) || a.id.localeCompare(b.id))
  }
  async getDaily(_date: string): Promise<readonly Creator[]> {
    void _date
    throw new Error('Daily rankings are not available in the current backend yet.')
  }
}
