export type SocialPlatform = 'instagram' | 'tiktok' | 'youtube' | 'x' | 'twitch' | 'onlyfans' | 'website'

export interface SocialProfile {
  readonly id: string
  readonly platform: SocialPlatform
  readonly url: string
}

export interface Creator {
  readonly id: string
  readonly username: string
  readonly name: string
  readonly category: CreatorCategory
  readonly imageUrl: string
  readonly socialProfiles: readonly SocialProfile[]
  readonly totalContributed: number
  readonly dailyContributed: number
  readonly scoreReachedAt: string
  readonly joinedAt: string
}

export const creatorCategories = ['streamer', 'gaming', 'influencer', 'adult-entertainment', 'beauty-fashion', 'fitness-wellness', 'music', 'podcasting', 'education', 'comedy', 'art-design', 'food', 'travel', 'technology', 'business', 'other'] as const
export type CreatorCategory = typeof creatorCategories[number]
export const creatorCategoryLabels: Record<CreatorCategory, string> = {
  streamer: 'Streamer', gaming: 'Gaming', influencer: 'Influencer', 'adult-entertainment': 'Adult entertainment',
  'beauty-fashion': 'Beauty & fashion', 'fitness-wellness': 'Fitness & wellness', music: 'Music', podcasting: 'Podcasting',
  education: 'Education', comedy: 'Comedy', 'art-design': 'Art & design', food: 'Food', travel: 'Travel',
  technology: 'Technology', business: 'Business & finance', other: 'Other',
}

export interface RankedCreator extends Creator {
  readonly rank: number
  readonly previousRank?: number
}

export interface LeaderboardRepository {
  getAll(): Promise<readonly Creator[]>
  getDaily(date: string): Promise<readonly Creator[]>
}
