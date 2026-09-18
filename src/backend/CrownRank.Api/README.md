# CrownRank API

Run the API in Development with .NET 10 and configuration supplied through environment variables:

```powershell
$env:CrownRank__AdminPassword = "choose-a-local-password"
dotnet run --project src/backend/CrownRank.Api
```

Development uses the SQLite database and local asset directory from `appsettings.json`, enables the mock gateway, and permits `http://localhost:5173` through CORS. Never enable mock payments in a deployed production environment. Use secret configuration for the administrator password; no API password is committed to source control.

Public routes are under `/api`: categories, legal versions, global and UTC daily leaderboards, public profiles, multipart entry submission, boost preview and checkout, and payment status/confirmation. Entry submission expects `name`, `username`, `categoryId`, `acceptedAgreements`, `amountInMinorUnits`, `currency`, a JSON `socialLinks` field, and an `image` file. In Development, `POST /api/dev/payments/{attemptId}/outcome` accepts `Processing`, `Succeeded`, `Failed`, or `Cancelled` so the frontend can exercise every mock outcome.

`POST /api/admin/login` accepts the configured password and returns an opaque short-lived bearer token. Send it as `Authorization: Bearer <accessToken>` to `/api/admin/...`. Protected routes inspect payment attempts and manage entries and categories. Login and mutation endpoints are rate limited; errors use RFC Problem Details.

The mock confirmation endpoint models backend verification but is not a Stripe webhook. Stripe work must add a signature-verifying webhook endpoint and a durable Stripe gateway/reconciliation implementation before live payments are possible.
