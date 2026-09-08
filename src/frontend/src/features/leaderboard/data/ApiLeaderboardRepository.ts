import { apiRequest, resolveApiAsset } from '../../../shared/api/httpClient'
import type { Creator, LeaderboardRepository } from '../domain/creator'

export class ApiLeaderboardRepository implements LeaderboardRepository {
  async getAll(): Promise<readonly Creator[]> {
    const creators = await apiRequest<readonly Creator[]>('/api/creators')
    return creators.map(creator => ({ ...creator, imageUrl: resolveApiAsset(creator.imageUrl) }))
  }

  async getDaily(date: string): Promise<readonly Creator[]> {
    const creators = await apiRequest<readonly Creator[]>(`/api/rankings/daily/${date}`)
    return creators.map(creator => ({ ...creator, imageUrl: resolveApiAsset(creator.imageUrl) }))
  }
}
