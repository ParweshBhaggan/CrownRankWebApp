import type { CheckoutResponse } from '../../shared/api/models/payment'
import { redirectToUrl } from '../../shared/api/redirect'

export function continueToCheckout(session: CheckoutResponse): void
{
  if (session.url) {
    const target = new URL(session.url)
    if (target.protocol !== 'https:' || target.hostname !== 'checkout.stripe.com')
      throw new Error('The API returned an invalid Stripe Checkout URL.')
    redirectToUrl(session.url)
    return
  }
  redirectToUrl(`/payment/success?payment_id=${encodeURIComponent(session.id)}`)
}
