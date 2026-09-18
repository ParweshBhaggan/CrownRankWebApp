import { apiRequest, resolveApiAsset } from '../../../shared/api/httpClient'
import {
  creatorCategories,
  creatorCategoryLabels,
  type Creator,
  type CreatorCategory,
  type LeaderboardRepository,
  type SocialPlatform,
} from '../domain/creator'

export interface ApiCategory {
  readonly id: string
  readonly name: string
  readonly description?: string
}

interface ApiLeaderboardRow {
  readonly entryId: string
  readonly name: string
  readonly username: string
  readonly categoryId: string
  readonly scoreInMinorUnits: number
  readonly currency: string
  readonly scoreReachedAtUtc: string
  readonly rank: number
}

interface ApiEntry {
  readonly id: string
  readonly name: string
  readonly username: string
  readonly categoryId: string
  readonly imageUrl: string
  readonly socialLinks: readonly {
    readonly platform: string
    readonly url: string
    readonly customPlatformName?: string
  }[]
}

export async function getCategories(): Promise<readonly ApiCategory[]> {
  return apiRequest<readonly ApiCategory[]>('/api/categories')
}

export function categorySlug(category: ApiCategory): CreatorCategory {
  const byLabel = creatorCategories.find(
    slug => creatorCategoryLabels[slug].toLowerCase() === category.name.toLowerCase(),
  )
  if (byLabel) return byLabel
  const normalized = category.name.toLowerCase().replace(/&/g, 'and').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')
  return creatorCategories.find(slug => slug === normalized) ?? 'other'
}

async function loadLeaderboard(path: string): Promise<readonly Creator[]> {
  const [rows, categories] = await Promise.all([
    apiRequest<readonly ApiLeaderboardRow[]>(path),
    getCategories(),
  ])
  const categoryById = new Map(categories.map(category => [category.id, categorySlug(category)]))
  return Promise.all(rows.map(async row => {
    const profile = await apiRequest<ApiEntry>(`/api/entries/${row.entryId}`)
    const [firstName, ...remainingName] = profile.name.trim().split(/\s+/)
    return {
      id: row.entryId,
      username: row.username,
      firstName,
      lastName: remainingName.join(' '),
      displayName: row.name,
      category: categoryById.get(row.categoryId) ?? 'other',
      imageUrl: resolveApiAsset(profile.imageUrl),
      socialProfiles: profile.socialLinks.map((link, index) => ({
        id: `${row.entryId}-social-${index}`,
        platform: link.platform.toLowerCase() as SocialPlatform,
        url: link.url,
      })),
      totalContributed: row.scoreInMinorUnits / 100,
      dailyContributed: row.scoreInMinorUnits / 100,
      scoreReachedAt: row.scoreReachedAtUtc,
      joinedAt: row.scoreReachedAtUtc,
    }
  }))
}

export class ApiLeaderboardRepository implements LeaderboardRepository {
  getAll(): Promise<readonly Creator[]> {
    return loadLeaderboard('/api/leaderboards/global')
  }

  getDaily(date: string): Promise<readonly Creator[]> {
    return loadLeaderboard(`/api/leaderboards/daily?date=${encodeURIComponent(date)}`)
  }
}
