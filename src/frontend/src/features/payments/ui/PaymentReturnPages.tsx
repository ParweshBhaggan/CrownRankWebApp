import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getPaymentStatus } from '../data/getPaymentStatus'
import type { PaymentPurpose } from '../domain/payment'

const validPurpose = (value: string | null): value is PaymentPurpose =>
  value === 'ranking-entry' || value === 'creator-boost'

export function PaymentComplete({ onConfirmed }: { readonly onConfirmed: () => void }) {
  const [params] = useSearchParams()
  const referenceId = params.get('referenceId') ?? ''
  const creatorId = params.get('creatorId') ?? ''
  const purpose = params.get('purpose')
  const paymentDetailsAreValid = Boolean(referenceId && creatorId && validPurpose(purpose))
  const [state, setState] = useState<'checking' | 'confirmed' | 'delayed' | 'invalid'>(paymentDetailsAreValid ? 'checking' : 'invalid')

  useEffect(() => {
    if (!referenceId || !creatorId || !validPurpose(purpose)) return
    let active = true
    let timer: number | undefined
    let attempts = 0
    const check = async () => {
      try {
        const status = await getPaymentStatus(referenceId, creatorId, purpose)
        if (!active) return
        if (status.confirmed) {
          setState('confirmed')
          onConfirmed()
          return
        }
      } catch { /* A webhook may still be processing; retry below. */ }
      attempts += 1
      if (!active) return
      if (attempts >= 15) setState('delayed')
      else timer = window.setTimeout(check, 1000)
    }
    void check()
    return () => { active = false; if (timer !== undefined) window.clearTimeout(timer) }
  }, [creatorId, onConfirmed, purpose, referenceId])

  return <section className="page-shell api-state" aria-live="polite">
    <p className="eyebrow">Secure checkout</p>
    {state === 'checking' && <><h1>Confirming your payment…</h1><p>Stripe accepted the checkout. We’re waiting for the signed confirmation.</p></>}
    {state === 'confirmed' && <><h1>Payment confirmed</h1><p>Your contribution is recorded and the ranking has been updated.</p><Link className="primary-button" to={purpose === 'creator-boost' ? `/creators/${creatorId}` : '/rankings'}>View the updated ranking</Link></>}
    {state === 'delayed' && <><h1>Confirmation is taking longer than expected</h1><p>Your payment may still complete. Keep the Stripe webhook listener running, then refresh this page.</p><button className="primary-button" onClick={() => window.location.reload()}>Check again</button></>}
    {state === 'invalid' && <><h1>Invalid payment return</h1><p>The checkout return details are missing.</p><Link to="/rankings">Back to rankings</Link></>}
  </section>
}

export function PaymentCancelled() {
  return <section className="page-shell api-state"><p className="eyebrow">Checkout cancelled</p><h1>No payment was made</h1><p>Your ranking was not changed. You can return and start checkout again whenever you’re ready.</p><Link className="primary-button" to="/rankings">Back to rankings</Link></section>
}
