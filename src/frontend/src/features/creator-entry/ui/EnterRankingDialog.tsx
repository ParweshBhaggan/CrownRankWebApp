import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from 'react'
import { creatorCategories, creatorCategoryLabels, type CreatorCategory, type SocialPlatform } from '../../leaderboard/domain/creator'
import type { PaymentGateway } from '../../payments/domain/payment'
import { formatCurrency } from '../../../shared/format/currency'
import { validateRankingEntry } from '../application/validateRankingEntry'
import type { RankingEntryValidationErrors, SocialLinkInput } from '../domain/rankingEntry'

const platforms: readonly { value: SocialPlatform; label: string }[] = [
  { value: 'instagram', label: 'Instagram' }, { value: 'tiktok', label: 'TikTok' },
  { value: 'youtube', label: 'YouTube' }, { value: 'x', label: 'X' },
  { value: 'twitch', label: 'Twitch' }, { value: 'onlyfans', label: 'OnlyFans' },
  { value: 'website', label: 'Website' },
]

interface Props { readonly isOpen: boolean; readonly paymentGateway: PaymentGateway; readonly onClose: () => void }
const createSocialLink = (): SocialLinkInput => ({ id: crypto.randomUUID(), platform: 'instagram', url: '' })

export function EnterRankingDialog({ isOpen, paymentGateway, onClose }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [username, setUsername] = useState('')
  const [category, setCategory] = useState<CreatorCategory>('influencer')
  const [amount, setAmount] = useState('25')
  const [socialLinks, setSocialLinks] = useState<SocialLinkInput[]>([createSocialLink()])
  const [profileImage, setProfileImage] = useState<File>()
  const [previewUrl, setPreviewUrl] = useState<string>()
  const [acceptedTerms, setAcceptedTerms] = useState(false)
  const [errors, setErrors] = useState<RankingEntryValidationErrors>({})
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [checkoutReady, setCheckoutReady] = useState(false)

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    if (isOpen && !dialog.open) dialog.showModal()
    if (!isOpen && dialog.open) dialog.close()
  }, [isOpen])

  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl) }, [previewUrl])
  const closeDialog = () => { if (!isSubmitting) onClose() }
  const updateSocialLink = (id: string, patch: Partial<SocialLinkInput>) => setSocialLinks((current) => current.map((link) => link.id === id ? { ...link, ...patch } : link))

  function handleImageChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (previewUrl) URL.revokeObjectURL(previewUrl)
    setProfileImage(file)
    setPreviewUrl(file ? URL.createObjectURL(file) : undefined)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const contributionCents = Math.round(Number(amount) * 100)
    const validationErrors = validateRankingEntry({ firstName, lastName, username, category, socialLinks, profileImage, contributionCents })
    if (!acceptedTerms) validationErrors.terms = 'Confirm the declaration before continuing.'
    setErrors(validationErrors)
    if (Object.keys(validationErrors).length > 0) return

    setIsSubmitting(true)
    try {
      await paymentGateway.createCheckout({
        referenceId: crypto.randomUUID(), purpose: 'ranking-entry', money: { amountCents: contributionCents, currency: 'USD' },
        successUrl: window.location.origin, cancelUrl: window.location.href, metadata: { username: username.trim(), firstName: firstName.trim(), lastName: lastName.trim(), category },
      })
      setCheckoutReady(true)
    } finally { setIsSubmitting(false) }
  }

  return (
    <dialog ref={dialogRef} className="entry-dialog" onCancel={(event) => { event.preventDefault(); closeDialog() }} onClose={onClose}>
      <div className="dialog-shell">
        <div className="dialog-topbar"><a className="brand" href="/">CrownRank</a><button className="icon-button" type="button" aria-label="Close dialog" onClick={closeDialog}>×</button></div>
        {checkoutReady ? (
          <div className="success-state" role="status"><span className="success-mark">✓</span><p className="eyebrow">Checkout prepared</p><h2>You’re ready for the crown.</h2><p>In production, Stripe Checkout opens here. Your ranking is only activated after the payment webhook is verified.</p><button className="primary-button" type="button" onClick={onClose}>Back to leaderboard</button></div>
        ) : (
          <form onSubmit={handleSubmit} noValidate>
            <header className="dialog-heading"><p className="eyebrow">Enter the ranking</p><h2>Put your name on the board.</h2><p>No account needed. Choose your amount, add your creator profile, and continue to secure checkout.</p></header>
            <div className="form-layout">
              <div className="form-main">
                <div className="field-grid">
                  <div className="field-group"><label htmlFor="first-name">First name</label><input id="first-name" value={firstName} onChange={(event) => setFirstName(event.target.value)} autoComplete="given-name" />{errors.firstName && <p className="field-error">{errors.firstName}</p>}</div>
                  <div className="field-group"><label htmlFor="last-name">Last name</label><input id="last-name" value={lastName} onChange={(event) => setLastName(event.target.value)} autoComplete="family-name" />{errors.lastName && <p className="field-error">{errors.lastName}</p>}</div>
                </div>
                <div className="field-group"><label htmlFor="username">Creator username</label><div className="input-prefix"><span>@</span><input id="username" value={username} onChange={(event) => setUsername(event.target.value)} placeholder="yourhandle" autoComplete="username" aria-describedby={errors.username ? 'username-error' : undefined} /></div>{errors.username && <p className="field-error" id="username-error">{errors.username}</p>}</div>
                <div className="field-group"><label htmlFor="category">Creator category</label><select id="category" value={category} onChange={(event) => setCategory(event.target.value as CreatorCategory)}>{creatorCategories.map((value) => <option key={value} value={value}>{creatorCategoryLabels[value]}</option>)}</select><p className="field-hint">Choose the category that best represents your primary content.</p></div>
                <fieldset className="field-group"><legend>Social profiles</legend><p className="field-hint">At least one public creator profile is required.</p><div className="social-fields">{socialLinks.map((link, index) => <div className="social-row" key={link.id}><select aria-label={`Platform ${index + 1}`} value={link.platform} onChange={(event) => updateSocialLink(link.id, { platform: event.target.value as SocialPlatform })}>{platforms.map((platform) => <option key={platform.value} value={platform.value}>{platform.label}</option>)}</select><input type="url" aria-label={`Social profile URL ${index + 1}`} value={link.url} onChange={(event) => updateSocialLink(link.id, { url: event.target.value })} placeholder="https://..." inputMode="url" />{socialLinks.length > 1 && <button className="remove-button" type="button" aria-label={`Remove social profile ${index + 1}`} onClick={() => setSocialLinks((current) => current.filter((item) => item.id !== link.id))}>×</button>}</div>)}</div>{socialLinks.length < 5 && <button className="text-button" type="button" onClick={() => setSocialLinks((current) => [...current, createSocialLink()])}>+ Add another profile</button>}{errors.socialLinks && <p className="field-error">{errors.socialLinks}</p>}</fieldset>
                <div className="field-group"><label htmlFor="profile-image">Profile image <span className="optional">Optional</span></label><label className="upload-field" htmlFor="profile-image">{previewUrl ? <img src={previewUrl} alt="Selected profile preview" /> : <span className="upload-icon">↥</span>}<span><strong>{profileImage?.name ?? 'Choose an image'}</strong><small>JPG, PNG, WebP, GIF or BMP · Max 8 MB</small></span></label><input className="visually-hidden" id="profile-image" type="file" accept="image/jpeg,image/png,image/webp,image/gif,image/bmp" onChange={handleImageChange} />{errors.profileImage && <p className="field-error">{errors.profileImage}</p>}</div>
              </div>
              <aside className="contribution-card"><p className="eyebrow">Your opening contribution</p><label htmlFor="amount">Choose your amount</label><div className="amount-input"><span>$</span><input id="amount" type="number" min="1" step="1" value={amount} onChange={(event) => setAmount(event.target.value)} inputMode="decimal" /></div><div className="quick-amounts" aria-label="Suggested amounts">{[10, 25, 50, 100].map((value) => <button className={amount === String(value) ? 'selected' : ''} key={value} type="button" onClick={() => setAmount(String(value))}>${value}</button>)}</div>{errors.contribution && <p className="field-error">{errors.contribution}</p>}<div className="rank-note"><span>♛</span><p><strong>Every amount counts</strong>Your contribution becomes your starting score. Fans can boost it later.</p></div><label className="check-row"><input type="checkbox" checked={acceptedTerms} onChange={(event) => setAcceptedTerms(event.target.checked)} /><span>I confirm I own or represent this creator profile and accept the platform terms.</span></label>{errors.terms && <p className="field-error">{errors.terms}</p>}<button className="primary-button full" type="submit" disabled={isSubmitting}>{isSubmitting ? 'Preparing checkout…' : `Continue with ${formatCurrency(Math.max(0, Math.round(Number(amount || 0) * 100)))}`}</button><p className="secure-note">Secure payment handled by Stripe at launch.</p></aside>
            </div>
          </form>
        )}
      </div>
    </dialog>
  )
}
