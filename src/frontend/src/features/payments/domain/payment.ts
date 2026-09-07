export type PaymentPurpose = 'ranking-entry' | 'creator-boost'
export type PaymentProvider = 'mock' | 'stripe'

export interface Money { readonly amountCents: number; readonly currency: 'USD' }
export interface CreateCheckoutRequest {
  readonly referenceId: string
  readonly purpose: PaymentPurpose
  readonly money: Money
  readonly successUrl: string
  readonly cancelUrl: string
  readonly metadata: Readonly<Record<string, string>>
}
export interface CheckoutSession { readonly id: string; readonly provider: PaymentProvider; readonly checkoutUrl: string }
export interface PaymentGateway { createCheckout(request: CreateCheckoutRequest): Promise<CheckoutSession> }

