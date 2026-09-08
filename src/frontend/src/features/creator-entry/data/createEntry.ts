import { apiRequest } from '../../../shared/api/httpClient'
import type { Creator } from '../../leaderboard/domain/creator'
import type { RankingEntryDraft } from '../domain/rankingEntry'

export async function createEntry(draft: RankingEntryDraft, entryReference: string): Promise<Creator> {
  const form = new FormData()
  form.set('entryReference', entryReference)
  form.set('firstName', draft.firstName.trim())
  form.set('lastName', draft.lastName.trim())
  form.set('username', draft.username.trim())
  form.set('category', draft.category)
  form.set('initialAmount', draft.contribution.toFixed(2))
  form.set('socialProfilesJson', JSON.stringify(draft.socialLinks.map(({ platform, url }) => ({ platform, url: url.trim() }))))
  if (draft.profileImage) form.set('image', draft.profileImage)
  return apiRequest<Creator>('/api/creators', { method: 'POST', body: form })
}
