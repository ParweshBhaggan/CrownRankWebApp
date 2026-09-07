import { ApiLeaderboardRepository } from '../../features/leaderboard/data/ApiLeaderboardRepository'
import { MockPaymentGateway } from '../../features/payments/data/MockPaymentGateway'

export const appServices = { leaderboardRepository: new ApiLeaderboardRepository(), paymentGateway: new MockPaymentGateway() } as const
