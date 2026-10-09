Payment integration based on `recovery_point/working_application`: see [Stripe setup and flow](docs/stripe-payments.md). Images keep the recovery branch's existing data URL handling; checkout takes only name and amount.

# CrownRank

CrownRank uses React/TypeScript, ASP.NET Core (.NET 10), EF Core, and PostgreSQL.

## Run locally

Install the .NET 10 SDK, Node.js 24, and PostgreSQL. Configure `Database:Provider` as `postgresql` and `ConnectionStrings:DefaultConnection` in API configuration or user secrets.

From the repository root:

```bash
dotnet restore src/backend/CrownRankApp.slnx
dotnet ef database update --project src/backend/CrownRankApp.Infrastructure --startup-project src/backend/CrownRankApp.API
dotnet run --project src/backend/CrownRankApp.API --launch-profile http
```

The HTTP API uses `http://localhost:5169`; the HTTPS profile uses `https://localhost:7076`. Both launch profiles open `/scalar` when started by an IDE or `dotnet watch`. Plain `dotnet run` does not launch a browser; open `http://localhost:5169/scalar` manually. Scalar and OpenAPI are available in Development.

```bash
cd src/frontend
npm ci
npm run dev
```

Vite proxies `/api` to `http://localhost:5169`. For a direct API connection, set `VITE_API_URL` and configure `Cors:AllowedOrigins` in the API (defaults include `http://localhost:5173` and `https://localhost:5173`).

## Current flow

- Categories and social platform options come from the backend, including newly added options.
- Entry checkout sends only name and amount to Stripe through the original backend. After verification, the existing form posts to `/api/Entry?paymentId=...`; a verified $12.50 payment becomes `score: 12.5`.
- The entry, selected existing categories, and social links are saved together. Invalid selections return a validation error; existing usernames return a conflict.
- The board loads all entries, orders them by score descending, and refreshes after submission. An entry belonging to several categories appears in each category.
- Boost checkout redirects to Stripe and applies a verified payment through the existing boost service. Default limits are $10–$10,000, configurable in appsettings; negative values and fractional cents are rejected.
- Global rankings sort by score descending, then `UpdatedDate ?? CreatedDate` ascending, then ID. Equal scores therefore prefer the entry that reached its score first. The frontend preserves this order.
- Daily rankings call `GET /api/Entry/daily?date=YYYY-MM-DD` and sum opening scores plus boosts added during that UTC calendar day. Equal daily scores prefer the earlier last addition timestamp, then entry ID. Global scores never reset.
- Each addition is stored in one small `ScoreAdditions` table. Boosts increment the total in SQL and save history in the same transaction, protecting against concurrent lost updates. The frontend sends a stable `referenceId` so retries do not credit the same boost twice.

## API contract used by the frontend

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/Category` | All categories with IDs, names, descriptions |
| GET | `/api/SocialMediaDefault` | All social platform IDs and names |
| GET | `/api/Entry` | Entries with decimal scores, categories, social profiles, and dates |
| POST | `/api/Entry` | Save an entry directly |
| POST | `/api/Entry/{id}/boost` | Add a positive amount to the score |
| GET | `/api/Entry/daily?date=YYYY-MM-DD` | UTC daily score ranking; defaults to today |

Example submission:

```json
{
  "name": "Ada Lovelace",
  "username": "ada",
  "imgUrl": "data:image/webp;base64,...",
  "score": 12.5,
  "categories": [{ "name": "Technology", "description": "" }],
  "socialMediaPlatforms": [{ "platformName": "Instagram", "url": "https://instagram.com/ada" }]
}
```

The current API accepts `imgUrl`, not a multipart upload. The frontend now keeps uploaded images up to 1200×1000, preserves their aspect ratio, uses high-quality canvas scaling, and stores a WebP data URL at 92% quality in `imgUrl`. This avoids enlarging a tiny 200–300px source on the creator profile page. A dedicated server asset upload service remains future work. Frontend image inputs accept JPG, PNG, and WebP up to 5 MB.

Entry API responses use DTOs to avoid serializing circular EF navigation properties. Apply the new `AddScoreAdditions` migration with the `dotnet ef database update` command above before starting this version. Existing entry totals remain unchanged. Daily history starts with additions recorded after this migration; older entries appear in daily rankings when boosted, and their pre-migration opening totals are not invented as historical additions.

Boost body example: `{ "amount": 2.5, "referenceId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" }`. The reference is optional for direct API use. Reusing a reference with different details returns HTTP 409. Missing entries return 404; invalid amounts return 400. Daily responses contain `entry` (including its global score), `dailyScore`, and `scoreReachedDate`.

## Checks

```bash
dotnet build src/backend/CrownRankApp.slnx
# Set CROWNRANK_TEST_CONNECTION to a PostgreSQL connection with CREATE DATABASE permission.
# The integration checks create and remove their own disposable database.
dotnet run --project src/backend/CrownRankApp.IntegrationTests
cd src/frontend
npm test
npm run lint
npm run build
npx playwright install chromium
npm run test:e2e
```

CI runs PostgreSQL integration checks for migrations, opening additions, positive-only boost validation, decimal accuracy, global and daily tie ordering, historical isolation, UTC midnight boundaries, concurrent boosts, and sequential/concurrent retry deduplication. The checks create a separate disposable database and never modify the database named in the supplied connection string.

Browser tests intercept API responses and verify registration, direct boost submission, leaderboard refresh, category filtering, and daily scores that differ from global totals.


## Endpoint logs

The API writes one JSON-formatted line per request into date-based text files under `src/backend/CrownRankApp.API/Logs`. Backend request activity is stored in `Logs/Backend/YYYY-MM-DD.txt`. Requests made through the React API client are marked with `X-CrownRank-Client: frontend` and are also stored in `Logs/Frontend/YYYY-MM-DD.txt`. A new file is selected automatically when the local calendar date changes. Request and response bodies, authorization tokens, Stripe signatures, and passwords are not logged. Generated log files are ignored by Git.
