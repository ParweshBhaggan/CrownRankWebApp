export interface PaymentSettings {
  currency: string
  minimumAmount: number
  maximumAmount: number
  termsVersion: string
  privacyVersion: string
}
export interface EntrySubmissionRequest {
  referenceId: string
  amount: number
  name: string
  username: string
  categoryId: string
  socialProfiles: { platformId: string; url: string }[]
  imageDataUrl: string
  acceptedAgreements: boolean
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
}

export interface CheckoutRequest {
  name: string
  amount: number
}
export interface CheckoutPreparationResponse extends CheckoutRequest {
  id: string
}
