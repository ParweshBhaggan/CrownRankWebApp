import type { Creator, RankedCreator } from '../domain/creator'

export function rankCreators(creators: readonly Creator[]): readonly RankedCreator[] {
  return [...creators]
    .sort((left, right) => right.totalContributedCents - left.totalContributedCents || left.joinedAt.localeCompare(right.joinedAt))
    .map((creator, index) => ({ ...creator, rank: index + 1 }))
}

