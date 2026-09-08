import type { CheckoutSession, CreateCheckoutRequest, PaymentGateway } from '../domain/payment'
import { apiRequest } from '../../../shared/api/httpClient'

// The backend development adapter simulates success and persists the contribution.
// No payment provider is contacted and no real money is charged.
export class MockPaymentGateway implements PaymentGateway {
  async createCheckout(request: CreateCheckoutRequest): Promise<CheckoutSession> {
    return apiRequest<CheckoutSession>('/api/payments/checkout', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request),
    })
  }
}
