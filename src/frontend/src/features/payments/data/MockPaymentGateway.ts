import type { CheckoutSession, CreateCheckoutRequest, PaymentGateway } from '../domain/payment'

export class MockPaymentGateway implements PaymentGateway {
  async createCheckout(_request: CreateCheckoutRequest): Promise<CheckoutSession> {
    void _request
    throw new Error('Boosting is not available in the current backend yet. No money was charged.')
  }
}
