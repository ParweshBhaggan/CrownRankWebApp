import type { CreateCheckoutRequest, PaymentGateway } from '../domain/payment'
import { startBoostCheckout } from '../../../shared/api/services/paymentApi'

export class StripeCheckoutGateway implements PaymentGateway
{
  createCheckout(request: CreateCheckoutRequest)
  {
    return startBoostCheckout(request.referenceId, request.creatorId, request.amount)
  }
}
