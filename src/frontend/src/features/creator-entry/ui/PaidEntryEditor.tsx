import { useState, type FormEvent } from 'react'
import { useLookups } from '../../../shared/config/useLookups'
import { getPendingEntry, savePendingEntry } from '../data/pendingEntry'
import { profileImageDataUrl } from '../data/profileImage'
import type { PaymentResponse } from '../../../shared/api/models/payment'
import type { CreateEntryRequest } from '../../../shared/api/models/entry'

export function PaidEntryEditor({ paymentId, payment, onRetry }: { paymentId: string; payment: PaymentResponse; onRetry: () => void })
{
  const { categories, platforms, loading, error: optionsError } = useLookups()
  const [entry, setEntry] = useState<CreateEntryRequest>(() => getPendingEntry(paymentId) ?? {
    name: payment.name ?? '', username: '', imgUrl: '', score: payment.amount,
    categories: [], socialMediaPlatforms: [{ platformName: '', url: '' }],
  })
  const [image, setImage] = useState<File>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>)
  {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      if (loading || optionsError) throw new Error('Wait for available categories and social platforms, then retry.')
      if (!entry.imgUrl && !image) throw new Error('Choose a profile image to finish your paid entry.')
      if (image && (!['image/jpeg', 'image/png', 'image/webp'].includes(image.type) || image.size > 5 * 1024 * 1024))
        throw new Error('Choose a JPG, PNG, or WebP image up to 5 MB.')
      savePendingEntry(paymentId, { ...entry, imgUrl: image ? await profileImageDataUrl(image) : entry.imgUrl })
      onRetry()
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not save your entry details.')
    } finally { setBusy(false) }
  }

  return (
    <form onSubmit={submit}>
      <p>Your payment is saved. Correct your entry details and retry registration without paying again.</p>
      {!entry.imgUrl && <p>The original browser form is unavailable. Re-enter your details to use this existing paid checkout.</p>}
      <label>Paid creator name
        <input required maxLength={100} readOnly={Boolean(payment.name)} value={entry.name} onChange={event => setEntry({ ...entry, name: event.target.value })} />
      </label>
      <label>Creator username
        <input required maxLength={40} value={entry.username} onChange={event => setEntry({ ...entry, username: event.target.value.trim() })} />
      </label>
      <label>Creator category
        <select required value={entry.categories[0]?.name ?? ''} onChange={event => setEntry({ ...entry, categories: [{ name: event.target.value }] })}>
          <option value="">Choose a category</option>
          {categories.map(category => <option key={category.id} value={category.name}>{category.name}</option>)}
        </select>
      </label>
      {entry.socialMediaPlatforms.map((profile, index) => (
        <fieldset key={index}>
          <label>Social platform {index + 1}
            <select required value={profile.platformName} onChange={event => setEntry({ ...entry, socialMediaPlatforms: entry.socialMediaPlatforms.map((value, position) =>
              position === index ? { ...value, platformName: event.target.value } : value) })}>
              <option value="">Choose a platform</option>
              {platforms.map(platform => <option key={platform.id} value={platform.name}>{platform.name}</option>)}
            </select>
          </label>
          <label>Social profile URL {index + 1}
            <input type="url" required value={profile.url} onChange={event => setEntry({ ...entry, socialMediaPlatforms: entry.socialMediaPlatforms.map((value, position) =>
              position === index ? { ...value, url: event.target.value } : value) })} />
          </label>
        </fieldset>
      ))}
      <label>{entry.imgUrl ? 'Replace profile image (optional)' : 'Profile image'}
        <input type="file" required={!entry.imgUrl && !image} accept="image/png,image/jpeg,image/webp" onChange={event => setImage(event.target.files?.[0])} />
      </label>
      {!entry.imgUrl && <label>
        <input type="checkbox" required /> I accept the terms and privacy policy.
      </label>}
      {optionsError && <p role="alert">{optionsError}</p>}
      {error && <p role="alert">{error}</p>}
      <button className="primary-button" disabled={busy || loading || Boolean(optionsError)} type="submit">{busy ? 'Saving…' : 'Save paid entry'}</button>
    </form>
  )
}
