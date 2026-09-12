import { Link } from 'react-router-dom'
import type { RankedCreator } from '../domain/creator'
import { creatorCategoryLabels } from '../domain/creator'
import { formatCurrency } from '../../../shared/format/currency'

interface Props { creators: readonly RankedCreator[]; onBoost: (creator: RankedCreator) => void; compact?: boolean }

export function RankingList({ creators, onBoost, compact = false }: Props) {
  return <ol className={`rank-list ${compact ? 'is-compact' : ''}`}>
    {creators.map((creator) => <li key={creator.id} className={creator.rank <= 3 ? `podium rank-${creator.rank}` : ''}>
      <span className="rank-number">#{creator.rank}</span>
      <Link className="rank-creator" to={`/creators/${creator.id}`}>
        <img src={creator.imageUrl} alt="" />
        <span><strong>{creator.name}</strong><small>@{creator.username}</small></span>
      </Link>
      <span className="category-pill">{creatorCategoryLabels[creator.category]}</span>
      <strong className="rank-score">{formatCurrency(creator.totalContributed)}</strong>
      <button className="boost-button" type="button" onClick={() => onBoost(creator)}>Boost <span>＋</span></button>
    </li>)}
  </ol>
}
