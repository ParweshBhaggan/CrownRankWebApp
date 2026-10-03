import { useEffect, useRef, useState, type FormEvent } from 'react'
import type { Creator } from '../../leaderboard/domain/creator'
import type { PaymentGateway } from '../domain/payment'
import { formatCurrency, parseAmount, amountRange } from '../../../shared/format/currency'

import { useLookups } from '../../../shared/config/useLookups'
import { continueToCheckout } from '../application'

interface Props {
  creator: Creator
  gateway: PaymentGateway
  onClose: () => void
  onConfirmed: () => void
  available?: boolean
}

export function BoostDialog({ creator, gateway, onClose, available = true }: Props)
{
  const { paymentSettings, loading, error: settingsError } = useLookups()
  const ref = useRef<HTMLDialogElement>(null)
  const reference = useRef(crypto.randomUUID())
  const submitting = useRef(false)
  const [submittedAmount, setSubmittedAmount] = useState<number>()
  const [amount, setAmount] = useState('25')
  const [ready, setReady] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  useEffect(() =>
  {
    ref.current?.showModal()
  }, [])
  const close = () =>
  {
    if (!submitting.current) onClose()
  }
  async function submit(event: FormEvent)
  {
    event.preventDefault()
    if (submitting.current || !available || loading || settingsError) return
    const value = submittedAmount ?? parseAmount(amount)
    if (!Number.isFinite(value)) {
      setError(`Choose ${amountRange(paymentSettings)} with at most two decimal places.`)
      return
    }
    submitting.current = true
    setSubmittedAmount(value)
    setBusy(true)
    setError('')
    try {
      const session = await gateway.createCheckout({
        referenceId: reference.current,
        creatorId: creator.id,
        amount: value,
      })
      continueToCheckout(session)
      setReady(true)
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not save your Boost. Please retry.')
    } finally {
      submitting.current = false
      setBusy(false)
    }
  }
  return (
    <dialog
      ref={ref}
      className="boost-dialog"
      onClose={close}
      onCancel={(e) =>
      {
        e.preventDefault()
        close()
      }}
    >
      <button className="icon-button dialog-close" onClick={close} disabled={busy} aria-label="Close">
        ×
      </button>
      {ready ? (
        <div className="boost-success" role="status">
          <span>✓</span>
          <h2>Checkout ready</h2>
          <p>Stripe checkout is opening. The score updates after successful payment.</p>
          <button className="primary-button" onClick={close}>
            Done
          </button>
        </div>
      ) : (
        <form onSubmit={submit} noValidate>
          <p className="eyebrow">Boost this creator</p>
          <div className="boost-person">
            <img src={creator.imageUrl} alt="" />
            <div>
              <h2>{creator.displayName}</h2>
              <p>@{creator.username}</p>
            </div>
          </div>
          {!available && <p role="status">Boosting is not available in the current backend yet.</p>}
          <label htmlFor="boost-amount">Boost amount</label>
          <div className="amount-input">
            <span>{paymentSettings.currency.toUpperCase()}</span>
            <input
              id="boost-amount"
              type="number"
              min={paymentSettings.minimumAmount}
              max={paymentSettings.maximumAmount}
              step="0.01"
              value={amount}
              disabled={!available || busy || submittedAmount !== undefined}
              onChange={(e) => setAmount(e.target.value)}
            />
          </div>
          <div className="quick-amounts">
            {[10, 25, 50, 100].filter(value => value >= paymentSettings.minimumAmount && value <= paymentSettings.maximumAmount).map((value) => (
              <button
                key={value}
                type="button"
                disabled={!available || busy || submittedAmount !== undefined}
                onClick={() => setAmount(String(value))}
              >
                {formatCurrency(value)}
              </button>
            ))}
          </div>
          {settingsError && <p role="alert">{settingsError}</p>}
          {error && (
            <p role="alert" className="field-error">
              {error}
            </p>
          )}
          <button className="primary-button full" type="submit" disabled={!available || busy || loading || Boolean(settingsError)}>
            {busy
              ? 'Opening Stripe…'
              : `${!available ? 'Boost unavailable' : error ? 'Retry' : 'Continue to payment'} · ${formatCurrency(parseAmount(amount))}`}
          </button>
          <small className="secure-note">No account required · Secure Stripe Checkout</small>
        </form>
      )}
    </dialog>
  )
}

