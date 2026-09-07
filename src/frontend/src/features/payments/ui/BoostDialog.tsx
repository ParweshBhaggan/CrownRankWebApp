import { useEffect, useRef, useState, type FormEvent } from 'react'
import type { Creator } from '../../leaderboard/domain/creator'
import type { PaymentGateway } from '../domain/payment'
import { formatCurrency } from '../../../shared/format/currency'

interface Props { creator?: Creator; gateway: PaymentGateway; onClose: () => void }

export function BoostDialog({ creator, gateway, onClose }: Props) {
  const ref = useRef<HTMLDialogElement>(null)
  const [amount, setAmount] = useState('25')
  const [ready, setReady] = useState(false)
  useEffect(() => { if (creator && !ref.current?.open) ref.current?.showModal(); if (!creator && ref.current?.open) ref.current.close() }, [creator])
  async function submit(event: FormEvent) {
    event.preventDefault()
    const cents = Math.round(Number(amount) * 100)
    if (!creator || cents < 100) return
    await gateway.createCheckout({ referenceId: crypto.randomUUID(), purpose: 'creator-boost', money: { amountCents: cents, currency: 'USD' }, successUrl: window.location.href, cancelUrl: window.location.href, metadata: { creatorId: creator.id } })
    setReady(true)
  }
  return <dialog ref={ref} className="boost-dialog" onClose={() => { setReady(false); onClose() }} onCancel={(e) => { e.preventDefault(); onClose() }}>
    <button className="icon-button dialog-close" onClick={onClose} aria-label="Close">×</button>
    {creator && (ready ? <div className="boost-success"><span>✓</span><h2>Boost checkout prepared</h2><p>Stripe Checkout will open here. The score updates only after payment confirmation.</p><button className="primary-button" onClick={onClose}>Done</button></div> : <form onSubmit={submit}>
      <p className="eyebrow">Boost this creator</p><div className="boost-person"><img src={creator.imageUrl} alt="" /><div><h2>{creator.displayName}</h2><p>@{creator.username} · Current score {formatCurrency(creator.totalContributedCents)}</p></div></div>
      <label htmlFor="boost-amount">Boost amount</label><div className="amount-input"><span>$</span><input id="boost-amount" type="number" min="1" step="1" value={amount} onChange={(e) => setAmount(e.target.value)} /></div>
      <div className="quick-amounts">{[10,25,50,100].map(value => <button key={value} type="button" onClick={() => setAmount(String(value))}>{'$'}{value}</button>)}</div>
      <button className="primary-button full" type="submit">Continue with {formatCurrency(Math.max(0, Number(amount) * 100))}</button><small className="secure-note">No account required · Secure Stripe checkout at launch</small>
    </form>)}
  </dialog>
}
