import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PaymentResultPage } from '../../src/features/payments/ui/PaymentResultPage'
import { confirmPayment, resumePayment } from '../../src/shared/api/services/paymentApi'
import { savePendingEntry, getPendingEntry } from '../../src/features/creator-entry/data/pendingEntry'
import { postEntry } from '../../src/shared/api/services/entryApi'
import { redirectToUrl } from '../../src/shared/api/redirect'

vi.mock('../../src/shared/api/services/paymentApi', () => ({ confirmPayment: vi.fn(), resumePayment: vi.fn() }))
vi.mock('../../src/shared/api/services/entryApi', () => ({ postEntry: vi.fn() }))
vi.mock('../../src/shared/config/useLookups', () => ({ useLookups: () => ({ categories: [{ id: 'category', name: 'Science' }], platforms: [{ id: 'platform', name: 'Instagram' }] }) }))
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
beforeEach(() => { vi.clearAllMocks(); localStorage.clear() })
describe('payment results', () => {
  it('only shows confirmation after the backend has fulfilled the payment', async () => {
    vi.mocked(confirmPayment).mockResolvedValue({ id, status: 'paid', fulfilled: true, entryId: 'creator', amount: 12.5, currency: 'usd', purpose: 'entry' })
    const confirmed = mount()
    expect(await screen.findByRole('heading', { name: 'Payment confirmed' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View creator' })).toHaveAttribute('href', '/creators/creator')
    expect(confirmed).toHaveBeenCalledOnce()
  })
  it('does not claim an unpaid success URL is proof of payment', async () => {
    vi.mocked(confirmPayment).mockResolvedValue({ id, status: 'pending', fulfilled: false, entryId: null, amount: 10, currency: 'usd', purpose: 'entry' })
    const confirmed = mount()
    await waitFor(() => expect(confirmPayment).toHaveBeenCalledWith(id))
    expect(screen.getByRole('heading', { name: /confirming/i })).toBeInTheDocument()
    expect(confirmed).not.toHaveBeenCalled()
  })
  it('resumes a cancelled checkout without creating a second operation', async () => {
    vi.mocked(confirmPayment).mockResolvedValue({ id, status: 'pending', fulfilled: false, entryId: null, amount: 10, currency: 'usd', purpose: 'entry' })
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
  it('registers the retained form after backend verification and clears it only on success', async () => {
    const entry = { name: 'Ada', username: 'ada', categories: [{ name: 'Science' }], socialMediaPlatforms: [{ platformName: 'Instagram', url: 'https://instagram.com/ada' }], score: 12.5, imgUrl: 'data:image/webp;base64,test' }
    savePendingEntry(id, entry)
    const paid = { id, status: 'paid', fulfilled: false, entryId: null, amount: 12.5, currency: 'usd', purpose: 'entry' as const }
    vi.mocked(confirmPayment).mockResolvedValue(paid)
    vi.mocked(postEntry).mockResolvedValue({ ...getPendingEntry(id)!, id: 'creator', createdDate: '2026-10-03T10:00:00Z' })
    const confirmed = mount()
    expect(await screen.findByRole('heading', { name: 'Payment confirmed' })).toBeInTheDocument()
    expect(postEntry).toHaveBeenCalledWith(entry, id)
    expect(getPendingEntry(id)).toBeNull()
    expect(confirmed).toHaveBeenCalledOnce()
  })
  it('retains a failed registration and allows corrections without another checkout', async () => {
    savePendingEntry(id, { name: 'Ada', username: 'taken', categories: [{ name: 'Science' }], socialMediaPlatforms: [{ platformName: 'Instagram', url: 'https://instagram.com/ada' }], score: 12.5, imgUrl: 'data:image/webp;base64,test' })
    const paid = { id, status: 'paid', fulfilled: false, entryId: null, amount: 12.5, currency: 'usd', purpose: 'entry' as const }
    vi.mocked(confirmPayment).mockResolvedValue(paid)
    vi.mocked(postEntry).mockRejectedValueOnce(new Error('Username is taken')).mockResolvedValueOnce({ ...getPendingEntry(id)!, id: 'creator', createdDate: '2026-10-03T10:00:00Z' })
    const confirmed = mount()
    expect(await screen.findByRole('alert')).toHaveTextContent('Username is taken')
    expect(confirmed).not.toHaveBeenCalled()
    expect(getPendingEntry(id)?.username).toBe('taken')
    const username = screen.getByLabelText('Creator username')
    await userEvent.clear(username)
    await userEvent.type(username, 'available')
    await userEvent.click(screen.getByRole('button', { name: 'Save paid entry' }))
    expect(await screen.findByRole('heading', { name: 'Payment confirmed' })).toBeInTheDocument()
    expect(vi.mocked(postEntry).mock.calls[1][0].username).toBe('available')
    expect(resumePayment).not.toHaveBeenCalled()
  })
  it('never registers a retained form for an unpaid checkout', async () => {
    savePendingEntry(id, { name: 'Ada', username: 'ada', categories: [{ name: 'Science' }], socialMediaPlatforms: [], score: 10, imgUrl: 'image' })
    vi.mocked(confirmPayment).mockResolvedValue({ id, status: 'pending', fulfilled: false, entryId: null, amount: 10, currency: 'usd', purpose: 'entry' })
    mount()
    await waitFor(() => expect(confirmPayment).toHaveBeenCalledWith(id))
    expect(postEntry).not.toHaveBeenCalled()
    expect(getPendingEntry(id)).not.toBeNull()
  })
  it('reports a missing form while preserving the verified paid state', async () => {
    vi.mocked(confirmPayment).mockResolvedValue({ id, status: 'paid', fulfilled: false, entryId: null, amount: 10, currency: 'usd', purpose: 'entry' })
    mount()
    expect(await screen.findByRole('alert')).toHaveTextContent('browser you used for checkout')
    expect(postEntry).not.toHaveBeenCalled()
    expect(screen.getByText('Payment received. Your ranking update is still processing.')).toBeInTheDocument()
  })

})
