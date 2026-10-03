import { resolveApiAsset } from '../../../shared/api/httpClient'
import { getCategories } from '../../../shared/api/services/categoryApi'
import { getEntries, getDailyEntries } from '../../../shared/api/services/entryApi'
import type { ApiEntry, ApiDailyEntry } from '../../../shared/api/models/entry'
import type { Creator, LeaderboardRepository } from '../domain/creator'

export class ApiLeaderboardRepository implements LeaderboardRepository
{
  private async load(
    request: Promise<readonly (ApiEntry | ApiDailyEntry)[]>,
    daily = false,
  ): Promise<readonly Creator[]>
  {
    const [entries, categories] = await Promise.all([request, getCategories()])
    const ids = new Map(categories.map((category) => [category.name, category.id]))

    return entries.map((row) =>
    {
      const entry = 'entry' in row ? row.entry : row
      const score = 'dailyScore' in row ? row.dailyScore : entry.score
      const reachedDate = 'scoreReachedDate' in row ? row.scoreReachedDate : (entry.updatedDate ?? entry.createdDate)
      const [firstName, ...remainingName] = entry.name.trim().split(/\s+/)
      const categoryIds = entry.categories.map((category) => ids.get(category.name) ?? category.name)

      return {
        id: entry.id,
        username: entry.username,
        displayName: entry.name,
        firstName,
        lastName: remainingName.join(' '),
        category: categoryIds[0] ?? '',
        categories: categoryIds,
        imageUrl: resolveApiAsset(entry.imgUrl),
        socialProfiles: entry.socialMediaPlatforms.map((link, index) => ({
          id: `${entry.id}-social-${index}`,
          platform: link.platformName,
          url: link.url,
        })),
        totalContributed: score,
        dailyContributed: daily ? score : 0,
        scoreReachedAt: reachedDate,
        joinedAt: entry.createdDate,
      }
    })
  }

  getAll(): Promise<readonly Creator[]>
  {
    return this.load(getEntries())
  }

  getDaily(date: string): Promise<readonly Creator[]>
  {
    return this.load(getDailyEntries(date), true)
  }
}
