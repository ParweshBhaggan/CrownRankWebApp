import { afterEach, describe, expect, it } from 'vitest'
import { apiRequest, resolveApiAsset } from '../../src/shared/api/httpClient'
import { createEntry } from '../../src/features/creator-entry/data/createEntry'
import { ApiLeaderboardRepository } from '../../src/features/leaderboard/data/ApiLeaderboardRepository'
import { MockPaymentGateway } from '../../src/features/payments/data/MockPaymentGateway'
import type { RankingEntryDraft } from '../../src/features/creator-entry/domain/rankingEntry'

const originalFetch = globalThis.fetch
afterEach(() => { globalThis.fetch = originalFetch })

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

  it('submits the current multipart entry contract and confirms its mock payment', async () => {
    const requests: { url: string; init?: RequestInit }[] = []
    globalThis.fetch = async (input, init) => {
      const url = String(input)
      requests.push({ url, init })
      if (url.endsWith('/api/categories')) return Response.json([{ id: 'category-1', name: 'Technology' }])
      if (url.endsWith('/api/entries')) return Response.json({ entryId: 'entry-1', attemptId: 'attempt-1', checkoutUrl: 'mock://attempt-1' }, { status: 202 })
      if (url.includes('/outcome')) return new Response(null, { status: 204 })
      return Response.json({ state: 'Confirmed' })
    }
    const draft = {
      name: ' Ada Lovelace ', username: ' ada ', category: 'technology', contribution: 12.5,
      socialLinks: [{ id: 'ui-only', platform: 'instagram', url: ' https://instagram.com/ada ' }],
      profileImage: new File(['image'], 'profile.png', { type: 'image/png' }),
    } satisfies RankingEntryDraft

    await createEntry(draft, 'ui-reference')

    expect(requests.map(request => request.url)).toEqual([
      '/api/categories',
      '/api/entries',
      '/api/dev/payments/attempt-1/outcome',
      '/api/payments/attempt-1/confirm',
    ])
    const form = requests[1].init?.body as FormData
    expect(form.get('name')).toBe('Ada Lovelace')
    expect(form.get('username')).toBe('ada')
    expect(form.get('categoryId')).toBe('category-1')
    expect(form.get('amountInMinorUnits')).toBe('1250')
    expect(form.get('currency')).toBe('EUR')
    expect(form.get('socialLinks')).toBe('[{"platform":"Instagram","url":"https://instagram.com/ada"}]')
    expect(form.get('image')).toBe(draft.profileImage)
  })

  it('maps global and daily leaderboard rows with profiles and public image URLs', async () => {
    const paths: string[] = []
    globalThis.fetch = async input => {
      const url = String(input)
      paths.push(url)
      if (url.endsWith('/api/categories')) return Response.json([{ id: 'category-1', name: 'Technology' }])
      if (url.includes('/api/entries/entry-1')) return Response.json({
        id: 'entry-1', name: 'Ada Lovelace', username: 'ada', categoryId: 'category-1',
        imageUrl: 'http://localhost:5173/uploads/profiles/one.png',
        socialLinks: [{ platform: 'Instagram', url: 'https://instagram.com/ada' }],
      })
      return Response.json([{
        entryId: 'entry-1', name: 'Ada Lovelace', username: 'ada', categoryId: 'category-1',
        scoreInMinorUnits: 1250, currency: 'EUR', scoreReachedAtUtc: '2026-09-18T10:00:00Z', rank: 1,
      }])
    }
    const repository = new ApiLeaderboardRepository()

    const global = await repository.getAll()
    const daily = await repository.getDaily('2026-09-18')

    expect(paths).toContain('/api/leaderboards/global')
    expect(paths).toContain('/api/leaderboards/daily?date=2026-09-18')
    expect(global[0]).toMatchObject({
      id: 'entry-1', displayName: 'Ada Lovelace', category: 'technology',
      totalContributed: 12.5, imageUrl: 'http://localhost:5173/uploads/profiles/one.png',
    })
    expect(daily[0].dailyContributed).toBe(12.5)
    expect(resolveApiAsset('https://cdn.example/avatar.webp')).toBe('https://cdn.example/avatar.webp')
  })

  it('runs the boost mock-payment flow against entry endpoints', async () => {
    const requests: { url: string; body?: string }[] = []
    globalThis.fetch = async (input, init) => {
      const url = String(input)
      requests.push({ url, body: init?.body ? String(init.body) : undefined })
      if (url.endsWith('/boosts')) return Response.json({ attemptId: 'attempt-2' }, { status: 202 })
      if (url.includes('/outcome')) return new Response(null, { status: 204 })
      return Response.json({ state: 'Confirmed' })
    }
    const payload = { referenceId: 'ref-1', creatorId: 'entry-1', purpose: 'creator-boost', amount: 2.5, currency: 'EUR' } as const

    await expect(new MockPaymentGateway().createCheckout(payload)).resolves.toEqual({ id: 'attempt-2', confirmed: true })
    expect(requests.map(request => request.url)).toEqual([
      '/api/entries/entry-1/boosts',
      '/api/dev/payments/attempt-2/outcome',
      '/api/payments/attempt-2/confirm',
    ])
    expect(JSON.parse(requests[0].body!)).toEqual({ amountInMinorUnits: 250, currency: 'EUR' })
  })
})
