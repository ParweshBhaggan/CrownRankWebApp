import type { PaymentSettings } from '../api/models/payment'

export const defaultPaymentSettings: PaymentSettings = {
  currency: 'usd', minimumAmount: 10, maximumAmount: 10000,
}
let currentSettings = defaultPaymentSettings

export function configurePaymentSettings(settings: PaymentSettings): void
{
  currentSettings = settings
}
export function formatCurrency(amount: number, currency = currentSettings.currency): string
{
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: currency.toUpperCase() }).format(
    Number.isFinite(amount) ? amount : 0,
  )
}
export function amountRange(settings = currentSettings): string
{
  return `${formatCurrency(settings.minimumAmount, settings.currency)}–${formatCurrency(settings.maximumAmount, settings.currency)}`
}
// Parse at cent precision, rejecting excess decimals rather than rounding a payment.
export function parseAmount(value: string, settings = currentSettings): number
{
  if (!/^\d{1,6}(\.\d{1,2})?$/.test(value.trim())) return NaN
  const [whole, fraction = ''] = value.trim().split('.')
  const cents = Number(whole) * 100 + Number(fraction.padEnd(2, '0'))
  return cents >= Math.round(settings.minimumAmount * 100) && cents <= Math.round(settings.maximumAmount * 100)
    ? cents / 100 : NaN
}
