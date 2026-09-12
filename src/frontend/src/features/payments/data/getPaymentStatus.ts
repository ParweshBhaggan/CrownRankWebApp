import { apiRequest } from '../../../shared/api/httpClient'
import type { PaymentPurpose, PaymentStatus } from '../domain/payment'

export function getPaymentStatus(referenceId: string, creatorId: string, purpose: PaymentPurpose): Promise<PaymentStatus> {
  const query = new URLSearchParams({ creatorId, purpose })
  return apiRequest<PaymentStatus>(`/api/payments/status/${encodeURIComponent(referenceId)}?${query}`)
}
