# Stripe payments from the working recovery branch

This integration starts at `recovery_point/working_application`. The existing browser `profileImageDataUrl` helper is unchanged: it resizes to 300 × 250 and produces the same WebP data URL. The existing `EntryResponseDto`, `CreateAsync`, image field, category mapping, and social profile mapping remain in use. There is no new image upload endpoint, asset folder, image decoding dependency, or entry-submission endpoint.

## Flow

1. Continue to payment saves the original form, including its existing image data URL, in localStorage under a random checkout reference. Storage failure stops checkout before a charge can start.
2. The frontend posts only `{ "name": "Creator name", "amount": 12.50 }` to `POST /api/payments/entry-checkout/{referenceId}`. Boost checkout uses the same body and `POST /api/payments/entries/{entryId}/boost-checkout/{referenceId}`.
3. The API saves a small payment record and creates a single Stripe line item with a stable idempotency key. The browser opens the returned Stripe URL.
4. `/payment/success?payment_id=...` verifies the saved Stripe session through the backend. A return URL alone never proves payment.
5. Once paid, the original `postEntry` sends its unchanged form to `POST /api/Entry?paymentId=...`. The server uses the verified payment amount as the score and calls the existing entry service. The entry, score history, and payment fulfillment marker commit in one transaction. Retrying returns the existing entry.
6. Boosts use the existing `BoostScoreAsync` service and payment ID as its unique score-addition reference. Both verified webhooks and return-page confirmation can fulfill a boost; retries cannot award it twice.

Cancel returns to `/payment/cancel?payment_id=...`. An unpaid cancel return publishes nothing and offers Resume checkout for the same Stripe session. A completed payment is still recognized on either return page. Expired checkout cannot publish; the visitor can start a new form explicitly. Failed cards stay in Stripe's checkout for correction. Network failures retain the original form and reference; validation failures after payment allow correction without charging again. Paid receipt is saved independently before registration, so failure to register does not lose payment evidence.

Entry registration requires returning in the same browser and frontend origin, where the original form is retained. If the browser never returns, the webhook still records the payment, but cannot reconstruct a form that was not sent to the server. Boosts can fulfill from the webhook alone. Do not clear browser storage while completing an entry; keep payment links private because their random UUID acts as a capability.

## Setup

Keep your existing database connection and credentials. Configure these additional appsettings sections:

```json
"Payments": {
  "Currency": "usd",
  "MinimumAmount": 10,
  "MaximumAmount": 10000
},
"Stripe": {
  "SecretKey": "sk_test_...",
  "WebhookSecret": "whsec_...",
  "FrontendUrl": "http://localhost:5173",
  "LiveMode": false
}
```

Use the secret key from your Stripe test project. GitHub's secret protection previously rejected that key, so the branch intentionally leaves new Stripe credential fields blank; existing credentials are preserved. The signing secret must match the current webhook endpoint or CLI listener. `FrontendUrl` must match the origin used to fill the form; local HTTP is allowed and production requires HTTPS. Currency and limits are loaded by the frontend from `GET /api/payments/settings`. Restart the backend after changing settings.

From `src/backend`, apply the new migration and start the API:

```powershell
dotnet ef database update --project CrownRankApp.Infrastructure --startup-project CrownRankApp.API
dotnet run --project CrownRankApp.API
```

Then start the frontend from `src/frontend` with `npm ci` and `npm run dev`. Configure `VITE_API_URL` to your running API if needed. Forward Stripe events to that API, using the HTTPS port printed on startup:

```shell
stripe listen --forward-to https://localhost:7076/api/payments/stripe-webhook --skip-verify
```

Set `Stripe:WebhookSecret` to the listener's current `whsec_...`, then restart the API. In Stripe test mode use `4242 4242 4242 4242`, a future expiry and any CVC. For a deployed webhook subscribe to checkout.session.completed, checkout.session.async_payment_succeeded, checkout.session.async_payment_failed, and checkout.session.expired. The handler verifies the raw signature and test/live environment and retrieves current session state before applying it. Unrelated events are acknowledged without changing scores.

The migration only adds `CheckoutPayments`; it does not change the existing image or entry tables. If you previously applied migrations from the abandoned Stripe integration branch, create a backup and use the database schema associated with the recovery branch before applying this new branch's migration. Do not copy that abandoned branch's migrations into this integration.

## Checks and scope

CI builds the backend, checks migration consistency, and runs disposable PostgreSQL integration checks. Payment tests cover limits, concurrent checkout/entry/boost retries, payment verification, unchanged image data URLs, paid form correction, uncertain creation, expiry, amount mismatch, signed webhooks, and the existing HTTP registration endpoints. Frontend and browser checks exercise Stripe redirects, success/cancel handling, retained forms, and entry/boost ranking updates.

No live Stripe charges are made by tests. Browser checks simulate the external Stripe page; real Stripe behavior requires your test account and listener. Refund/dispute score deductions, tax calculation, and payout distribution are not added. This branch preserves the recovery branch's administrative endpoints; administration authentication remains a separate deployment concern.
