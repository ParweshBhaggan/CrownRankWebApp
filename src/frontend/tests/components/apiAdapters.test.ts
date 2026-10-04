import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiRequest, resolveApiAsset } from '../../src/shared/api/httpClient'
import { createEntry } from '../../src/features/creator-entry/data/createEntry'
import { ApiLeaderboardRepository } from '../../src/features/leaderboard/data/ApiLeaderboardRepository'
import { StripeCheckoutGateway } from '../../src/features/payments/data/StripeCheckoutGateway'
import type { RankingEntryDraft } from '../../src/features/creator-entry/domain/rankingEntry'

vi.mock('../../src/features/creator-entry/data/profileImage', () => ({ profileImageDataUrl: vi.fn(async () => 'data:image/webp;base64,test') }))

vi.mock('../../src/shared/api/redirect', () => ({ redirectToUrl: vi.fn() }))
import { getPendingEntry } from '../../src/features/creator-entry/data/pendingEntry'
import { redirectToUrl } from '../../src/shared/api/redirect'

const originalFetch = globalThis.fetch
afterEach(() => { globalThis.fetch = originalFetch; localStorage.clear() })

describe('frontend API adapters', () => {
  it('returns JSON and no-content and reports validation details', async () => {
    globalThis.fetch = async () => new Response(JSON.stringify({ value: 42 }), { status: 200 })
    await expect(apiRequest('/ok')).resolves.toEqual({ value: 42 })

    globalThis.fetch = async () => new Response(null, { status: 204 })
    await expect(apiRequest('/empty')).resolves.toBeUndefined()

    globalThis.fetch = async () => new Response(JSON.stringify({ errors: { request: ['Amount is invalid.'], social: ['Link is invalid.'] } }), {
      status: 400, headers: { 'Content-Type': 'application/problem+json' },
    })
    await expect(apiRequest('/invalid')).rejects.toMatchObject({ status: 400, message: 'Amount is invalid. Link is invalid.' })
  })

  it('reports an interrupted request with a useful retry message', async () => {
    globalThis.fetch = async () => { throw new TypeError('fetch failed') }

    await expect(apiRequest('/api/categories')).rejects.toThrow(
      'The API request was interrupted. Check that the backend is running, then retry.',
    )
  })

  it('loads defaults and submits JSON with a decimal score and names from the backend', async () => {
    const requests: { url: string; init?: RequestInit }[] = []
    globalThis.fetch = async (input, init) => {
      const url = String(input)
      requests.push({ url, init })
      if (url === '/api/Category') return Response.json([{ id: 'science-id', name: 'New science category' }])
      if (url === '/api/SocialMediaDefault') return Response.json([{ id: 'social-id', name: 'New platform' }])
      return Response.json({ id: 'ui-reference', url: 'https://checkout.stripe.com/test', status: 'pending' })
    }
    const draft = {
      name: ' Ada Lovelace ', username: ' ada ', category: 'science-id', contribution: 12.5,
      socialLinks: [{ id: 'ui-only', platform: 'New platform', url: ' https://example.com/ada ' }],
      profileImage: new File(['image'], 'profile.png', { type: 'image/png' }),
    } satisfies RankingEntryDraft
    await createEntry(draft, 'ui-reference')
    expect(requests.map(request => request.url)).toEqual(['/api/Category', '/api/SocialMediaDefault', '/api/payments/entry-checkout/ui-reference'])
    expect(requests[2].init?.headers).toEqual({ 'Content-Type': 'application/json' })
    expect(JSON.parse(String(requests[2].init?.body))).toEqual({ name: 'Ada Lovelace', amount: 12.5 })
    expect(redirectToUrl).toHaveBeenCalledWith('https://checkout.stripe.com/test')
    expect(getPendingEntry('ui-reference')).toEqual({
      name: 'Ada Lovelace', username: 'ada', imgUrl: 'data:image/webp;base64,test', score: 12.5,
      categories: [{ name: 'New science category', description: '' }],
      socialMediaPlatforms: [{ platformName: 'New platform', url: 'https://example.com/ada' }],
    })
  })

  it('retains the original image and checkout details after an uncertain network response', async () => {
    const checkoutBodies: unknown[] = []
    globalThis.fetch = async (input, init) => {
      if (String(input) === '/api/Category') return Response.json([{ id: 'science', name: 'Science' }])
      if (String(input) === '/api/SocialMediaDefault') return Response.json([])
      checkoutBodies.push(JSON.parse(String(init?.body)))
      if (checkoutBodies.length === 1) throw new TypeError('Lost response')
      return Response.json({ id: 'reference', url: 'https://checkout.stripe.com/recovered', status: 'pending' })
    }
    const draft = { name: 'Original name', username: 'creator', category: 'science', contribution: 12.5,
      socialLinks: [], profileImage: new File(['image'], 'avatar.png', { type: 'image/png' }) }
    await expect(createEntry(draft, 'reference')).rejects.toThrow('interrupted')
    await createEntry({ ...draft, name: 'Changed after interruption', contribution: 25 }, 'reference')
    expect(checkoutBodies).toEqual([{ name: 'Original name', amount: 12.5 }, { name: 'Original name', amount: 12.5 }])
    expect(getPendingEntry('reference')?.imgUrl).toBe('data:image/webp;base64,test')
  })

  it('stops before checkout when browser storage cannot retain the original form', async () => {
    const stored = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('Storage quota') })
    const urls: string[] = []
    globalThis.fetch = async input => {
      urls.push(String(input))
      return String(input) === '/api/Category' ? Response.json([{ id: 'science', name: 'Science' }]) : Response.json([])
    }
    try {
      await expect(createEntry({ name: 'Creator', username: 'creator', category: 'science', contribution: 10,
        socialLinks: [], profileImage: new File(['image'], 'avatar.png', { type: 'image/png' }) }, 'reference')).rejects.toThrow('browser could not save')
      expect(urls.some(url => url.includes('checkout'))).toBe(false)
    } finally { stored.mockRestore() }
  })

  it('maps entries including multiple categories and preserves backend ranking order', async () => {
    globalThis.fetch = async input => String(input) === '/api/Category'
      ? Response.json([{ id: 'technology-id', name: 'Technology' }, { id: 'science-id', name: 'Science' }])
      : Response.json([12.5, 10].map((score, index) => ({
          id: `entry-${index}`, name: 'Ada Lovelace', username: `ada${index}`, score,
          imgUrl: 'data:image/webp;base64,test', createdDate: '2026-09-30T10:00:00Z',
          categories: [{ name: 'Technology' }, { name: 'Science' }],
          socialMediaPlatforms: [{ platformName: 'Instagram', url: 'https://instagram.com/ada' }],
        })))
    const repository = new ApiLeaderboardRepository()
    const entries = await repository.getAll()
    expect(entries.map(entry => entry.totalContributed)).toEqual([12.5, 10])
    expect(entries[0]).toMatchObject({ category: 'technology-id', categories: ['technology-id', 'science-id'], socialProfiles: [{ platform: 'Instagram' }], imageUrl: 'data:image/webp;base64,test' })
    expect(resolveApiAsset('https://cdn.example/avatar.webp')).toBe('https://cdn.example/avatar.webp')
  })

  it('maps daily scores separately from global totals and preserves date tie order', async () => {
    const paths: string[] = []
    globalThis.fetch = async input => {
      paths.push(String(input))
      if (String(input) === '/api/Category') return Response.json([])
      return Response.json([2, 1].map(id => ({
        entry: { id: String(id), name: 'Creator', username: 'creator', score: 100,
          imgUrl: '/avatar.png', createdDate: '2026-01-01T00:00:00Z', categories: [], socialMediaPlatforms: [] },
        dailyScore: 2.5, scoreReachedDate: '2026-09-30T12:00:00Z',
      })))
    }
    const daily = await new ApiLeaderboardRepository().getDaily('2026-09-30')
    expect(paths).toContain('/api/Entry/daily?date=2026-09-30')
    expect(daily.map(entry => entry.id)).toEqual(['2', '1'])
    expect(daily[0]).toMatchObject({ totalContributed: 2.5, dailyContributed: 2.5, scoreReachedAt: '2026-09-30T12:00:00Z' })
  })

  it('starts boost checkout with only name and amount and a stable retry reference', async () => {
    globalThis.fetch = vi.fn(async () => Response.json({ id: 'ref', url: 'https://checkout.stripe.com/boost', status: 'pending' }))
    const request = { referenceId: 'ref', creatorId: 'entry', amount: 12.5 } as const
    await expect(new StripeCheckoutGateway().createCheckout(request)).resolves.toEqual({ id: 'ref', url: 'https://checkout.stripe.com/boost', status: 'pending' })
    expect(globalThis.fetch).toHaveBeenCalledWith('/api/payments/entries/entry/boost-checkout/ref', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: 'CrownRank creator boost', amount: 12.5 }),
    })
  })
})

