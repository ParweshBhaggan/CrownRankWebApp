import type { CheckoutSession, CreateCheckoutRequest, PaymentGateway } from '../domain/payment'
import { boostEntry } from '../../../shared/api/services/entryApi'

// Direct score updates for the current no-payment flow.
export class ScoreBoostGateway implements PaymentGateway
{
  async createCheckout(request: CreateCheckoutRequest): Promise<CheckoutSession>
  {
    await boostEntry(request.creatorId, { amount: request.amount, referenceId: request.referenceId })
    return { id: request.referenceId, confirmed: true }
  }
}
