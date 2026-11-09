import type { CreateEntryRequest } from './entry'
export interface PaymentSettings {
  readonly currency: string
  readonly minimumAmount: number
  readonly maximumAmount: number
}
export interface CheckoutRequest {
  readonly name: string
  readonly amount: number
  readonly entry?: CreateEntryRequest
}
export interface CheckoutResponse {
  readonly id: string
  readonly url: string | null
  readonly status: string
}
export interface PaymentResponse {
  readonly id: string
  readonly status: string
  readonly fulfilled: boolean
  readonly entryId: string | null
  readonly amount: number
  readonly currency: string
  readonly purpose: string
}
