import { apiRequest } from '../../../shared/api/httpClient'
import { categorySlug, getCategories } from '../../leaderboard/data/ApiLeaderboardRepository'
import type { RankingEntryDraft } from '../domain/rankingEntry'

interface PaymentStart {
  readonly attemptId: string
  readonly entryId: string
  readonly checkoutUrl: string
}

const platformNames: Record<string, string> = {
  instagram: 'Instagram',
  tiktok: 'TikTok',
  youtube: 'YouTube',
  onlyfans: 'OnlyFans',
  facebook: 'Facebook',
  x: 'X',
  twitch: 'Twitch',
  website: 'Website',
  other: 'Other',
}

export async function createEntry(draft: RankingEntryDraft, _entryReference: string): Promise<void> {
  if (!draft.profileImage) throw new Error('A profile image is required.')
  const categories = await getCategories()
  const selected = categories.find(category => categorySlug(category) === draft.category)
  if (!selected) throw new Error('The selected category is not currently available.')

  const form = new FormData()
  form.set('name', `${draft.firstName.trim()} ${draft.lastName.trim()}`.trim())
  form.set('username', draft.username.trim())
  form.set('categoryId', selected.id)
  form.set('acceptedAgreements', 'true')
  form.set('amountInMinorUnits', String(Math.round(draft.contribution * 100)))
  form.set('currency', 'EUR')
  form.set('socialLinks', JSON.stringify(draft.socialLinks.map(({ platform, url }) => ({
    platform: platformNames[platform],
    url: url.trim(),
  }))))
  form.set('image', draft.profileImage)

  const payment = await apiRequest<PaymentStart>('/api/entries', { method: 'POST', body: form })
  await apiRequest<void>(`/api/dev/payments/${payment.attemptId}/outcome`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ outcome: 'Succeeded' }),
  })
  await apiRequest(`/api/payments/${payment.attemptId}/confirm`, { method: 'POST' })
}
