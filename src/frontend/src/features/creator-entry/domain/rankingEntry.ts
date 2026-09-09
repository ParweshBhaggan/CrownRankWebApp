import type { CreatorCategory, SocialPlatform } from '../../leaderboard/domain/creator'

export interface SocialLinkInput { readonly id: string; readonly platform: SocialPlatform; readonly url: string }
export interface RankingEntryDraft { readonly firstName: string; readonly lastName: string; readonly username: string; readonly category: CreatorCategory; readonly socialLinks: readonly SocialLinkInput[]; readonly profileImage?: File; readonly contribution: number }
export interface RankingEntryValidationErrors { firstName?: string; lastName?: string; username?: string; category?: string; contribution?: string; socialLinks?: string; profileImage?: string; terms?: string }

