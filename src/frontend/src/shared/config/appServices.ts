import { ApiLeaderboardRepository } from '../../features/leaderboard/data/ApiLeaderboardRepository'
import { ApiPaymentGateway } from '../../features/payments/data/ApiPaymentGateway'

export const appServices = { leaderboardRepository: new ApiLeaderboardRepository(), paymentGateway: new ApiPaymentGateway() } as const
