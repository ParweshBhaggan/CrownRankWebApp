import { apiGet, apiPost } from '../apiRequest'
import { payment_settings_endpoint, entry_checkout_endpoint, boost_checkout_endpoint,
  payment_endpoint, confirm_payment_endpoint, resume_payment_endpoint } from '../endpoints/paymentEndpoints'
import type { PaymentSettings, EntryCheckoutRequest, CheckoutResponse, PaymentResponse } from '../models/payment'

export function getPaymentSettings(): Promise<PaymentSettings>
{
  return apiGet(payment_settings_endpoint)
}
export function startEntryCheckout(request: EntryCheckoutRequest): Promise<CheckoutResponse>
{
  return apiPost(entry_checkout_endpoint, request)
}
export function startBoostCheckout(referenceId: string, entryId: string, amount: number): Promise<CheckoutResponse>
{
  return apiPost(boost_checkout_endpoint, { referenceId, entryId, amount })
}
export function getPayment(id: string): Promise<PaymentResponse>
{
  return apiGet(payment_endpoint(id))
}
export function confirmPayment(id: string): Promise<PaymentResponse>
{
  return apiPost(confirm_payment_endpoint(id), {})
}
export function resumePayment(id: string): Promise<CheckoutResponse>
{
  return apiPost(resume_payment_endpoint(id), {})
}
