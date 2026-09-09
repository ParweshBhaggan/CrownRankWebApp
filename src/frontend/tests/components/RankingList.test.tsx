import { MemoryRouter } from 'react-router-dom'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import { RankingList } from '../../src/features/leaderboard/ui/RankingList'

it('renders API rank, creator link, category, decimal score, and Boost action', async () => {
  const creator = {
    id: 'creator-1', username: 'ada', firstName: 'Ada', lastName: 'Lovelace', displayName: 'Ada Lovelace',
    category: 'technology' as const, imageUrl: '/avatar.svg', socialProfiles: [], totalContributed: 12.5,
    dailyContributed: 12.5, joinedAt: '2026-09-01T00:00:00Z', scoreReachedAt: '2026-09-08T12:00:00Z', rank: 1,
  }
  const onBoost = vi.fn()
  const user = userEvent.setup()
  render(<MemoryRouter><RankingList creators={[creator]} onBoost={onBoost} /></MemoryRouter>)

  expect(screen.getByText('#1')).toBeInTheDocument()
  expect(screen.getByRole('link', { name: /Ada Lovelace/i })).toHaveAttribute('href', '/creators/creator-1')
  expect(screen.getByText('Technology')).toBeInTheDocument()
  expect(screen.getByText('$12.50')).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: /boost/i }))
  expect(onBoost).toHaveBeenCalledWith(creator)
})
