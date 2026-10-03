import { startEntryCheckout } from '../../../shared/api/services/paymentApi'
import { getCategories } from '../../../shared/api/services/categoryApi'
import { getSocialMediaDefaults } from '../../../shared/api/services/socialMediaDefaultApi'
import { profileImageDataUrl } from './profileImage'
import type { RankingEntryDraft } from '../domain/rankingEntry'
import { savePendingEntry } from './pendingEntry'
import { continueToCheckout } from '../../payments/application'

export async function createEntry(draft: RankingEntryDraft, entryReference: string): Promise<void>
{
  if (!draft.profileImage) throw new Error('A profile image is required.')
  if (!draft.acceptedAgreements) throw new Error('Accept the terms and privacy policy before paying.')
  const [categories, platforms] = await Promise.all([getCategories(), getSocialMediaDefaults()])
  const selected = categories.find((category) => category.id === draft.category)
  if (!selected) throw new Error('The selected category is not currently available.')
  if (draft.socialLinks.some(link => !platforms.some(platform => platform.name === link.platform)))
  {
    throw new Error('A selected social platform is not currently available.')
  }
  savePendingEntry(entryReference, {
    name: draft.name.trim(), username: draft.username.trim(),
    categories: [{ name: selected.name, description: selected.description ?? '' }],
    socialMediaPlatforms: draft.socialLinks.map(link => ({ platformName: link.platform, url: link.url.trim() })),
    score: draft.contribution, imgUrl: await profileImageDataUrl(draft.profileImage), acceptedAgreements: draft.acceptedAgreements,
  })
  const session = await startEntryCheckout(entryReference, { name: draft.name.trim(), amount: draft.contribution })
  continueToCheckout(session)
}
