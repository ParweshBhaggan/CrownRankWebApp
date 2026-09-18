import type { CheckoutSession, CreateCheckoutRequest, PaymentGateway } from '../domain/payment'
import { apiRequest } from '../../../shared/api/httpClient'

interface PaymentStart {
  readonly attemptId: string
}

// The backend development adapter simulates success and persists the contribution.
// No payment provider is contacted and no real money is charged.
export class MockPaymentGateway implements PaymentGateway {
  async createCheckout(request: CreateCheckoutRequest): Promise<CheckoutSession> {
    const payment = await apiRequest<PaymentStart>(`/api/entries/${request.creatorId}/boosts`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        amountInMinorUnits: Math.round(request.amount * 100),
        currency: request.currency,
      }),
    })
    await apiRequest<void>(`/api/dev/payments/${payment.attemptId}/outcome`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ outcome: 'Succeeded' }),
    })
    await apiRequest(`/api/payments/${payment.attemptId}/confirm`, { method: 'POST' })
    return { id: payment.attemptId, confirmed: true }
  }
}
