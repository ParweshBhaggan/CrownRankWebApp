import { parseAmount } from '../../../shared/format/currency.ts'
import type { RankingEntryDraft, RankingEntryValidationErrors } from '../domain/rankingEntry'

const usernamePattern = /^[a-zA-Z0-9._-]{2,40}$/
const acceptedImageTypes = new Set(['image/jpeg', 'image/png', 'image/webp', 'image/gif', 'image/bmp'])

export function validateRankingEntry(draft: RankingEntryDraft): RankingEntryValidationErrors {
  const errors: RankingEntryValidationErrors = {}
  if (draft.name.trim().length < 1 || draft.name.trim().length > 160) errors.name = 'Enter a name using no more than 160 characters.'
  if (!usernamePattern.test(draft.username.trim())) errors.username = 'Use 2–40 letters, numbers, dots, underscores, or dashes.'
  if (!Number.isFinite(parseAmount(String(draft.contribution)))) errors.contribution = 'Choose $1.00–$10,000.00 with at most two decimal places.'
  const linksAreValid = draft.socialLinks.length > 0 && draft.socialLinks.length <= 5 && draft.socialLinks.every((link) => {
    try { const url = new URL(link.url); const hosts: Record<string, string[]> = { instagram: ['instagram.com'], tiktok: ['tiktok.com'], youtube: ['youtube.com', 'youtu.be'], x: ['x.com', 'twitter.com'], twitch: ['twitch.tv'], onlyfans: ['onlyfans.com'] }; return url.protocol === 'https:' && Boolean(url.hostname) && !url.username && !url.password && link.url.length <= 500 && (link.platform === 'website' || hosts[link.platform]?.some(host => url.hostname === host || url.hostname.endsWith(`.${host}`))) } catch { return false }
  })
  const platformsAreUnique = new Set(draft.socialLinks.map(link => link.platform)).size === draft.socialLinks.length
  if (!linksAreValid || !platformsAreUnique) errors.socialLinks = 'Add 1–5 unique HTTPS links matching the selected platforms.'
  if (draft.profileImage && (!acceptedImageTypes.has(draft.profileImage.type) || draft.profileImage.size > 8 * 1024 * 1024)) errors.profileImage = 'Choose a JPG, PNG, WebP, GIF, or BMP image up to 8 MB.'
  return errors
}
