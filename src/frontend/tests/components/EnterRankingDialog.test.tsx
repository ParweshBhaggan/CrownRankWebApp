import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { EnterRankingDialog } from '../../src/features/creator-entry/ui/EnterRankingDialog'
import { createEntry } from '../../src/features/creator-entry/data/createEntry'

vi.mock('../../src/features/creator-entry/data/createEntry', () => ({ createEntry: vi.fn() }))

describe('EnterRankingDialog', () => {
  beforeEach(() => vi.mocked(createEntry).mockReset())

  it('blocks invalid entry data and shows field errors without calling the API', async () => {
    const user = userEvent.setup()
    render(<EnterRankingDialog isOpen onClose={vi.fn()} onConfirmed={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: /continue with/i }))

    expect(await screen.findByText(/enter a name/i)).toBeInTheDocument()
    expect(screen.getByText(/use 2–40 letters/i)).toBeInTheDocument()
    expect(screen.getByText(/confirm the declaration/i)).toBeInTheDocument()
    expect(createEntry).not.toHaveBeenCalled()
  })

  it('submits a valid no-account entry and displays payment confirmation', async () => {
    vi.mocked(createEntry).mockResolvedValue({ creatorId: 'creator-1', session: { id: 'mock-1', confirmed: true } })
    const user = userEvent.setup()
    const confirmed = vi.fn()
    render(<EnterRankingDialog isOpen onClose={vi.fn()} onConfirmed={confirmed} />)

    await user.type(screen.getByLabelText('Name'), 'Ada Lovelace')
    await user.type(screen.getByLabelText('Creator username'), 'ada')
    await user.type(screen.getByLabelText('Social profile URL 1'), 'https://instagram.com/ada')
    await user.click(screen.getByRole('checkbox'))
    await user.clear(screen.getByLabelText('Choose your amount'))
    await user.type(screen.getByLabelText('Choose your amount'), '12.50')
    await user.click(screen.getByRole('button', { name: /continue with \$12\.50/i }))

    await waitFor(() => expect(createEntry).toHaveBeenCalledOnce())
    expect(vi.mocked(createEntry).mock.calls[0][0]).toMatchObject({
      name: 'Ada Lovelace', username: 'ada', contribution: 12.5,
    })
    expect(confirmed).toHaveBeenCalledOnce()
    expect(await screen.findByText(/you’re ready for the crown/i)).toBeInTheDocument()
    expect(screen.getByText(/profile is now on the leaderboard/i)).toBeInTheDocument()
  })

  it('keeps the same attempt available when a server error can be retried', async () => {
    vi.mocked(createEntry).mockRejectedValueOnce(new Error('Database unavailable')).mockResolvedValueOnce({ creatorId: 'creator-1', session: { id: 'mock-1', confirmed: true } })
    const user = userEvent.setup()
    render(<EnterRankingDialog isOpen onClose={vi.fn()} onConfirmed={vi.fn()} />)
    await user.type(screen.getByLabelText('Name'), 'Ada Lovelace')
    await user.type(screen.getByLabelText('Creator username'), 'ada')
    await user.type(screen.getByLabelText('Social profile URL 1'), 'https://instagram.com/ada')
    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: /continue with/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Database unavailable')
    await user.click(screen.getByRole('button', { name: /continue with/i }))
    await waitFor(() => expect(createEntry).toHaveBeenCalledTimes(2))
    expect(vi.mocked(createEntry).mock.calls[0][1]).toBe(vi.mocked(createEntry).mock.calls[1][1])
  })
})
