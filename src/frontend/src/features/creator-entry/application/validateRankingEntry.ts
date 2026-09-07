import type { RankingEntryDraft, RankingEntryValidationErrors } from '../domain/rankingEntry'

const usernamePattern = /^[a-zA-Z0-9._-]{2,40}$/
const acceptedImageTypes = new Set(['image/jpeg', 'image/png', 'image/webp'])

export function validateRankingEntry(draft: RankingEntryDraft): RankingEntryValidationErrors {
  const errors: RankingEntryValidationErrors = {}
  if (draft.firstName.trim().length < 2) errors.firstName = 'Enter your first name.'
  if (draft.lastName.trim().length < 2) errors.lastName = 'Enter your last name.'
  if (!usernamePattern.test(draft.username.trim())) errors.username = 'Use 2–40 letters, numbers, dots, underscores, or dashes.'
  if (!Number.isInteger(draft.contributionCents) || draft.contributionCents < 100) errors.contribution = 'Enter an amount of at least $1.00.'
  const linksAreValid = draft.socialLinks.length > 0 && draft.socialLinks.every((link) => {
    try { const url = new URL(link.url); return url.protocol === 'https:' && Boolean(url.hostname) } catch { return false }
  })
  if (!linksAreValid) errors.socialLinks = 'Add at least one complete HTTPS social profile URL.'
  if (draft.profileImage && (!acceptedImageTypes.has(draft.profileImage.type) || draft.profileImage.size > 5 * 1024 * 1024)) errors.profileImage = 'Choose a JPG, PNG, or WebP image up to 5 MB.'
  return errors
}
