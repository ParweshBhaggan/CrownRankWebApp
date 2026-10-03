export interface PaymentSettings {
  currency: string
  minimumAmount: number
  maximumAmount: number
  termsVersion: string
  privacyVersion: string
}
export interface CheckoutResponse {
  id: string
  url: string | null
  status: string
}
export interface PaymentResponse {
  id: string
  status: string
  fulfilled: boolean
  entryId: string | null
  amount: number
  currency: string
  purpose: 'entry' | 'boost'
}

export interface CheckoutRequest {
  name: string
  amount: number
}
