import type { PaymentResponse } from '../../../shared/api/models/payment'
import type { PaidEntryRequest } from '../../../shared/api/models/entry'
import { postEntry } from '../../../shared/api/services/entryApi'

const prefix = 'crownrank:paid-entry:'

export function savePendingEntry(paymentId: string, entry: PaidEntryRequest): void
{
  try {
    localStorage.setItem(`${prefix}${paymentId}`, JSON.stringify(entry))
  } catch {
    throw new Error('Your browser could not save the form for the payment return. Enable browser storage before continuing.')
  }
}

export function getPendingEntry(paymentId: string): PaidEntryRequest | null
{
  try {
    const value = localStorage.getItem(`${prefix}${paymentId}`)
    return value ? JSON.parse(value) as PaidEntryRequest : null
  } catch { return null }
}

export function discardPendingEntry(paymentId: string): void
{
  try { localStorage.removeItem(`${prefix}${paymentId}`) }
  catch { /* Registration succeeded; cleanup can be retried on the next visit. */ }
}

export async function completePaidEntry(payment: PaymentResponse): Promise<PaymentResponse>
{
  if (payment.fulfilled) {
    discardPendingEntry(payment.id)
    return payment
  }
  if (payment.status !== 'paid' || payment.purpose !== 'entry') return payment
  const entry = getPendingEntry(payment.id)
  if (!entry) throw new Error('Payment received. Open this return link in the browser you used for checkout to finish registering your entry.')
  const result = await postEntry(entry, payment.id)
  if (result.fulfilled) discardPendingEntry(payment.id)
  return result
}
