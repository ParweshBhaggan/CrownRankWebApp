import { expect, test } from '@playwright/test'

const paymentId = '019b7335-1979-4f83-b5f3-4a83e4bb1a96'
const boostId = '019b7335-1979-4f83-b5f3-4a83e4bb1a97'
test('entry and boost checkout update rankings only after confirmed payment', async ({ page }) => {
  const entries = [{
    id: 'entry-1', name: 'Ada Lovelace', username: 'ada', score: 10,
    imgUrl: '/favicon.svg', createdDate: '2026-09-30T10:00:00Z',
    categories: [{ name: 'Technology' }, { name: 'Science' }],
    socialMediaPlatforms: [{ platformName: 'Instagram', url: 'https://instagram.com/ada' }],
  }]
  const today = new Date().toISOString().slice(0, 10)
  const dailyScores = new Map([['entry-1', 5]])
  let submitted: Record<string, unknown> | undefined
  let boosted = false
  let entered = false
  let confirmed = false
  await page.route(/^https?:\/\/[^/]+\/api\//, async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (path === '/api/payments/settings') return route.fulfill({ json: {
      currency: 'usd', minimumAmount: 10, maximumAmount: 10000, termsVersion: 'v1', privacyVersion: 'v1',
    } })
    if (path === '/api/Category') return route.fulfill({ json: [
      { id: 'technology-id', name: 'Technology' }, { id: 'science-id', name: 'Science' },
    ] })
    if (path === '/api/SocialMediaDefault') return route.fulfill({ json: [
      { id: 'instagram-id', name: 'Instagram' }, { id: 'facebook-id', name: 'Facebook' },
    ] })
    if (path === '/api/Entry/daily') return route.fulfill({ json: entries
      .filter(entry => dailyScores.has(entry.id)).map(entry => ({ entry, dailyScore: dailyScores.get(entry.id), scoreReachedDate: `${today}T10:00:00Z` }))
      .sort((a, b) => b.dailyScore! - a.dailyScore!) })
    if (path === '/api/Entry' && request.method() === 'GET') return route.fulfill({ json: [...entries].sort((a,b) => b.score - a.score) })
    if (path === '/api/payments/entry-submissions') {
      submitted = request.postDataJSON()
      return route.fulfill({ json: { id: paymentId, name: submitted!.name, amount: submitted!.amount } })
    }
    if (path === `/api/payments/${paymentId}/checkout`) {
      expect(request.postDataJSON()).toEqual({ name: 'Grace Hopper', amount: 12.5 })
      return route.fulfill({ json: { id: paymentId, url: 'https://checkout.stripe.com/entry', status: 'pending' } })
    }
    if (path.startsWith(`/api/payments/entries/${paymentId}/boost-checkout/`)) {
      expect(request.postDataJSON()).toEqual({ name: 'CrownRank creator boost', amount: 10.5 })
      confirmed = false
      return route.fulfill({ json: { id: boostId, url: 'https://checkout.stripe.com/boost', status: 'pending' } })
    }
    if (path.endsWith('/confirm')) {
      const boosting = path.includes(boostId)
      if (confirmed && !entered) {
        entered = true
        entries.push({ id: paymentId, name: submitted!.name as string, username: submitted!.username as string,
          score: submitted!.amount as number, imgUrl: '/favicon.svg', createdDate: `${today}T11:00:00Z`,
          categories: [{ name: 'Science' }], socialMediaPlatforms: [{ platformName: 'Facebook', url: 'https://facebook.com/grace' }] })
        dailyScores.set(paymentId, submitted!.amount as number)
      }
      if (confirmed && boosting && !boosted) {
        boosted = true
        entries.find(entry => entry.id === paymentId)!.score += 10.5
        dailyScores.set(paymentId, 23)
      }
      return route.fulfill({ json: { id: boosting ? boostId : paymentId, status: confirmed ? 'paid' : 'pending',
        fulfilled: confirmed, entryId: confirmed ? paymentId : null, amount: boosting ? 10.5 : 12.5, currency: 'usd' } })
    }
    throw new Error(`Unexpected API request: ${request.method()} ${path}`)
  })
  await page.route('https://checkout.stripe.com/**', route => route.fulfill({ contentType: 'text/html', body: '<h1>Stripe test checkout</h1>' }))
  await page.goto('/')
  await expect(page.getByText('Ada Lovelace').first()).toBeVisible()
  await page.getByRole('button', { name: 'Enter ranking', exact: true }).click()
  await page.getByLabel('Name', { exact: true }).fill('Grace Hopper')
  await page.getByLabel('Creator username').fill('grace')
  await page.getByLabel('Creator category').selectOption('science-id')
  await page.getByLabel('Platform 1', { exact: true }).selectOption('Facebook')
  await page.getByLabel('Social profile URL 1').fill('https://facebook.com/grace')
  await page.getByLabel('Profile image').setInputFiles({ name: 'profile.png', mimeType: 'image/png',
    buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aL1sAAAAASUVORK5CYII=', 'base64') })
  await page.getByRole('checkbox').check()
  await page.getByLabel('Choose your amount').fill('12.50')
  await page.getByRole('button', { name: /Continue to payment.*\$12.50/ }).click()
  await expect(page).toHaveURL('https://checkout.stripe.com/entry')
  expect(submitted).toMatchObject({ amount: 12.5, categoryId: 'science-id', acceptedAgreements: true,
    socialProfiles: [{ platformId: 'facebook-id', url: 'https://facebook.com/grace' }] })
  expect(submitted?.imageDataUrl).toMatch(/^data:image\/webp;base64,/)
  expect(entries).toHaveLength(1)
  await page.goto(`/payment/success?payment_id=${paymentId}`)
  await expect(page.getByRole('heading', { name: /Confirming your payment/ })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Payment confirmed' })).toHaveCount(0)
  confirmed = true
  await page.getByRole('button', { name: 'Check again' }).click()
  await expect(page.getByRole('heading', { name: 'Payment confirmed' })).toBeVisible()
  await page.getByRole('link', { name: 'View creator' }).click()
  await expect(page.getByRole('heading', { name: 'Grace Hopper' })).toBeVisible()
  await page.getByRole('button', { name: 'Boost this creator' }).click()
  await page.getByLabel('Boost amount').fill('10.50')
  await page.getByRole('button', { name: /Continue to payment/ }).click()
  await expect(page).toHaveURL('https://checkout.stripe.com/boost')
  expect(entries.find(entry => entry.id === paymentId)!.score).toBe(12.5)
  confirmed = true
  await page.goto(`/payment/success?payment_id=${boostId}`)
  await expect(page.getByRole('heading', { name: 'Payment confirmed' })).toBeVisible()
  await page.getByRole('link', { name: 'Back to rankings' }).click()
  await expect(page.getByText('$23.00', { exact: true })).toBeVisible()
  await page.getByRole('link', { name: 'Daily rank', exact: true }).click()
  await expect(page.getByText('$23.00', { exact: true })).toBeVisible()
  await expect(page.getByText('$5.00', { exact: true })).toBeVisible()
})
