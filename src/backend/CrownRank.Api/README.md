# CrownRank API

## Local PostgreSQL setup

Development now uses PostgreSQL. Create an empty local database and configure credentials without committing them:

```powershell
createdb -U postgres crownrank
$env:ConnectionStrings__CrownRank = "Host=localhost;Port=5432;Database=crownrank;Username=postgres;Password=your-password"
$env:CrownRank__AdminPassword = "choose-a-local-password"
dotnet run --project src/backend/CrownRank.Api
```

The checked-in Development connection string uses the conventional local `postgres/postgres` credentials only as a convenience. Override it with an environment variable or .NET user secrets on any machine where those credentials differ. Startup applies the provider-compatible EF Core migration and idempotently seeds the development categories and leaderboard entries.

SQLite remains available for the console and isolated automated tests. Set `CrownRank:DatabaseProvider` to `Sqlite` and `CrownRank:DatabasePath` when that provider is required.

## Frontend and profile images

Start the Vite frontend separately:

```powershell
npm --prefix src/frontend install
npm --prefix src/frontend run dev
```

The API accepts and validates profile uploads, resizes them to at most 300×250, saves the normalized PNG under `src/frontend/public/uploads/profiles`, and persists its public URL in PostgreSQL. Vite serves that directory at `http://localhost:5173/uploads/profiles`. The storage abstraction returns a public URL, so a future Azure Blob implementation can upload the same processed image and return its blob URL without changing Domain or Application workflows.

Generated uploads and local databases remain ignored by Git. Production deployments should use durable object storage rather than writing into a built frontend bundle.

## API workflow

Public routes are under `/api`: categories, legal versions, global and UTC daily leaderboards, public profiles, multipart entry submission, boost preview and checkout, and payment status/confirmation. Entry submission expects `name`, `username`, `categoryId`, `acceptedAgreements`, `amountInMinorUnits`, `currency`, a JSON `socialLinks` field, and an `image` file.

In Development, `POST /api/dev/payments/{attemptId}/outcome` accepts `Processing`, `Succeeded`, `Failed`, or `Cancelled` so the frontend can exercise every mock outcome. Never enable mock payments in a deployed production environment.

`POST /api/admin/login` accepts the configured password and returns an opaque short-lived bearer token. Send it as `Authorization: Bearer <accessToken>` to `/api/admin/...`. Protected routes inspect payment attempts and manage entries and categories. Login and mutation endpoints are rate limited; errors use RFC Problem Details.

The mock confirmation endpoint models backend verification but is not a Stripe webhook. Stripe work must add a signature-verifying webhook endpoint and a durable Stripe gateway/reconciliation implementation before live payments are possible.
