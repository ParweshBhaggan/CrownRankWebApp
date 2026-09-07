import type { SocialPlatform } from '../../leaderboard/domain/creator'

export interface SocialLinkInput { readonly id: string; readonly platform: SocialPlatform; readonly url: string }
export interface RankingEntryDraft { readonly username: string; readonly socialLinks: readonly SocialLinkInput[]; readonly profileImage?: File; readonly contributionCents: number }
export interface RankingEntryValidationErrors { username?: string; contribution?: string; socialLinks?: string; profileImage?: string; terms?: string }

