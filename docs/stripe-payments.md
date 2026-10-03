# Stripe payments

Entry submissions and boosts use Stripe-hosted Checkout. CrownRank charges USD by default; a $1 contribution adds 1 score before Stripe fees. Both operations accept $10–$10,000, with up to two decimal places.

## Configuration

In `src/backend/CrownRankApp.API/appsettings.json`:

```json
"Payments": {
  "Currency": "usd",
  "MinimumAmount": 10,
  "MaximumAmount": 10000,
  "TermsVersion": "2026-10-03",
  "PrivacyVersion": "2026-10-03"
},
"Stripe": {
  "SecretKey": "sk_test_...",
  "WebhookSecret": "whsec_...",
  "FrontendUrl": "http://localhost:5173",
  "LiveMode": false
}
```

Set the secret key for your Stripe test account and the signing secret from your current Stripe CLI listener. The secret key and signing secret are different credentials. Real values can be entered in appsettings as requested; they never belong in the frontend. No publishable key is needed for hosted Checkout redirects. Existing database connection settings are preserved.

`Payments` also supplies the frontend's currency, input limits, suggested amounts, and validation through `GET /api/payments/settings`. Changing limits requires restarting the backend. USD, EUR, and GBP are supported as two-decimal currencies. Changing the currency does not convert existing leaderboard scores or historical payments; use one currency for a running leaderboard.

Start with card payments, including wallets made available by Stripe's card method. Adaptive Pricing is disabled so the amount and charged currency match the stored operation. iDEAL is not enabled for this USD flow. Refunds and disputes do not automatically deduct scores; that policy needs a separate implementation.

## Run locally

From `src/backend`, update the existing database:

```powershell
dotnet ef database update --project CrownRankApp.Infrastructure --startup-project CrownRankApp.API
dotnet run --project CrownRankApp.API
```

From `src/frontend`:

```shell
npm ci
npm run dev
```

Use the API URL printed by the backend. Forward Stripe events to its payment webhook, for example:

```shell
stripe login
stripe listen --forward-to https://localhost:YOUR_API_PORT/api/payments/stripe-webhook --skip-verify
```

Put the listener's `whsec_...` value in `Stripe:WebhookSecret` and restart the API. Use `4242 4242 4242 4242`, a future expiry, and any three-digit CVC in test Checkout. See Stripe's [testing guide](https://docs.stripe.com/testing) for decline and authentication scenarios. Browser tests simulate the external Checkout boundary; integration tests do not charge Stripe or require real keys.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/payments/settings` | Public currency, limits, agreement versions |
| POST | `/api/payments/entry-checkout` | Validate and reserve an entry; return Checkout URL |
| POST | `/api/payments/boost-checkout` | Validate a target and amount; return Checkout URL |
| GET | `/api/payments/{id}` | Read minimal local payment/fulfillment status |
| POST | `/api/payments/{id}/confirm` | Retrieve Stripe state and fulfill a verified payment |
| POST | `/api/payments/{id}/resume` | Resume the existing Checkout session or recover its result |
| POST | `/api/payments/stripe-webhook` | Verify Stripe's signature and process authoritative state |

Payment IDs are random UUID capabilities. Keep return links private. Public status responses contain no customer email, billing data, draft profile, or Stripe secret. The supplied `session_id` query parameter is never proof of payment; confirmation uses the session associated with the stored payment ID.

All other API mutations require `X-Admin-Key`, matching `Admin:ApiKey`. An empty configured admin key disables administrative API mutations. Public creation and boosting through `/api/Entry` and `/api/Entry/{id}/boost` have been removed. Direct score operations remain internal for existing integration checks, with no public no-payment endpoint.

## Persistence and recovery

- A pending `PaymentOperation` stores its amount/currency, request fingerprint, purpose, session/payment intent IDs, agreement evidence, and fulfillment timestamps. Entry drafts are persisted separately from published entries as a validated JSON snapshot with lookup IDs and a stored image URL.
- Repeating a reference with the same details resumes its operation; changed details are rejected. The persisted operation ID determines the Stripe idempotency key. Stripe session creation parameters remain stable across retries.
- The webhook verifies the raw request body and signature, checks test/live environment, and handles completed, asynchronous success/failure, and expired events. It then retrieves current Stripe state, so stale events cannot reverse a successful payment.
- Amount, currency, metadata, and session association must match the stored operation. Payment receipt is persisted before fulfillment. A transaction claims fulfillment, creates the entry or increments its score, records one `ScoreAddition` with the operation ID, and commits fulfillment together. Duplicate or concurrent confirmations cannot credit twice.
- Score history uses the verified Stripe charge timestamp. A delayed webhook retains the payment's original UTC ranking date; an older payment cannot move an existing entry's tie timestamp backwards.
- The success page polls confirmation for a limited period and offers manual retry. A paid operation whose fulfillment needs recovery stays distinguishable from an unpaid operation.
- Returning to the cancel page does not cancel a Stripe session or imply failure. The visitor can resume the same session. Stripe expiry releases the username reservation; a new submission uses a new reference.
- A background worker checks pending payments every minute. It recovers saved sessions and paid-but-unfulfilled operations. For a session whose creation response was lost, it first retries the original idempotent request while its fixed expiry still permits creation; later it searches Stripe sessions in the original creation window. It releases a never-created operation only after checking Stripe and waiting two days. Failed scans remain retryable; individual failures do not stop other operations.
- Profile uploads are decoded, checked, resized to fit 300 × 250, stripped of metadata, and saved as WebP in `wwwroot/assets/profiles`. Use a persistent volume for this directory when hosting. Expired entry drafts/images are removed after seven days. Payment audit records remain in the database.
- Active boost checkouts prevent administrative deletion of their entry. Paid operations that cannot fulfill remain paid and require recovery/administrator attention, rather than silently being marked unpaid.

Webhook handling performs a short database transaction before acknowledging processed events. On processing failure it returns 503 so Stripe can retry. Unrelated verified events are acknowledged without awarding scores. Monitor recovery and webhook error logs, especially payments marked paid with a null fulfillment timestamp.

Before live use, set live credentials and `LiveMode: true`, configure the public HTTPS frontend URL, register the public HTTPS webhook with these Checkout event types, and ensure generated profile assets persist across deployments. This change does not add refund administration, automated disputes, tax calculation, or payout distribution.
