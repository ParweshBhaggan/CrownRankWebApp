# CrownRank

CrownRank is a fan-powered creator leaderboard built with React, TypeScript, ASP.NET Core (.NET 10), EF Core, and PostgreSQL. Public users do not create accounts or log in. Creator profiles contain one name field, a username, category, social links, and an optional photo. Location is not collected or included in the active data model.

## Local payment flow

1. Enter Ranking submits the complete profile and optional image to the API.
2. The backend saves a separate pending checkout record, not a creator/rank, then starts checkout with the configured provider.
3. The default `Mock` provider confirms immediately for development and automated tests. The optional `Stripe` provider redirects to hosted Stripe Checkout.
4. Stripe payments are recorded only after `/api/payments/webhook` verifies Stripe's signature and validates the Checkout Session metadata and total. That webhook atomically creates the creator, records the opening contribution, and removes the pending checkout record.
5. The Stripe return page polls backend payment status while the webhook completes, then refreshes the ranking.

Entry and Boost references are stable across retries. Provider requests and contribution writes are idempotent, so retrying the same checkout does not add the contribution twice. Pending checkout records are stored separately and are never returned by creator or ranking endpoints.

All creator mutation and checkout routes remain registered only in Development. There is no login, registration, location collection, deployment, or container work in this change.

## Run locally

Prerequisites: .NET 10 SDK, Node.js 22.18+ (or Node.js 24), and local PostgreSQL 16+.

Set `ConnectionStrings:Database` using .NET user secrets or environment variables. The API defaults to `http://localhost:5080`; the frontend defaults to `http://localhost:5173`. Use `src/frontend/.env.example` for a browser-facing API URL override.

Startup never creates, migrates, or updates the database schema. Migrations remain owner-managed.

This model change removes `Location`, introduces pending-entry and hiding fields, and replaces minor-unit money properties with decimal dollar amounts. When preparing your migration against existing data, preserve and convert the old contribution amounts: `1250` cents must become `12.50` dollars. EF may generate drop/add operations for renamed properties; review them before applying the migration. Do not reinterpret cents as whole dollars or discard the ledger unintentionally.

```bash
dotnet restore CrownRank.slnx
dotnet run --project src/backend/CrownRank.Api
```

```bash
cd src/frontend
npm ci
npm run dev
```

Swagger is available at `http://localhost:5080/swagger` in Development. The database starts empty unless you explicitly enable `SeedData:Enabled` through configuration. Optional seeding inserts sample creators only into an empty database and requires an already updated schema.

### Stripe sandbox

The application remains on the mock provider unless Stripe is deliberately enabled. Store all three values in .NET user secrets; do not put keys in `appsettings.json` or commit them.

```powershell
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." `
  --project src/backend/CrownRank.Api
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..." `
  --project src/backend/CrownRank.Api
dotnet user-secrets set "Payments:Provider" "Stripe" `
  --project src/backend/CrownRank.Api
```

Keep this running in a separate terminal while testing:

```powershell
stripe listen --forward-to http://localhost:5080/api/payments/webhook
```

The `whsec_...` value printed by that exact CLI listener must be the webhook secret stored above. Start the API and frontend, then use Stripe's test card `4242 4242 4242 4242`, any future expiry, and any CVC. Switch back to deterministic mock payments with:

```powershell
dotnet user-secrets set "Payments:Provider" "Mock" `
  --project src/backend/CrownRank.Api
```

## API contracts

| Method | Route | Behavior |
|---|---|---|
| GET | `/api/health` | Process health |
| GET | `/api/creators` | Visible, confirmed creators in global order |
| GET | `/api/creators/{id}` | Visible creator details |
| GET | `/api/rankings/daily/{date}` | Ranking for a UTC calendar date |
| POST | `/api/creators` | Pending entry; Development only |
| POST | `/api/payments/checkout` | Start a configured-provider Boost checkout; Development only |
| GET | `/api/payments/status/{referenceId}` | Read webhook-confirmed payment status; Development only |
| POST | `/api/payments/webhook` | Verify and process Stripe events when Stripe is enabled; Development only |
| DELETE | `/api/creators/{id}` | Hide a public profile; retain its ledger; Development only |

Entry uses multipart fields: `entryReference` (UUID), `name`, `username`, `category`, `initialAmount`, `socialProfilesJson`, and optional `image`.

Boost checkout uses JSON: `referenceId` (UUID), `creatorId`, `purpose` (`creator-boost`), `amount`, and `currency` (`USD`). Checkout responses contain `id`, `confirmed`, and an optional Stripe-hosted `url`. Entry responses contain `creatorId` and the same nested `session` contract.

A unique internal payment reference prevents duplicate credits; reusing a reference with different payment details is rejected. A browser success redirect is never accepted as proof of payment.

## Money and rankings

Money uses .NET `decimal`, PostgreSQL `numeric(18,2)`, and dollar-based API fields (`amount`, `initialAmount`, `totalContributed`, `dailyContributed`). The supported contribution range is $1.00–$10,000.00, with at most two decimal places. Inputs with fractional cents are rejected rather than rounded. The Stripe adapter converts a validated decimal to Stripe's required integer minor units only at the external API boundary; domain and stored values remain decimal dollars.

The backend orders higher scores first, then the time the creator reached that score, then creator ID for deterministic exact ties. The frontend preserves that order. Category-filtered boards number positions within the category.

Daily scores include confirmations within the selected UTC day. Historical results are derived from the retained ledger, not frozen snapshots. Later-day Boosts do not add to earlier dates. Hidden profiles are excluded from public boards and archives; their contributions remain stored. Hiding is moderation, not erasure of personal data or image files.

## Images and validation

JPEG, PNG, WebP, GIF, and BMP are accepted up to 8 MB. The server identifies actual image content and checks the 25-megapixel limit before full decoding. It uses the first frame of animated images, applies orientation, crops to 1024×1024, removes EXIF/ICC metadata, and writes WebP under ignored local profile uploads.

Usernames allow 2–40 letters, numbers, dots, underscores, or dashes. Names are required with an 80-character maximum. Entries require 1–5 HTTPS social links; platform-specific links must match the selected platform's hostname. Website links remain general HTTPS links.

## Checks

```bash
dotnet test CrownRank.slnx
cd src/frontend
npm test
npm run lint
npm run build
npx playwright install chromium # first browser-test run only
npm run test:e2e
```

Backend tests cover domain rules, separation of pending checkouts from creators, legacy interrupted-entry cleanup, idempotent mock and webhook confirmations, decimal Boost amounts, UTC ranking boundaries, ties, hiding without ledger deletion, social URL safety, EF model constraints, payment status, and the complete development HTTP API flow. API tests replace persistence and external adapters inside the test host, so they never read or update the developer database. PostgreSQL concurrency tests remain future work.

Frontend tests are split into fast logic tests, Vitest/Testing Library component and API-adapter tests, and a Playwright browser journey. They cover navigation, entry registration, leaderboard refresh, checkout contracts, webhook-status return handling, Boost confirmation, and the no-account experience.

## Later work

- Cleanup/expiry policy for abandoned pending Stripe entry sessions.
- Database integration tests for concurrent confirmations and entry conflicts.
- Server-side pagination and aggregate queries as the dataset grows.
- Profile correction and moderation workflows, and the remaining pre-launch review of operator details, provider eligibility, and ImageSharp licensing.

No license has been selected. All rights are reserved unless the repository owner adds one.
