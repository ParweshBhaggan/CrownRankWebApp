import type { Creator, RankedCreator } from '../domain/creator'

// The API owns score ordering and tie-breaking, including full timestamp precision.
export function rankCreators(creators: readonly Creator[]): readonly RankedCreator[] {
  return creators.map((creator, index) => ({ ...creator, rank: index + 1 }))
}
