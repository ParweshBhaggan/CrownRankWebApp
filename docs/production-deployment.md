# Production hardening and deployment

This branch keeps the existing image data URL workflow, entry API and name/amount checkout body. Automated checks reduce regressions; they are not a security audit or a guarantee of production readiness.

## Test locally

Apply the migrations before starting the API, including `20261004010000_AddPaymentRecoveryChecks`:

```sh
cd src/backend
dotnet ef database update --project CrownRankApp.Infrastructure --startup-project CrownRankApp.API
```

The new migration adds a nullable payment recovery timestamp and index, not image or entry changes. Start the API in Development and the frontend as before. Existing local credentials are preserved. Test entry/image registration, boost, cancellation, retry and a paid success link opened in a fresh browser.

## Administrative access

Administrative mutation endpoints require `X-Admin-Key`, matching `Admin:ApiKey` (32–256 characters). An empty key disables administrative mutations; it does not disable public browsing or payments. Generate a random key and store it privately, for example in PowerShell:

```powershell
$env:Admin__ApiKey = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Never put this key in frontend code, a public environment variable or browser storage. It is a server/operator credential, not a multi-user login system. A future administrator UI requires proper authenticated sessions and roles instead of distributing this shared key.

## Production configuration

Production startup rejects secrets whose effective configuration provider is a JSON file, weak administrator keys, mismatched Stripe test/live keys and non-HTTPS or localhost frontend/CORS origins. Supply secrets through environment variables or a secrets provider. Development still accepts the current local configuration.

```text
ASPNETCORE_ENVIRONMENT=Production
Admin__ApiKey=<random-secret-at-least-32-characters>
ConnectionStrings__DefaultConnection=<production-connection-from-secret-store>
Stripe__SecretKey=<test-key-for-staging-or-live-key-for-production>
Stripe__WebhookSecret=<this-deployed-webhook-signing-secret>
Stripe__FrontendUrl=https://your-frontend.example
Stripe__LiveMode=false
Cors__AllowedOrigins__0=https://your-frontend.example
```

Use Production with test-mode Stripe for staging. Set LiveMode to true and use live credentials only after testing the deployed integration and checking your Stripe account configuration. Configure the public HTTPS webhook in Stripe; its signing secret is different from a local CLI listener's secret. Subscribe to the checkout events listed in `stripe-payments.md`.

Previously committed credentials remain unchanged as requested. Before going live, rotate exposed credentials yourself, use a dedicated least-privilege database account, and keep production secrets outside git. Nothing here provisions secrets or rotates accounts automatically.

When hosted behind a proxy, explicitly configure trusted forwarded headers and HTTPS routing for that deployment. Do not trust arbitrary client-supplied forwarding headers. Configure the frontend host's own HTTPS, security headers, CSP and caching; API headers cannot secure a separately hosted frontend. Keep dependencies updated and set appropriate edge request/body limits and rate limits.

## Health and payment recovery

`GET /health/live` checks that the API is alive. `GET /health/ready` checks database connectivity and the payments table with a five-second cancellation timeout; failure returns a generic 503. It is not an exhaustive schema check. Monitor both endpoints and route logs to private centralized storage with actionable alerts.

The recovery worker checks at most 20 receipts every 60 seconds, with a persisted five-minute cooldown and atomic claims across API instances. It verifies known Stripe sessions and retries unfulfilled boosts through the existing idempotent payment service. It never creates a checkout. Unpaid known sessions are checked within a seven-day expiry window; paid unfulfilled receipts remain eligible for review. A paid entry still missing its profile after 30 minutes emits a warning rather than inventing profile data or creating an incomplete entry.

These settings are configurable in `PaymentRecovery`. Disabling the worker does not disable webhook or browser confirmation. Failures remain retryable. Deploying this code alone does not provision logging, alerts or an on-call process.

## Backups and support

Take a database backup before migration. Configure encrypted backups and point-in-time recovery, then rehearse restoration into a separate database. Keep the previous deployable artifact available. Never test restoration over the live database.

If browser storage is lost after payment, the success page lets the user re-enter their original paid profile and image without another charge. Retain payment links privately; support must verify ownership before sharing one. If the user cannot recover the entry, use an explicit support/refund procedure rather than asking them to pay again.

Refunds and disputes are not automatically deducted from rankings. Decide and document that product policy before launch, and use Stripe Dashboard for authorized refunds. Do not make ad hoc SQL edits to ranking scores or payment audit records.

Before launch, test real Stripe test-mode checkout/webhooks on the deployed host, missed webhook recovery, repeated confirmation, cancellation, declined cards, lost browser drafts and image registration. Credential rotation, monitoring, backup restoration, load testing, deployment validation and refund policies remain operator responsibilities.
