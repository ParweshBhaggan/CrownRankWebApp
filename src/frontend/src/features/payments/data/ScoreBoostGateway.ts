import type { CheckoutSession, CreateCheckoutRequest, PaymentGateway } from '../domain/payment'
import { apiRequest } from '../../../shared/api/httpClient'

// Direct score updates for the current no-payment flow.
export class ScoreBoostGateway implements PaymentGateway {
  async createCheckout(request: CreateCheckoutRequest): Promise<CheckoutSession> {
    await apiRequest(`/api/Entry/${encodeURIComponent(request.creatorId)}/boost`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ amount: request.amount, referenceId: request.referenceId }),
    })
    return { id: request.referenceId, confirmed: true }
  }
}
