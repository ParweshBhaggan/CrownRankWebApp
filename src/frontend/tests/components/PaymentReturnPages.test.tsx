import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PaymentComplete } from '../../src/features/payments/ui/PaymentReturnPages'
import { getPaymentStatus } from '../../src/features/payments/data/getPaymentStatus'

vi.mock('../../src/features/payments/data/getPaymentStatus', () => ({ getPaymentStatus: vi.fn() }))

describe('PaymentComplete', () => {
  beforeEach(() => vi.mocked(getPaymentStatus).mockReset())

  it('shows confirmation only after the backend reports the webhook result', async () => {
    vi.mocked(getPaymentStatus).mockResolvedValue({ creatorId: 'creator-1', confirmed: true })
    const confirmed = vi.fn()
    render(<MemoryRouter initialEntries={['/payment/complete?referenceId=ref-1&creatorId=creator-1&purpose=ranking-entry']}><PaymentComplete onConfirmed={confirmed} /></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'Payment confirmed' })).toBeInTheDocument()
    expect(getPaymentStatus).toHaveBeenCalledWith('ref-1', 'creator-1', 'ranking-entry')
    expect(confirmed).toHaveBeenCalledOnce()
  })

  it('rejects a return URL with missing payment references', () => {
    render(<MemoryRouter initialEntries={['/payment/complete']}><PaymentComplete onConfirmed={vi.fn()} /></MemoryRouter>)

    expect(screen.getByRole('heading', { name: 'Invalid payment return' })).toBeInTheDocument()
    expect(getPaymentStatus).not.toHaveBeenCalled()
  })
})
