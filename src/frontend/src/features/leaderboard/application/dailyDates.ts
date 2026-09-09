export const dateKey = (date: Date): string => date.toISOString().slice(0, 10)

export const todayKey = (now = new Date()): string => dateKey(now)

export function archivedDateKeys(now = new Date(), count = 14): readonly string[] {
  return Array.from({ length: count }, (_, index) => {
    const date = new Date(now)
    date.setUTCDate(date.getUTCDate() - index)
    return dateKey(date)
  })
}

export function resolveDailyDate(requested: string | undefined, now = new Date()): string {
  const today = todayKey(now)
  if (!requested || !/^\d{4}-\d{2}-\d{2}$/.test(requested) || requested > today) return today
  const parsed = new Date(`${requested}T00:00:00Z`)
  return Number.isFinite(parsed.valueOf()) && dateKey(parsed) === requested ? requested : today
}
