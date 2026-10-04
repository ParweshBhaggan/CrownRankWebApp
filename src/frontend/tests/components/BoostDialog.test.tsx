import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { BoostDialog } from '../../src/features/payments/ui/BoostDialog'
import type { Creator } from '../../src/features/leaderboard/domain/creator'

vi.mock('../../src/shared/config/useLookups', () => ({ useLookups: () => ({ paymentSettings: { currency: 'usd', minimumAmount: 10, maximumAmount: 10000 }, loading: false, error: '' }) }))
vi.mock('../../src/shared/api/redirect', () => ({ redirectToUrl: vi.fn() }))
import { redirectToUrl } from '../../src/shared/api/redirect'

const creator = {
  id: 'creator-1', username: 'ada', firstName: 'Ada', lastName: 'Lovelace', displayName: 'Ada Lovelace',
  category: 'technology', imageUrl: '/avatar.svg', socialProfiles: [], totalContributed: 10,
  dailyContributed: 10, joinedAt: '2026-09-01T00:00:00Z', scoreReachedAt: '2026-09-08T12:00:00Z',
} satisfies Creator

describe('BoostDialog', () => {
  it('opens Stripe with a decimal amount without crediting the ranking', async () => {
    const createCheckout = vi.fn().mockResolvedValue({ id: 'mock-1', url: 'https://checkout.stripe.com/boost', status: 'pending' })
    const confirmed = vi.fn()
    const user = userEvent.setup()
    render(<BoostDialog creator={creator} gateway={{ createCheckout }} onClose={vi.fn()} onConfirmed={confirmed} />)

    await user.clear(screen.getByLabelText('Boost amount'))
    await user.type(screen.getByLabelText('Boost amount'), '12.50')
    await user.click(screen.getByRole('button', { name: /continue to payment/i }))

    await waitFor(() => expect(createCheckout).toHaveBeenCalledOnce())
    expect(createCheckout).toHaveBeenCalledWith(expect.objectContaining({
      creatorId: creator.id, amount: 12.5,
    }))
    expect(confirmed).not.toHaveBeenCalled()
    expect(redirectToUrl).toHaveBeenCalledWith('https://checkout.stripe.com/boost')
    expect(await screen.findByText('Checkout ready')).toBeInTheDocument()
  })

  it('shows an unconfirmed result and retries with the same reference and amount', async () => {
    const createCheckout = vi.fn()
      .mockResolvedValueOnce({ id: 'mock-1', url: null, status: 'pending' })
      .mockResolvedValueOnce({ id: 'mock-1', url: 'https://checkout.stripe.com/boost', status: 'pending' })
    const user = userEvent.setup()
    render(<BoostDialog creator={creator} gateway={{ createCheckout }} onClose={vi.fn()} onConfirmed={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: /continue to payment/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/did not return a checkout URL/i)
    await user.click(screen.getByRole('button', { name: /retry/i }))

    await waitFor(() => expect(createCheckout).toHaveBeenCalledTimes(2))
    expect(createCheckout.mock.calls[0][0]).toEqual(createCheckout.mock.calls[1][0])
  })
})

