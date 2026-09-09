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

  it('sends the exact multipart entry contract with decimal dollars', async () => {
    let request: { url: string; init?: RequestInit } | undefined
    globalThis.fetch = async (input, init) => {
      request = { url: String(input), init }
      return Response.json({ id: 'creator-1' })
    }
    const draft = {
      firstName: ' Ada ', lastName: ' Lovelace ', username: ' ada ', category: 'technology', contribution: 12.5,
      socialLinks: [{ id: 'ui-only', platform: 'instagram', url: ' https://instagram.com/ada ' }],
    } satisfies RankingEntryDraft

    await createEntry(draft, 'entry-reference')

    expect(request?.url).toBe('http://localhost:5080/api/creators')
    expect(request?.init?.method).toBe('POST')
    const form = request?.init?.body as FormData
    expect(form.get('entryReference')).toBe('entry-reference')
    expect(form.get('firstName')).toBe('Ada')
    expect(form.get('lastName')).toBe('Lovelace')
    expect(form.get('username')).toBe('ada')
    expect(form.get('initialAmount')).toBe('12.50')
    expect(form.get('socialProfilesJson')).toBe('[{"platform":"instagram","url":"https://instagram.com/ada"}]')
  })

  it('calls global and daily ranking routes and resolves local assets', async () => {
    const paths: string[] = []
    globalThis.fetch = async input => {
      paths.push(String(input))
      return Response.json([{ id: '1', imageUrl: '/uploads/one.webp' }])
    }
    const repository = new ApiLeaderboardRepository()

    const global = await repository.getAll()
    const daily = await repository.getDaily('2026-09-08')

    expect(paths).toEqual(['http://localhost:5080/api/creators', 'http://localhost:5080/api/rankings/daily/2026-09-08'])
    expect(global[0].imageUrl).toBe('http://localhost:5080/uploads/one.webp')
    expect(daily[0].imageUrl).toBe('http://localhost:5080/uploads/one.webp')
    expect(resolveApiAsset('https://cdn.example/avatar.webp')).toBe('https://cdn.example/avatar.webp')
  })

  it('posts the mock Boost contract without a real payment provider', async () => {
    let body = ''
    globalThis.fetch = async (_input, init) => {
      body = String(init?.body)
      return Response.json({ id: 'mock-1', confirmed: true })
    }
    const payload = { referenceId: 'ref-1', creatorId: 'creator-1', purpose: 'creator-boost', amount: 2.5, currency: 'USD' } as const

    await expect(new MockPaymentGateway().createCheckout(payload)).resolves.toEqual({ id: 'mock-1', confirmed: true })
    expect(JSON.parse(body)).toEqual(payload)
  })
})
