export function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(Number.isFinite(amount) ? amount : 0)
}

// Parse user input at cent precision before converting to the API's dollar amount.
export function parseAmount(value: string): number {
  if (!/^\d{1,5}(\.\d{1,2})?$/.test(value.trim())) return NaN
  const [whole, fraction = ''] = value.trim().split('.')
  const cents = Number(whole) * 100 + Number(fraction.padEnd(2, '0'))
  return cents >= 100 && cents <= 1_000_000 ? cents / 100 : NaN
}
