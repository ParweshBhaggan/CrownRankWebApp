export type SocialPlatform = 'instagram' | 'tiktok' | 'youtube' | 'x' | 'twitch' | 'onlyfans' | 'website'

export interface SocialProfile {
  readonly id: string
  readonly platform: SocialPlatform
  readonly url: string
}

export interface Creator {
  readonly id: string
  readonly username: string
  readonly displayName: string
  readonly imageUrl: string
  readonly socialProfiles: readonly SocialProfile[]
  readonly totalContributedCents: number
  readonly supporterCount: number
  readonly joinedAt: string
}

export interface RankedCreator extends Creator {
  readonly rank: number
  readonly previousRank?: number
}

export interface LeaderboardRepository {
  getAll(): Promise<readonly Creator[]>
}

