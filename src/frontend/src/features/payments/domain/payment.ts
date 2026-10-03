import type { CheckoutResponse } from '../../../shared/api/models/payment'
export interface CreateCheckoutRequest {
  readonly referenceId: string
  readonly creatorId: string
  readonly amount: number
}
export interface PaymentGateway {
  createCheckout(request: CreateCheckoutRequest): Promise<CheckoutResponse>
}
