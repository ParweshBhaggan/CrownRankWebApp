# CrownRank

CrownRank is a fan-powered creator leaderboard built with React, TypeScript, ASP.NET Core (.NET 10), EF Core, and PostgreSQL. Public users do not create accounts or log in. Creator profiles contain names, a username, category, social links, and an optional photo. Location is not collected or included in the active data model.

## Current local flow

1. Enter Ranking submits the complete profile and optional image to the API as a pending entry.
2. The existing mock-payment approach confirms a simulated checkout through the backend development adapter. No payment provider is contacted and no money is charged.
3. Confirmation records a contribution and makes the creator visible.
4. Boost records another simulated contribution against an existing visible creator.
5. Boards refresh after confirmation and explicit retries. Window focus does not restart in-flight requests; this avoids repeated cancellations while switching between the browser and debugger. Read requests time out with a retry message after 30 seconds.

Entry and checkout references are stable across retries in the current dialog. Closing and reopening the entry dialog preserves an unfinished attempt during the current page session. A full browser reload discards that in-memory draft; abandoned pending entries currently reserve their usernames and require local cleanup. Real checkout recovery, expiry, and cancellation screens remain future work.

All creator mutation and mock checkout routes are registered only in Development. There is no login, registration, admin UI, Stripe integration, deployment, or container work in this change.

## Run locally

Prerequisites: .NET 10 SDK, Node.js 22.18+ (or Node.js 24), and local PostgreSQL 16+.

Set `ConnectionStrings:Database` using .NET user secrets or environment variables. The API defaults to `http://localhost:5080`; the frontend defaults to `http://localhost:5173`. Use `src/frontend/.env.example` for a browser-facing API URL override.

**Create and apply your own migration before starting the updated API.** Startup never creates, migrates, or updates the database schema. Existing migration files and the model snapshot are deliberately untouched.

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

## API contracts

| Method | Route | Behavior |
|---|---|---|
| GET | `/api/health` | Process health |
| GET | `/api/creators` | Visible, confirmed creators in global order |
| GET | `/api/creators/{id}` | Visible creator details |
| GET | `/api/rankings/daily/{date}` | Ranking for a UTC calendar date |
| POST | `/api/creators` | Pending entry; Development only |
| POST | `/api/payments/checkout` | Confirm a simulated contribution; Development only |
| DELETE | `/api/creators/{id}` | Hide a public profile; retain its ledger; Development only |

Entry uses multipart fields: `entryReference` (UUID), `firstName`, `lastName`, `username`, `category`, `initialAmount`, `socialProfilesJson`, and optional `image`.

Mock checkout uses JSON: `referenceId` (UUID), `creatorId`, `purpose` (`ranking-entry` or `creator-boost`), `amount`, and `currency` (`USD`). Its response contains `id` and `confirmed`. Entry payments must match the pending opening amount. Boosts require a published, visible creator.

The backend serializes checkout writes per creator using a database row lock. A unique payment reference prevents duplicate credits; reusing a reference with different payment details is rejected. This mock orchestration must not be reused as proof of payment when integrating a real provider.

## Money and rankings

Money uses .NET `decimal`, PostgreSQL `numeric(18,2)`, and dollar-based API fields (`amount`, `initialAmount`, `totalContributed`, `dailyContributed`). The supported contribution range is $1.00–$10,000.00, with at most two decimal places. Inputs with fractional cents are rejected rather than rounded. Frontend input parsing uses cent precision internally, but the API and database represent decimal dollars.

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
```

Backend behavior tests cover pending publication, confirmation retries, Boost amounts, unconfirmed mock results, UTC ranking boundaries, ties, hiding without ledger deletion, and social URL validation. PostgreSQL row locking still needs verification against the local database; the application unit tests use an in-memory repository.

Frontend tests use Node's built-in test runner and TypeScript stripping, with no additional test dependency.

## Later work

- Durable checkout recovery and cancellation/expiry for abandoned pending entries.
- Database integration tests for concurrent confirmations and entry conflicts.
- Server-side pagination and aggregate queries as the dataset grows.
- Real payment integration with verified, idempotent provider events only when deliberately enabled.
- Profile correction and moderation workflows, and the remaining pre-launch review of operator details, provider eligibility, and ImageSharp licensing.

No license has been selected. All rights are reserved unless the repository owner adds one.
