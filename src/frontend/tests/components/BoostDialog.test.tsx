import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { BoostDialog } from '../../src/features/payments/ui/BoostDialog'
import { LookupContext } from '../../src/shared/config/useLookups'
import { defaultPaymentSettings } from '../../src/shared/format/currency'
import { redirectToUrl } from '../../src/shared/api/redirect'
import type { Creator } from '../../src/features/leaderboard/domain/creator'

vi.mock('../../src/shared/api/redirect', () => ({ redirectToUrl: vi.fn() }))
const creator = {
  id: 'creator-1', username: 'ada', firstName: 'Ada', lastName: 'Lovelace', displayName: 'Ada Lovelace',
  category: 'technology', imageUrl: '/avatar.svg', socialProfiles: [], totalContributed: 10,
  dailyContributed: 10, joinedAt: '2026-09-01T00:00:00Z', scoreReachedAt: '2026-09-08T12:00:00Z',
} satisfies Creator
function mount(createCheckout: ReturnType<typeof vi.fn>)
{
  const confirmed = vi.fn()
  render(<LookupContext.Provider value={{ categories: [], platforms: [], payments: defaultPaymentSettings, loading: false, error: '', retry: vi.fn() }}>
    <BoostDialog creator={creator} gateway={{ createCheckout }} onClose={vi.fn()} onConfirmed={confirmed} />
  </LookupContext.Provider>)
  return confirmed
}
describe('BoostDialog', () => {
  it('redirects to Stripe without confirming a boost before payment', async () => {
    const createCheckout = vi.fn().mockResolvedValue({ id: 'payment', url: 'https://checkout.stripe.com/test', status: 'pending' })
    const confirmed = mount(createCheckout)
    const user = userEvent.setup()
    await user.clear(screen.getByLabelText('Boost amount'))
    await user.type(screen.getByLabelText('Boost amount'), '12.50')
    await user.click(screen.getByRole('button', { name: /continue to payment/i }))
    await waitFor(() => expect(createCheckout).toHaveBeenCalledOnce())
    expect(createCheckout).toHaveBeenCalledWith(expect.objectContaining({ creatorId: creator.id, amount: 12.5 }))
    expect(redirectToUrl).toHaveBeenCalledWith('https://checkout.stripe.com/test')
    expect(confirmed).not.toHaveBeenCalled()
  })
  it('rejects amounts below the configured minimum', async () => {
    const createCheckout = vi.fn()
    mount(createCheckout)
    const user = userEvent.setup()
    await user.clear(screen.getByLabelText('Boost amount'))
    await user.type(screen.getByLabelText('Boost amount'), '9.99')
    await user.click(screen.getByRole('button', { name: /continue to payment/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent('$10.00–$10,000.00')
    expect(createCheckout).not.toHaveBeenCalled()
  })
  it('retries a timeout with the same reference and amount', async () => {
    const createCheckout = vi.fn().mockRejectedValueOnce(new Error('Checkout interrupted'))
      .mockResolvedValueOnce({ id: 'payment', url: 'https://checkout.stripe.com/test', status: 'pending' })
    mount(createCheckout)
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /continue to payment/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Checkout interrupted')
    expect(screen.getByLabelText('Boost amount')).toBeDisabled()
    await user.click(screen.getByRole('button', { name: /retry/i }))
    await waitFor(() => expect(createCheckout).toHaveBeenCalledTimes(2))
    expect(createCheckout.mock.calls[0][0]).toEqual(createCheckout.mock.calls[1][0])
  })
})
