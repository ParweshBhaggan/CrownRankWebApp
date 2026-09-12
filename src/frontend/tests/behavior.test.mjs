import test from 'node:test'
import assert from 'node:assert/strict'
import { parseAmount, formatCurrency } from '../src/shared/format/currency.ts'
import { rankCreators } from '../src/features/leaderboard/application/rankCreators.ts'
import { validateRankingEntry } from '../src/features/creator-entry/application/validateRankingEntry.ts'
import { archivedDateKeys, resolveDailyDate, todayKey } from '../src/features/leaderboard/application/dailyDates.ts'

test('decimal amounts retain cent precision and display in dollars', () => {
  assert.equal(parseAmount('12.50'), 12.5)
  assert.equal(parseAmount('1.01'), 1.01)
  assert.equal(parseAmount('10000.00'), 10000)
  assert.equal(formatCurrency(12.5), '$12.50')
})
test('invalid or out-of-range amounts are rejected instead of rounded', () => {
  for (const value of ['', '0', '-1', '1.001', '10000.01', 'Infinity', 'NaN', '1e2']) assert.ok(Number.isNaN(parseAmount(value)), value)
})
test('ranking preserves authoritative API order without mutating creators', () => {
  const older = { id: 'a', totalContributed: 20, joinedAt: '2026-01-01', scoreReachedAt: '2026-09-08T12:00:00Z' }
  const newer = { id: 'b', totalContributed: 20, joinedAt: '2026-09-01', scoreReachedAt: '2026-09-08T11:00:00Z' }
  assert.deepEqual(rankCreators([newer, older]).map(x => x.id), ['b', 'a'])
  assert.equal(older.rank, undefined)
  assert.deepEqual(rankCreators([]), [])
})
const draft = { name: 'Ada Lovelace', username: 'ada', category: 'technology', contribution: 12.5, socialLinks: [{ id: '1', platform: 'instagram', url: 'https://www.instagram.com/ada' }] }
test('entry validation accepts valid data and rejects misleading social hosts', () => {
  assert.deepEqual(validateRankingEntry(draft), {})
  for (const url of ['http://instagram.com/ada', 'https://instagram.com.evil.example/ada', 'https://user:pass@instagram.com/ada']) {
    assert.ok(validateRankingEntry({ ...draft, socialLinks: [{ ...draft.socialLinks[0], url }] }).socialLinks)
  }
})
test('entry limits match the backend money and profile rules', () => {
  assert.ok(validateRankingEntry({ ...draft, contribution: 1.001 }).contribution)
  assert.ok(validateRankingEntry({ ...draft, name: 'a'.repeat(161) }).name)
  assert.ok(validateRankingEntry({ ...draft, socialLinks: Array(6).fill(draft.socialLinks[0]) }).socialLinks)
  assert.ok(validateRankingEntry({ ...draft, socialLinks: [draft.socialLinks[0], { ...draft.socialLinks[0], id: '2' }] }).socialLinks)
})
test('daily dates are UTC-stable and invalid or future archive routes fall back to today', () => {
  const now = new Date('2026-03-01T00:30:00Z')
  assert.equal(todayKey(now), '2026-03-01')
  assert.deepEqual(archivedDateKeys(now, 3), ['2026-03-01', '2026-02-28', '2026-02-27'])
  assert.equal(resolveDailyDate('2026-02-28', now), '2026-02-28')
  for (const invalid of [undefined, '2026-02-30', '2026-03-02', 'not-a-date']) {
    assert.equal(resolveDailyDate(invalid, now), '2026-03-01')
  }
})
