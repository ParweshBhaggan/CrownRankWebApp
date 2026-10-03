import { ApiLeaderboardRepository } from '../../features/leaderboard/data/ApiLeaderboardRepository'
import { StripeCheckoutGateway } from '../../features/payments/data/StripeCheckoutGateway'

export const appServices = {
  leaderboardRepository: new ApiLeaderboardRepository(),
  paymentGateway: new StripeCheckoutGateway(),
} as const

