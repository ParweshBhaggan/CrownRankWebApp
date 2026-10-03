# Stripe payments

Entry checkout and boosts use Stripe-hosted Checkout. CrownRank charges USD by default; a $1 contribution adds 1 score before Stripe fees. Both operations accept $10–$10,000, with up to two decimal places.

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

GitHub secret protection prevents committing the supplied Stripe test key. A separately provided local appsettings.json contains the example's test credentials and preserves the existing database connection. Place it in CrownRankApp.API after checkout. Replace its signing secret with the value from your current Stripe CLI listener when starting a new listener. The secret key and signing secret are different credentials. Real values can be entered in appsettings as requested; they never belong in the frontend. No publishable key is needed for hosted Checkout redirects. Existing database connection settings are preserved.

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
| POST | `/api/profile-images/{referenceId}` | Upload multipart `file` and return its asset URL |
| POST | `/api/payments/entry-checkout/{referenceId}` | Accept only `{ name, amount }`; return Checkout URL |
| POST | `/api/Entry?paymentId={referenceId}` | Register the original entry form after backend payment verification |
| POST | `/api/payments/entries/{entryId}/boost-checkout/{referenceId}` | Accept only `{ name, amount }`; the target and retry reference are in the route |
| GET | `/api/payments/{id}` | Read minimal local payment/fulfillment status |
| POST | `/api/payments/{id}/confirm` | Verify payment; fulfill boosts and registered entries |
| POST | `/api/payments/{id}/resume` | Resume the existing Checkout session or recover its result |
| POST | `/api/payments/stripe-webhook` | Verify Stripe's signature and process authoritative state |

Payment IDs are random UUID capabilities. Keep return links private. Public status responses contain no customer email, billing data, draft profile, or Stripe secret. The supplied `session_id` query parameter is never proof of payment; confirmation uses the session associated with the stored payment ID.

Profile image uploads and paid entry registration are public; registration independently checks its payment reference. All other API mutations require `X-Admin-Key`, matching `Admin:ApiKey`. An empty configured admin key disables administrative API mutations. Entry creation through `/api/Entry` requires a verified, unused entry payment. Public boosting through `/api/Entry/{id}/boost` has been removed. Direct score operations remain internal for existing integration checks, with no public no-payment endpoint.

## Persistence and recovery

- A pending `PaymentOperation` stores its amount/currency, request fingerprint, purpose, session/payment intent IDs, agreement evidence, and fulfillment timestamps. Entry checkout stores only payment information and the display name. The frontend retains the existing entry form and uploaded image URL in localStorage under the stable payment reference before redirecting.
- Repeating a reference with the same details resumes its operation; changed details are rejected. The persisted operation ID determines the Stripe idempotency key. Stripe session creation parameters remain stable across retries.
- The webhook verifies the raw request body and signature, checks test/live environment, and handles completed, asynchronous success/failure, and expired events. It then retrieves current Stripe state, so stale events cannot reverse a successful payment.
- Amount, currency, metadata, and session association must match the stored operation. Payment receipt is persisted before fulfillment. After payment, the browser calls the existing `postEntry` flow with name, username, imgUrl, categories, socialMediaPlatforms, score, and agreement acceptance. The server ignores the supplied score and uses the verified payment amount. A transaction validates the profile, claims fulfillment, creates the entry or increments its score, and records one `ScoreAddition` with the payment ID. Duplicate or concurrent confirmations cannot credit twice.
- Score history uses the verified Stripe charge timestamp. A delayed webhook retains the payment's original UTC ranking date; an older payment cannot move an existing entry's tie timestamp backwards.
- The success page verifies payment, registers the retained entry form, and clears browser data only after successful registration. Network interruptions retain the form for retry. Registration validation errors allow editing the paid profile without creating another checkout or charge. Payment confirmation and registration retries are safe under concurrent requests.
- Returning to the cancel page does not cancel a Stripe session or imply failure. The visitor can resume the same session. An expired checkout cannot register an entry; a new checkout uses a new reference. No username is reserved before entry checkout.
- A background worker checks pending payments every minute. It recovers saved sessions and fulfillment for boosts or profiles already present on the server. A paid entry without a form waits for the browser to submit it and remains recorded as paid. For a session whose creation response was lost, it first retries the original idempotent request while its fixed expiry still permits creation; later it searches Stripe sessions in the original creation window. It releases a never-created operation only after checking Stripe and waiting two days. Failed scans remain retryable; individual failures do not stop other operations.
- Profile uploads are decoded, checked, resized to fit 300 × 250, stripped of metadata, and saved as WebP before checkout. Development uses frontend public assets; production uses configurable persistent API storage. Legacy expired drafts/images from earlier versions are removed after seven days. Payment audit records remain in the database.
- Active boost checkouts prevent administrative deletion of their entry. Registration locks its selected categories/platforms while creating the entry, and active legacy entry drafts protect their lookup references. Paid operations that cannot fulfill remain paid and require recovery/administrator attention, rather than silently being marked unpaid.

