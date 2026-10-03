export const payment_settings_endpoint = '/api/payments/settings'
export const entry_checkout_endpoint = '/api/payments/entry-checkout'
export const boost_checkout_endpoint = '/api/payments/boost-checkout'
export const payment_endpoint = (id: string) => `/api/payments/${encodeURIComponent(id)}`
export const confirm_payment_endpoint = (id: string) => `${payment_endpoint(id)}/confirm`
export const resume_payment_endpoint = (id: string) => `${payment_endpoint(id)}/resume`
