import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiDelete, apiGet, apiPost, apiPut } from '../../src/shared/api/apiRequest'
import { redirectToUrl } from '../../src/shared/api/redirect'
import { daily_entry_endpoint } from '../../src/shared/api/endpoints/entryEndpoints'
import { get_by_name_category_endpoint } from '../../src/shared/api/endpoints/categoryEndpoints'

afterEach(() => vi.unstubAllGlobals())

describe('shared API requests', () => {
  it('returns typed GET models and forwards cancellation and credentials', async () => {
    const fetch = vi.fn(async () => Response.json([{ id: 'entry' }]))
    vi.stubGlobal('fetch', fetch)
    const signal = new AbortController().signal

    await expect(apiGet('/api/Entry', { signal, credentials: 'include' })).resolves.toEqual([{ id: 'entry' }])
    expect(fetch).toHaveBeenCalledWith('/api/Entry', { method: 'GET', signal, credentials: 'include' })
  })

  it.each([['POST', apiPost], ['PUT', apiPut]] as const)('serializes %s and preserves custom headers', async (method, send) => {
    const fetch = vi.fn(async () => Response.json({ id: 'created' }))
    vi.stubGlobal('fetch', fetch)

    await expect(send('/api/Category', { name: 'Science' }, { headers: new Headers({ 'X-Request-Id': 'ref' }) })).resolves.toEqual({ id: 'created' })
    expect(fetch).toHaveBeenCalledWith('/api/Category', {
      method,
      headers: { 'Content-Type': 'application/json', 'x-request-id': 'ref' },
      body: JSON.stringify({ name: 'Science' }),
    })
  })

  it('handles DELETE without a JSON response', async () => {
    const fetch = vi.fn(async () => new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetch)

    await expect(apiDelete('/api/Entry/entry')).resolves.toBeUndefined()
    expect(fetch).toHaveBeenCalledWith('/api/Entry/entry', { method: 'DELETE' })
  })

  it('propagates API failures through the method helpers', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ error: 'Cannot boost this entry.' }, { status: 409 })))

    await expect(apiPost('/api/Entry/entry/boost', { amount: 2 })).rejects.toMatchObject({ status: 409, message: 'Cannot boost this entry.' })
  })

  it('returns a URL without navigating until explicitly requested', async () => {
    const assign = vi.fn()
    vi.stubGlobal('window', { location: { href: 'https://crownrank.example/rankings', assign } })
    vi.stubGlobal('fetch', vi.fn(async () => Response.json('https://checkout.example/session')))

    const url = await apiGet<string>('/future-checkout')
    expect(assign).not.toHaveBeenCalled()
    redirectToUrl(url)
    expect(assign).toHaveBeenCalledWith('https://checkout.example/session')

    redirectToUrl('/daily')
    expect(assign).toHaveBeenLastCalledWith('https://crownrank.example/daily')
    expect(() => redirectToUrl('javascript:alert(1)')).toThrow('HTTP or HTTPS')
    expect(assign).toHaveBeenCalledTimes(2)
  })

  it('encodes endpoint path and query values', () => {
    expect(daily_entry_endpoint('2026-10-03&extra=1')).toBe('/api/Entry/daily?date=2026-10-03%26extra%3D1')
    expect(get_by_name_category_endpoint('Science / Tech')).toBe('/api/Category/name/Science%20%2F%20Tech')
  })
})