Webhook handling performs a short database transaction before acknowledging processed events. On processing failure it returns 503 so Stripe can retry. Unrelated verified events are acknowledged without awarding scores. Monitor recovery and webhook error logs, especially payments marked paid with a null fulfillment timestamp.

Before live use, set live credentials and `LiveMode: true`, configure the public HTTPS frontend URL, register the public HTTPS webhook with these Checkout event types, and ensure generated profile assets persist across deployments. This change does not add refund administration, automated disputes, tax calculation, or payout distribution.

### Checkout payload and backend notation

Both checkout endpoints take `CheckoutRequest(string Name, decimal Amount)`, following the supplied ZIP example. The stable retry reference is in the route. Entry checkout returns the Stripe URL immediately without posting or storing the full entry profile. The old entry form payload is registered through `POST /api/Entry?paymentId=...` only after server verification, with `acceptedAgreements` added for consent evidence. Categories and social platforms still use the names returned by the existing lookup API.

The form is retained in the original browser and origin across Stripe navigation, cancellation, and page reloads. Set `Stripe:FrontendUrl` to the same frontend origin used to fill the form. Finish on the success/return page; a webhook records payment even if the visitor loses their connection, but it cannot reconstruct a profile that has not been submitted. Reopen the return link in the same browser to retry registration. If browser data is deleted, entry details must be supplied again; the backend still retains the paid payment reference. Paid entries with no registered profile are distinguishable in payment status and server records.

The Stripe gateway takes the same two-field DTO alongside a separate server-owned context containing the operation ID, currency, and dates. It never takes an entry or payment entity. Stripe receives a single product name and amount plus the currency, internal reference, expiry, redirect URLs, and idempotency key needed for reliable confirmation. Profile images, usernames, categories, and social links stay inside CrownRank.

Backend C# uses explicit method bodies, braces for control flow, four-space indentation, expanded initializers, and single-line declaration parameter lists. `.editorconfig` records these preferences. Query/expression-tree selectors still use the lambda syntax required by LINQ and EF; declaration bodies do not use expression arrows.


### Profile image uploads

Before opening Stripe, the frontend uploads the selected file to `POST /api/profile-images/{referenceId}` as multipart field `file`. The API validates the actual JPEG, PNG, or WebP bytes (maximum 5 MB), resizes to at most 300 × 250, removes metadata, saves a WebP asset, and returns `{ "url": "/assets/profiles/...webp" }`. The pending browser form and final entry registration contain that URL. Checkout still accepts only name and amount.

In development, files are saved under `src/frontend/public/assets/profiles/`. The API also serves this directory at `/assets/profiles`, including files uploaded after startup. Generated uploads are ignored by Git. Set `ProfileImages:StoragePath` in appsettings to override the storage root (the API adds `assets/profiles` underneath it). In production the default root is the API's `wwwroot`; configure a persistent writable volume or replace storage with a blob storage implementation before deploying to an ephemeral host. A deployed browser cannot write to a frontend source folder.

Asset filenames include the checkout reference and content hash: retries reuse an existing asset, replacement images get distinct URLs, and registration rejects a URL belonging to another reference. Entry validation failures retain uploaded assets so the paid form can be corrected and retried. The previous base64 form format remains accepted to recover already-paid entries: pull this branch, restart the API, and use **Check again** on the existing payment return page. This does not create another Stripe payment.
