import { DummyLeaderboardRepository } from '../../features/leaderboard/data/DummyLeaderboardRepository'
import { MockPaymentGateway } from '../../features/payments/data/MockPaymentGateway'

export const appServices = { leaderboardRepository: new DummyLeaderboardRepository(), paymentGateway: new MockPaymentGateway() } as const
