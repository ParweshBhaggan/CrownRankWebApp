export const payment_settings_endpoint = '/api/payments/settings'
export const entry_submission_endpoint = '/api/payments/entry-submissions'
export const boost_checkout_endpoint = (entryId: string, referenceId: string) => `/api/payments/entries/${encodeURIComponent(entryId)}/boost-checkout/${encodeURIComponent(referenceId)}`
export const payment_endpoint = (id: string) => `/api/payments/${encodeURIComponent(id)}`
export const confirm_payment_endpoint = (id: string) => `${payment_endpoint(id)}/confirm`
export const resume_payment_endpoint = (id: string) => `${payment_endpoint(id)}/resume`

export const entry_checkout_endpoint = (id: string) => `${payment_endpoint(id)}/checkout`
