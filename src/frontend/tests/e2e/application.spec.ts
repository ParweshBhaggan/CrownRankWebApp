import { expect, test } from '@playwright/test'

interface TestEntry {
  id: string
  name: string
  username: string
  scoreInMinorUnits: number
}

test('visitor can navigate, enter the ranking, and Boost without an account', async ({ page }) => {
  const entries: TestEntry[] = [{ id: 'entry-1', name: 'Ada Lovelace', username: 'ada', scoreInMinorUnits: 1000 }]
  let nextAttemptPurpose: 'entry' | 'boost' = 'entry'
  const corsHeaders = {
    'access-control-allow-origin': 'http://127.0.0.1:5173',
    'access-control-allow-methods': 'GET, POST, DELETE, OPTIONS',
    'access-control-allow-headers': 'content-type',
  }
  const rows = () => entries.map((entry, index) => ({
    entryId: entry.id,
    name: entry.name,
    username: entry.username,
    categoryId: 'category-technology',
    scoreInMinorUnits: entry.scoreInMinorUnits,
    currency: 'EUR',
    scoreReachedAtUtc: '2026-09-18T10:00:00Z',
    rank: index + 1,
  }))

  await page.route(/^https?:\/\/[^/]+\/api\//, async route => {
    const request = route.request()
    const url = new URL(request.url())
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers: corsHeaders })
    if (url.pathname === '/api/categories') {
      return route.fulfill({ json: [{ id: 'category-technology', name: 'Technology', description: 'Technology creators' }], headers: corsHeaders })
    }
    if (url.pathname === '/api/leaderboards/global' || url.pathname === '/api/leaderboards/daily') {
      return route.fulfill({ json: rows(), headers: corsHeaders })
    }
    if (request.method() === 'GET' && url.pathname.startsWith('/api/entries/')) {
      const id = url.pathname.split('/').at(-1)!
      const entry = entries.find(item => item.id === id)
      return entry
        ? route.fulfill({ json: {
            id: entry.id, name: entry.name, username: entry.username, categoryId: 'category-technology',
            imageUrl: '/assets/default-profile.svg',
            socialLinks: [{ platform: 'Instagram', url: `https://instagram.com/${entry.username}` }],
          }, headers: corsHeaders })
        : route.fulfill({ status: 404, headers: corsHeaders })
    }
    if (request.method() === 'POST' && url.pathname === '/api/entries') {
      entries.unshift({ id: 'entry-2', name: 'Grace Hopper', username: 'grace', scoreInMinorUnits: 1250 })
      nextAttemptPurpose = 'entry'
      return route.fulfill({ json: { entryId: 'entry-2', attemptId: 'attempt-entry', checkoutUrl: 'mock://entry' }, status: 202, headers: corsHeaders })
    }
    if (request.method() === 'POST' && url.pathname.endsWith('/boosts')) {
      const id = url.pathname.split('/')[3]
      const payload = request.postDataJSON()
      entries.find(entry => entry.id === id)!.scoreInMinorUnits += payload.amountInMinorUnits
      nextAttemptPurpose = 'boost'
      return route.fulfill({ json: { entryId: id, attemptId: 'attempt-boost', checkoutUrl: 'mock://boost' }, status: 202, headers: corsHeaders })
    }
    if (request.method() === 'POST' && url.pathname.includes('/outcome')) {
      return route.fulfill({ status: 204, headers: corsHeaders })
    }
    if (request.method() === 'POST' && url.pathname.endsWith('/confirm')) {
      return route.fulfill({ json: { state: 'Confirmed', purpose: nextAttemptPurpose }, headers: corsHeaders })
    }
    return route.fulfill({ status: 404, headers: corsHeaders })
  })

  await page.goto('/')
  await expect(page.getByText('No account required').first()).toBeVisible()
  await expect(page.getByText('Ada Lovelace').first()).toBeVisible()
  await page.getByRole('button', { name: 'Enter ranking' }).first().click()
  await page.getByLabel('First name').fill('Grace')
  await page.getByLabel('Last name').fill('Hopper')
  await page.getByLabel('Creator username').fill('grace')
  await page.getByLabel('Creator category').selectOption('technology')
  await page.getByLabel('Social profile URL 1').fill('https://instagram.com/grace')
  await page.getByLabel('Profile image').setInputFiles({
    name: 'profile.png',
    mimeType: 'image/png',
    buffer: Buffer.from('test-image'),
  })
  await page.getByLabel(/I confirm/).check()
  await page.getByLabel('Choose your amount').fill('12.50')
  await page.getByRole('button', { name: /Continue with €12.50/ }).click()
  await expect(page.getByText(/You’re ready for the crown/)).toBeVisible()
  await page.getByRole('button', { name: 'Back to leaderboard' }).click()
  await expect(page.getByText('Grace Hopper').first()).toBeVisible()

  await page.getByRole('button', { name: /Boost/ }).first().click()
  await page.getByLabel('Boost amount').fill('2.50')
  await page.getByRole('button', { name: /Confirm mock payment/ }).click()
  await expect(page.getByText('Boost confirmed')).toBeVisible()
  await page.getByRole('button', { name: 'Done' }).click()
  await expect(page.getByText('€15.00').first()).toBeVisible()

  await page.getByRole('link', { name: 'Global rank', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Global ranking' })).toBeVisible()
  await page.getByRole('link', { name: 'Categories' }).click()
  await expect(page.getByRole('heading', { name: 'Creator categories' })).toBeVisible()
})
