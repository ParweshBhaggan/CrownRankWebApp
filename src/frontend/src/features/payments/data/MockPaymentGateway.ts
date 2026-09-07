import type { CheckoutSession, CreateCheckoutRequest, PaymentGateway } from '../domain/payment'

export class MockPaymentGateway implements PaymentGateway {
  async createCheckout(request: CreateCheckoutRequest): Promise<CheckoutSession> {
    await new Promise((resolve) => window.setTimeout(resolve, 650))
    return { id: `mock_checkout_${crypto.randomUUID()}`, provider: 'mock', checkoutUrl: `${request.successUrl}?checkout=demo` }
  }
}

