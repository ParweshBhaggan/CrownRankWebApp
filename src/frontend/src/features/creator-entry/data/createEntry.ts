import { apiRequest } from '../../../shared/api/httpClient'
import type { CheckoutSession } from '../../payments/domain/payment'
import type { RankingEntryDraft } from '../domain/rankingEntry'

export interface EntryCheckoutResult { readonly creatorId: string; readonly session: CheckoutSession }

export async function createEntry(draft: RankingEntryDraft, entryReference: string): Promise<EntryCheckoutResult> {
  const form = new FormData()
  form.set('entryReference', entryReference)
  form.set('name', draft.name.trim())
  form.set('username', draft.username.trim())
  form.set('category', draft.category)
  form.set('initialAmount', draft.contribution.toFixed(2))
  form.set('socialProfilesJson', JSON.stringify(draft.socialLinks.map(({ platform, url }) => ({ platform, url: url.trim() }))))
  if (draft.profileImage) form.set('image', draft.profileImage)
  return apiRequest<EntryCheckoutResult>('/api/creators', { method: 'POST', body: form })
}
