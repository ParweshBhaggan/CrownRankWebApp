import { expect, test } from '@playwright/test'

const ada = {
  id: 'creator-1', username: 'ada', firstName: 'Ada', lastName: 'Lovelace', displayName: 'Ada Lovelace',
  category: 'technology', imageUrl: '/assets/default-profile.svg', socialProfiles: [{ id: 'social-1', platform: 'instagram', url: 'https://instagram.com/ada' }],
  totalContributed: 10, dailyContributed: 10, joinedAt: '2026-09-01T00:00:00Z', scoreReachedAt: '2026-09-08T12:00:00Z',
}

test('visitor can navigate, enter the ranking, and Boost without an account', async ({ page }) => {
  const creators = [{ ...ada }]
  const corsHeaders = {
    'access-control-allow-origin': 'http://127.0.0.1:5173',
    'access-control-allow-methods': 'GET, POST, DELETE, OPTIONS',
    'access-control-allow-headers': 'content-type',
  }
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url())
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: corsHeaders })
    if (route.request().method() === 'POST' && url.pathname === '/api/creators') {
      const added = { ...ada, id: 'creator-2', username: 'grace', firstName: 'Grace', lastName: 'Hopper', displayName: 'Grace Hopper', totalContributed: 12.5, dailyContributed: 12.5 }
      creators.unshift(added)
      return route.fulfill({ json: added, status: 201, headers: corsHeaders })
    }
    if (route.request().method() === 'POST' && url.pathname === '/api/payments/checkout') {
      const payload = route.request().postDataJSON()
      const creator = creators.find(item => item.id === payload.creatorId)!
      creator.totalContributed += payload.amount
      creator.dailyContributed += payload.amount
      return route.fulfill({ json: { id: `mock-${payload.referenceId}`, confirmed: true }, headers: corsHeaders })
    }
    if (url.pathname === '/api/creators' || url.pathname.startsWith('/api/rankings/daily/')) return route.fulfill({ json: creators, headers: corsHeaders })
    return route.fulfill({ status: 404, headers: corsHeaders })
  })

  await page.goto('/')
  await expect(page.getByText('No account required').first()).toBeVisible()
  await expect(page.getByText('Ada Lovelace').first()).toBeVisible()
  await page.getByRole('button', { name: 'Enter ranking' }).first().click()
  await page.getByLabel('First name').fill('Grace')
  await page.getByLabel('Last name').fill('Hopper')
  await page.getByLabel('Creator username').fill('grace')
  await page.getByLabel('Social profile URL 1').fill('https://instagram.com/grace')
  await page.getByLabel(/I confirm/).check()
  await page.getByLabel('Choose your amount').fill('12.50')
  await page.getByRole('button', { name: /Continue with \$12.50/ }).click()
  await expect(page.getByText(/You’re ready for the crown/)).toBeVisible()
  await page.getByRole('button', { name: 'Back to leaderboard' }).click()
  await expect(page.getByText('Grace Hopper').first()).toBeVisible()

  await page.getByRole('button', { name: /Boost/ }).first().click()
  await page.getByLabel('Boost amount').fill('2.50')
  await page.getByRole('button', { name: /Confirm mock payment/ }).click()
  await expect(page.getByText('Boost confirmed')).toBeVisible()
  await page.getByRole('button', { name: 'Done' }).click()
  await expect(page.getByText('$15.00').first()).toBeVisible()

  await page.getByRole('link', { name: 'Global rank' }).click()
  await expect(page.getByRole('heading', { name: 'Global ranking' })).toBeVisible()
  await page.getByRole('link', { name: 'Categories' }).click()
  await expect(page.getByRole('heading', { name: 'Creator categories' })).toBeVisible()
})
