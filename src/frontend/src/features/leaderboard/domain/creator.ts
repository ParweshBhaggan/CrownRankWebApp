export type SocialPlatform = string

export interface SocialProfile {
  readonly id: string
  readonly platform: SocialPlatform
  readonly url: string
}

export interface Creator {
  readonly id: string
  readonly username: string
  readonly firstName: string
  readonly lastName: string
  readonly displayName: string
  readonly category: CreatorCategory
  readonly categories?: readonly CreatorCategory[]
  readonly imageUrl: string
  readonly socialProfiles: readonly SocialProfile[]
  readonly totalContributed: number
  readonly dailyContributed: number
  readonly scoreReachedAt: string
  readonly joinedAt: string
}

export type CreatorCategory = string

export interface RankedCreator extends Creator {
  readonly rank: number
  readonly previousRank?: number
}

export interface LeaderboardRepository {
  getAll(): Promise<readonly Creator[]>
  getDaily(date: string): Promise<readonly Creator[]>
}

