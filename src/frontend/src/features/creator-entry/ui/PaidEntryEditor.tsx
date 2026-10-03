import { useState, type FormEvent } from 'react'
import { useLookups } from '../../../shared/config/useLookups'
import { getPendingEntry, savePendingEntry } from '../data/pendingEntry'
import { uploadProfileImage } from '../../../shared/api/services/profileImageApi'

export function PaidEntryEditor({ paymentId, onRetry }: { paymentId: string; onRetry: () => void })
{
  const { categories, platforms } = useLookups()
  const [entry, setEntry] = useState(() => getPendingEntry(paymentId)!)
  const [image, setImage] = useState<File>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>)
  {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      savePendingEntry(paymentId, { ...entry, imgUrl: image ? await uploadProfileImage(image, paymentId) : entry.imgUrl })
      onRetry()
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not save your entry details.')
    } finally { setBusy(false) }
  }

  return (
    <form onSubmit={submit}>
      <p>Your payment is saved. Correct your entry details and retry registration without paying again.</p>
      <label>Creator username
        <input required value={entry.username} onChange={event => setEntry({ ...entry, username: event.target.value.trim() })} />
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
      <label>Replace profile image (optional)
        <input type="file" accept="image/png,image/jpeg,image/webp" onChange={event => setImage(event.target.files?.[0])} />
      </label>
      <label>
        <input type="checkbox" required checked={entry.acceptedAgreements} onChange={event => setEntry({ ...entry, acceptedAgreements: event.target.checked })} />
        I accept the terms and privacy policy.
      </label>
      {error && <p role="alert">{error}</p>}
      <button className="primary-button" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save paid entry'}</button>
    </form>
  )
}
