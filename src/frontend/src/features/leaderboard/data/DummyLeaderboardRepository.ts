import type { Creator, LeaderboardRepository } from '../domain/creator'
import { dummyCreators } from './dummyCreators'

export class DummyLeaderboardRepository implements LeaderboardRepository {
  async getAll(): Promise<readonly Creator[]> {
    return Promise.resolve(dummyCreators)
  }
}

