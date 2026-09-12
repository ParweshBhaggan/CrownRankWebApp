import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { BoostDialog } from '../../src/features/payments/ui/BoostDialog'
import type { Creator } from '../../src/features/leaderboard/domain/creator'

const creator = {
  id: 'creator-1', username: 'ada', name: 'Ada Lovelace',
  category: 'technology', imageUrl: '/avatar.svg', socialProfiles: [], totalContributed: 10,
  dailyContributed: 10, joinedAt: '2026-09-01T00:00:00Z', scoreReachedAt: '2026-09-08T12:00:00Z',
} satisfies Creator

describe('BoostDialog', () => {
  it('sends decimal USD through the mock gateway and confirms once', async () => {
    const createCheckout = vi.fn().mockResolvedValue({ id: 'mock-1', confirmed: true })
    const confirmed = vi.fn()
    const user = userEvent.setup()
    render(<BoostDialog creator={creator} gateway={{ createCheckout }} onClose={vi.fn()} onConfirmed={confirmed} />)

    await user.clear(screen.getByLabelText('Boost amount'))
    await user.type(screen.getByLabelText('Boost amount'), '2.50')
    await user.click(screen.getByRole('button', { name: /continue to checkout/i }))

    await waitFor(() => expect(createCheckout).toHaveBeenCalledOnce())
    expect(createCheckout).toHaveBeenCalledWith(expect.objectContaining({
      creatorId: creator.id, purpose: 'creator-boost', amount: 2.5, currency: 'USD',
    }))
    expect(confirmed).toHaveBeenCalledOnce()
    expect(await screen.findByText('Boost confirmed')).toBeInTheDocument()
  })

  it('shows an unconfirmed result and retries with the same reference and amount', async () => {
    const createCheckout = vi.fn()
      .mockResolvedValueOnce({ id: 'mock-1', confirmed: false })
      .mockResolvedValueOnce({ id: 'mock-1', confirmed: true })
    const user = userEvent.setup()
    render(<BoostDialog creator={creator} gateway={{ createCheckout }} onClose={vi.fn()} onConfirmed={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: /continue to checkout/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/could not be started/i)
    await user.click(screen.getByRole('button', { name: /retry/i }))

    await waitFor(() => expect(createCheckout).toHaveBeenCalledTimes(2))
    expect(createCheckout.mock.calls[0][0]).toEqual(createCheckout.mock.calls[1][0])
  })
})
