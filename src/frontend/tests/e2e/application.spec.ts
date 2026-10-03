import { expect, test } from '@playwright/test'

test('visitor enters, boosts, browses categories, and views daily scores', async ({ page }) => {
  const entries = [{
    id: 'entry-1', name: 'Ada Lovelace', username: 'ada', score: 10,
    imgUrl: '/favicon.svg', createdDate: '2026-09-30T10:00:00Z',
    categories: [{ name: 'Technology' }, { name: 'Science' }],
    socialMediaPlatforms: [{ platformName: 'Instagram', url: 'https://instagram.com/ada' }],
  }]
  const today = new Date().toISOString().slice(0, 10)
  const dailyScores = new Map([['entry-1', 5]])
  const boosts = new Set<string>()
  let submitted: Record<string, unknown> | undefined
  await page.route(/^https?:\/\/[^/]+\/api\//, async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    if (path === '/api/Category') return route.fulfill({ json: [
      { id: 'technology-id', name: 'Technology' }, { id: 'science-id', name: 'Science' },
    ] })
    if (path === '/api/SocialMediaDefault') return route.fulfill({ json: [
      { id: 'instagram-id', name: 'Instagram' }, { id: 'facebook-id', name: 'Facebook' },
    ] })
    if (path === '/api/Entry/daily') return route.fulfill({ json: entries
      .filter(entry => dailyScores.has(entry.id)).map(entry => ({ entry, dailyScore: dailyScores.get(entry.id), scoreReachedDate: `${today}T10:00:00Z` }))
      .sort((a, b) => b.dailyScore! - a.dailyScore!) })
    if (path.endsWith('/boost') && request.method() === 'POST') {
      const payload = request.postDataJSON()
      const entry = entries.find(item => item.id === path.split('/')[3])!
      if (!boosts.has(payload.referenceId)) {
        boosts.add(payload.referenceId)
        entry.score += payload.amount
        dailyScores.set(entry.id, (dailyScores.get(entry.id) ?? 0) + payload.amount)
      }
      return route.fulfill({ json: entry })
    }
    if (path === '/api/Entry' && request.method() === 'GET') return route.fulfill({ json: [...entries].sort((a,b) => b.score - a.score) })
    if (path === '/api/Entry' && request.method() === 'POST') {
      submitted = request.postDataJSON()
      entries.push({ ...submitted, id: 'entry-2', createdDate: '2026-09-30T11:00:00Z' } as typeof entries[number])
      dailyScores.set('entry-2', submitted!.score as number)
      return route.fulfill({ status: 201, json: entries.at(-1) })
    }
    throw new Error(`Unexpected API request: ${request.method()} ${path}`)
  })
  await page.goto('/')
  await expect(page.getByText('Ada Lovelace').first()).toBeVisible()
  await page.getByRole('button', { name: 'Enter ranking', exact: true }).click()
  await page.getByLabel('Name', { exact: true }).fill('Grace Hopper')
  await page.getByLabel('Creator username').fill('grace')
  await page.getByLabel('Creator category').selectOption('science-id')
  await page.getByLabel('Platform 1', { exact: true }).selectOption('Facebook')
  await page.getByLabel('Social profile URL 1').fill('https://facebook.com/grace')
  await page.getByLabel('Profile image').setInputFiles({
    name: 'profile.png', mimeType: 'image/png',
    buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aL1sAAAAASUVORK5CYII=', 'base64'),
  })
  await page.getByRole('checkbox').check()
  await page.getByLabel('Choose your amount').fill('12.50')
  await page.getByRole('button', { name: /Submit with €12.50/ }).click()
  await expect(page.getByText('Entry saved', { exact: true })).toBeVisible()
  expect(submitted).toMatchObject({ score: 12.5, categories: [{ name: 'Science' }], socialMediaPlatforms: [{ platformName: 'Facebook', url: 'https://facebook.com/grace' }] })
  expect(submitted?.imgUrl).toMatch(/^data:image\/webp;base64,/)
  await page.getByRole('button', { name: 'Back to leaderboard' }).click()
  await expect(page.getByText('Grace Hopper').first()).toBeVisible()
  await page.getByRole('button', { name: /Boost/ }).first().click()
  await page.getByLabel('Boost amount').fill('2.50')
  await page.getByRole('button', { name: /Confirm boost/ }).click()
  await expect(page.getByRole('heading', { name: 'Boost confirmed' })).toBeVisible()
  await page.getByRole('button', { name: 'Done', exact: true }).click()
  await expect(page.getByText('€15.00').first()).toBeVisible()
  await page.getByRole('link', { name: 'Categories', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Science', exact: true })).toBeVisible()
  await page.getByRole('link', { name: /Science/ }).click()
  await expect(page).toHaveURL(/rankings\?category=science-id/)
  await expect(page.getByRole('heading', { name: 'Global ranking', exact: true })).toBeVisible()
  await expect(page.getByText('Grace Hopper')).toBeVisible()
  await expect(page.getByText('Ada Lovelace')).toBeVisible()
  await page.getByRole('link', { name: 'Daily rank', exact: true }).click()
  await expect(page).toHaveURL(/\/daily$/)
  await expect(page.getByRole('heading', { name: 'Today’s ranking' })).toBeVisible()
  await expect(page.getByText('€15.00', { exact: true })).toBeVisible()
  await expect(page.getByText('€5.00', { exact: true })).toBeVisible()
  await expect(page.getByText('€10.00', { exact: true })).toHaveCount(0)
})
