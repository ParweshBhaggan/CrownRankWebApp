import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { EnterRankingDialog } from '../../src/features/creator-entry/ui/EnterRankingDialog'
import { LookupProvider } from '../../src/shared/config/LookupProvider'
import { createEntry } from '../../src/features/creator-entry/data/createEntry'

vi.mock('../../src/shared/api/services/paymentApi', () => ({
  getPaymentSettings: vi.fn(async () => ({ currency: 'usd', minimumAmount: 10, maximumAmount: 10000 })),
}))

vi.mock('../../src/shared/api/services/categoryApi', () => ({
  getCategories: vi.fn(async () => [{ id: 'category-technology', name: 'Technology' }, { id: 'category-science', name: 'Science' }]),
}))

vi.mock('../../src/shared/api/services/socialMediaDefaultApi', () => ({
  getSocialMediaDefaults: vi.fn(async () => [{ id: 'platform-instagram', name: 'Instagram' }, { id: 'platform-facebook', name: 'Facebook' }]),
}))

vi.mock('../../src/features/creator-entry/data/createEntry', () => ({ createEntry: vi.fn() }))

describe('EnterRankingDialog', () => {
  beforeEach(() => vi.mocked(createEntry).mockReset())

  it('blocks invalid entry data and shows field errors without calling the API', async () => {
    const user = userEvent.setup()
    render(<LookupProvider><EnterRankingDialog isOpen onClose={vi.fn()} onConfirmed={vi.fn()} /></LookupProvider>)

    await user.click(screen.getByRole('button', { name: /continue to payment/i }))

    expect(await screen.findByText(/enter a name/i)).toBeInTheDocument()
    expect(screen.getByText(/confirm the declaration/i)).toBeInTheDocument()
    expect(createEntry).not.toHaveBeenCalled()
  })

  it('shows category and platform options supplied by the backend', async () => {
    render(<LookupProvider><EnterRankingDialog isOpen onClose={vi.fn()} onConfirmed={vi.fn()} /></LookupProvider>)
    expect(await screen.findByRole('option', { name: 'Science' })).toHaveValue('category-science')
    expect(screen.getByRole('option', { name: 'Facebook' })).toHaveValue('Facebook')
  })

  it('starts checkout without reporting an unpaid entry as saved', async () => {
    vi.mocked(createEntry).mockResolvedValue({} as never)
    const user = userEvent.setup()
    const confirmed = vi.fn()
    render(<LookupProvider><EnterRankingDialog isOpen onClose={vi.fn()} onConfirmed={confirmed} /></LookupProvider>)

    await user.type(screen.getByLabelText('Name'), 'Ada Lovelace')
    await user.type(screen.getByLabelText('Creator username'), 'ada')
    await user.selectOptions(screen.getByLabelText('Creator category'), 'category-technology')
    await user.selectOptions(screen.getByLabelText('Platform 1'), 'Instagram')
    await user.type(screen.getByLabelText('Social profile URL 1'), 'https://instagram.com/ada')
    await user.click(screen.getByRole('checkbox'))
    await user.upload(screen.getByLabelText('Profile image'), new File(['image'], 'profile.png', { type: 'image/png' }))
    await user.clear(screen.getByLabelText('Choose your amount'))
    await user.type(screen.getByLabelText('Choose your amount'), '12.50')
    await user.click(screen.getByRole('button', { name: /continue to payment.*\$12\.50/i }))

    await waitFor(() => expect(createEntry).toHaveBeenCalledOnce())
    expect(vi.mocked(createEntry).mock.calls[0][0]).toMatchObject({
      name: 'Ada Lovelace', username: 'ada', contribution: 12.5,
    })
    expect(confirmed).not.toHaveBeenCalled()
    expect(await screen.findByText(/checkout ready/i)).toBeInTheDocument()
    expect(screen.getByText(/profile is added after successful payment/i)).toBeInTheDocument()
  })

  it('allows editing and resubmission when a server error can be retried', async () => {
    vi.mocked(createEntry).mockRejectedValueOnce(new Error('Database unavailable')).mockResolvedValueOnce({} as never)
    const user = userEvent.setup()
    render(<LookupProvider><EnterRankingDialog isOpen onClose={vi.fn()} onConfirmed={vi.fn()} /></LookupProvider>)
    await user.type(screen.getByLabelText('Name'), 'Ada Lovelace')
    await user.type(screen.getByLabelText('Creator username'), 'ada')
    await user.selectOptions(screen.getByLabelText('Creator category'), 'category-technology')
    await user.selectOptions(screen.getByLabelText('Platform 1'), 'Instagram')
    await user.type(screen.getByLabelText('Social profile URL 1'), 'https://instagram.com/ada')
    await user.click(screen.getByRole('checkbox'))
    await user.upload(screen.getByLabelText('Profile image'), new File(['image'], 'profile.png', { type: 'image/png' }))
    await user.click(screen.getByRole('button', { name: /continue to payment/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Database unavailable')
    await user.click(screen.getByRole('button', { name: /continue to payment/i }))
    await waitFor(() => expect(createEntry).toHaveBeenCalledTimes(2))
    expect(vi.mocked(createEntry).mock.calls[0][1]).toBe(vi.mocked(createEntry).mock.calls[1][1])
  })
})

