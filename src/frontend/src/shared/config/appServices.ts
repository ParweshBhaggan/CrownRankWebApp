import { ApiLeaderboardRepository } from '../../features/leaderboard/data/ApiLeaderboardRepository'
import { ScoreBoostGateway } from '../../features/payments/data/ScoreBoostGateway'

export const appServices = {
  leaderboardRepository: new ApiLeaderboardRepository(),
  paymentGateway: new ScoreBoostGateway(),
} as const
