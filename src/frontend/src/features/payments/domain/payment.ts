export type PaymentPurpose = 'ranking-entry' | 'creator-boost'
export interface CreateCheckoutRequest {
  readonly referenceId: string
  readonly creatorId: string
  readonly purpose: PaymentPurpose
  readonly amount: number
  readonly currency: 'USD'
}
export interface CheckoutSession { readonly id: string; readonly confirmed: boolean; readonly url?: string }
export interface PaymentStatus { readonly creatorId: string; readonly confirmed: boolean }
export interface PaymentGateway { createCheckout(request: CreateCheckoutRequest): Promise<CheckoutSession> }
