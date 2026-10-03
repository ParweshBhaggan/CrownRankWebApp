import { postEntry, testPayment } from '../../../shared/api/services/entryApi'
import { getCategories } from '../../../shared/api/services/categoryApi'
import { getSocialMediaDefaults } from '../../../shared/api/services/socialMediaDefaultApi'
import { profileImageDataUrl } from './profileImage'
import type { RankingEntryDraft } from '../domain/rankingEntry'
import { redirectToUrl } from '../../../shared/api/redirect'

export async function createEntry(draft: RankingEntryDraft, entryReference: string): Promise<void>
{
  void entryReference
  if (!draft.profileImage) throw new Error('A profile image is required.')

  const [categories, platforms] = await Promise.all([getCategories(), getSocialMediaDefaults()])
  const selected = categories.find((category) => category.id === draft.category)
  if (!selected) throw new Error('The selected category is not currently available.')
  if (draft.socialLinks.some((link) => !platforms.some((platform) => platform.name === link.platform))) {
    throw new Error('A selected social platform is not currently available.')
  }

  await postEntry({
    name: draft.name.trim(),
    username: draft.username.trim(),
    imgUrl: await profileImageDataUrl(draft.profileImage),
    score: draft.contribution,
    categories: [{ name: selected.name, description: selected.description ?? '' }],
    socialMediaPlatforms: draft.socialLinks.map((link) => ({ platformName: link.platform, url: link.url.trim() })),
  })

 


}
