import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PaymentResultPage } from '../../src/features/payments/ui/PaymentResultPage'
import { confirmPayment, resumePayment } from '../../src/shared/api/services/paymentApi'
import { redirectToUrl } from '../../src/shared/api/redirect'

vi.mock('../../src/shared/api/services/paymentApi', () => ({ confirmPayment: vi.fn(), resumePayment: vi.fn() }))
vi.mock('../../src/shared/api/redirect', () => ({ redirectToUrl: vi.fn() }))
const id = '019b7335-1979-4f83-b5f3-4a83e4bb1a96'
function mount(cancelled = false, paymentId = id)
{
  const confirmed = vi.fn()
  render(<MemoryRouter initialEntries={[`/payment/success?payment_id=${paymentId}`]}>
    <PaymentResultPage cancelled={cancelled} onConfirmed={confirmed} />
  </MemoryRouter>)
  return confirmed
}
beforeEach(() => vi.clearAllMocks())
describe('payment results', () => {
  it('only shows confirmation after the backend has fulfilled the payment', async () => {
    vi.mocked(confirmPayment).mockResolvedValue({ id, status: 'paid', fulfilled: true, entryId: 'creator', amount: 12.5, currency: 'usd' })
    const confirmed = mount()
    expect(await screen.findByRole('heading', { name: 'Payment confirmed' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View creator' })).toHaveAttribute('href', '/creators/creator')
    expect(confirmed).toHaveBeenCalledOnce()
  })
  it('does not claim an unpaid success URL is proof of payment', async () => {
    vi.mocked(confirmPayment).mockResolvedValue({ id, status: 'pending', fulfilled: false, entryId: null, amount: 10, currency: 'usd' })
    const confirmed = mount()
    await waitFor(() => expect(confirmPayment).toHaveBeenCalledWith(id))
    expect(screen.getByRole('heading', { name: /confirming/i })).toBeInTheDocument()
    expect(confirmed).not.toHaveBeenCalled()
  })
  it('resumes a cancelled checkout without creating a second operation', async () => {
    vi.mocked(confirmPayment).mockResolvedValue({ id, status: 'pending', fulfilled: false, entryId: null, amount: 10, currency: 'usd' })
    vi.mocked(resumePayment).mockResolvedValue({ id, status: 'pending', url: 'https://checkout.stripe.com/resume' })
    mount(true)
    await userEvent.click(await screen.findByRole('button', { name: 'Resume checkout' }))
    expect(resumePayment).toHaveBeenCalledWith(id)
    expect(redirectToUrl).toHaveBeenCalledWith('https://checkout.stripe.com/resume')
  })
  it('rejects malformed payment links without making requests', () => {
    mount(false, 'invalid')
    expect(screen.getByRole('heading', { name: 'Invalid payment link' })).toBeInTheDocument()
    expect(confirmPayment).not.toHaveBeenCalled()
  })
  it('shows a retry action when confirmation is unavailable', async () => {
    vi.mocked(confirmPayment).mockRejectedValue(new Error('Confirmation unavailable'))
    mount()
    expect(await screen.findByRole('alert')).toHaveTextContent('Confirmation unavailable')
    expect(screen.getByRole('button', { name: 'Check again' })).toBeInTheDocument()
  })
})
