import type { CheckoutSession, CreateCheckoutRequest, PaymentGateway } from '../domain/payment'
import { apiRequest } from '../../../shared/api/httpClient'

export class ApiPaymentGateway implements PaymentGateway {
  async createCheckout(request: CreateCheckoutRequest): Promise<CheckoutSession> {
    return apiRequest<CheckoutSession>('/api/payments/checkout', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request),
    })
  }
}
