import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { confirmPayment, resumePayment } from '../../../shared/api/services/paymentApi'
import type { PaymentResponse } from '../../../shared/api/models/payment'
import { formatCurrency } from '../../../shared/format/currency'
import { completePaidEntry } from '../../creator-entry/data/pendingEntry'
import { PaidEntryEditor } from '../../creator-entry/ui/PaidEntryEditor'
import { continueToCheckout } from '../application'

export function PaymentResultPage({ cancelled = false, onConfirmed }: { cancelled?: boolean; onConfirmed: () => void })
{
  const [params] = useSearchParams()
  const id = params.get('payment_id') ?? ''
  const validId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)
  const [payment, setPayment] = useState<PaymentResponse>()
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [revision, setRevision] = useState(0)
  useEffect(() =>
  {
    if (!validId) return
    let active = true
    let timer: ReturnType<typeof setTimeout>
    let attempts = 0
    async function refresh()
    {
      try {
        const confirmed = await confirmPayment(id)
        if (!active) return
        setPayment(confirmed)
        const result = await completePaidEntry(confirmed)
        if (!active) return
        setPayment(result)
        setError('')
        if (result.fulfilled) {
          onConfirmed()
          return
        }
        if (!cancelled && result.status !== 'failed' && result.status !== 'expired' && ++attempts < 10)
          timer = setTimeout(refresh, 3000)
      } catch (failure) {
        if (active) setError(failure instanceof Error ? failure.message : 'Could not confirm your payment.')
      }
    }
    void refresh()
    return () => { active = false; clearTimeout(timer) }
  }, [id, validId, cancelled, revision, onConfirmed])

  async function resume()
  {
    setBusy(true)
    setError('')
    try { continueToCheckout(await resumePayment(id)) }
    catch (failure) { setError(failure instanceof Error ? failure.message : 'Could not resume checkout.') }
    finally { setBusy(false) }
  }
  const terminal = payment?.status === 'expired' || payment?.status === 'failed'
  return (
    <section className="page-shell" aria-live="polite">
      <p className="eyebrow">CrownRank payment</p>
      <h1>{!validId ? 'Invalid payment link' : payment?.fulfilled ? 'Payment confirmed'
        : terminal ? 'Checkout ended' : cancelled ? 'Checkout paused' : 'Confirming your payment…'}</h1>
      {payment?.fulfilled ? (
        <>
          <p>Your {formatCurrency(payment.amount, payment.currency)} contribution has been added to the ranking.</p>
          <Link className="primary-button" to={`/creators/${payment.entryId}`}>View creator</Link>
        </>
      ) : validId ? (
        <>
          <p>{terminal ? 'Your contribution has not been added. You can start a new submission from the leaderboard.'
            : cancelled ? 'Your checkout can still be open. Resume it to finish paying; your ranking updates only after confirmation.'
            : 'We are checking payment and saving your contribution. Keep this page open until your entry is saved.'}</p>
          {payment?.status === 'paid' && <p>Payment received. Your ranking update is still processing.</p>}
          {error && <p role="alert">{error}</p>}
          {error && payment?.status === 'paid' && payment.purpose === 'entry' &&
            <PaidEntryEditor key={id} paymentId={id} payment={payment} onRetry={() => setRevision(value => value + 1)} />}
          {!terminal && <button className="secondary-button" onClick={() => setRevision(value => value + 1)}>Check again</button>}
          {cancelled && payment?.status === 'pending' && <button className="primary-button" disabled={busy} onClick={resume}>
            {busy ? 'Opening checkout…' : 'Resume checkout'}
          </button>}
        </>
      ) : <p>This link does not identify a payment.</p>}
      <p><Link to="/rankings">Back to rankings</Link></p>
    </section>
  )
}
