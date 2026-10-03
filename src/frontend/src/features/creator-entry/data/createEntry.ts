import { prepareEntry, startEntryCheckout } from '../../../shared/api/services/paymentApi'
import { getCategories } from '../../../shared/api/services/categoryApi'
import { getSocialMediaDefaults } from '../../../shared/api/services/socialMediaDefaultApi'
import { profileImageDataUrl } from './profileImage'
import type { RankingEntryDraft } from '../domain/rankingEntry'
import { continueToCheckout } from '../../payments/application'

export async function createEntry(draft: RankingEntryDraft, entryReference: string): Promise<void>
{
  if (!draft.profileImage) throw new Error('A profile image is required.')
  if (!draft.acceptedAgreements) throw new Error('Accept the terms and privacy policy before paying.')
  const [categories, platforms] = await Promise.all([getCategories(), getSocialMediaDefaults()])
  const selected = categories.find((category) => category.id === draft.category)
  if (!selected) throw new Error('The selected category is not currently available.')
  const socialProfiles = draft.socialLinks.map((link) =>
  {
    const platform = platforms.find((value) => value.name === link.platform)
    if (!platform) throw new Error('A selected social platform is not currently available.')
    return { platformId: platform.id, url: link.url.trim() }
  })
  const prepared = await prepareEntry({
    referenceId: entryReference, name: draft.name.trim(), username: draft.username.trim(),
    categoryId: selected.id, amount: draft.contribution, socialProfiles,
    imageDataUrl: await profileImageDataUrl(draft.profileImage), acceptedAgreements: draft.acceptedAgreements,
  })
  const session = await startEntryCheckout(prepared.id, { name: prepared.name, amount: prepared.amount })
  continueToCheckout(session)
}
