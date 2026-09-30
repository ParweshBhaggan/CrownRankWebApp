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
- Entry submission sends JSON to `/api/Entry`. An opening amount of €12.50 becomes `score: 12.5`; no payment provider is called or money charged.
- The entry, selected existing categories, and social links are saved together. Invalid selections return a validation error; existing usernames return a conflict.
- The board loads all entries, orders them by score descending, and refreshes after submission. An entry belonging to several categories appears in each category.
- Boosts and daily rankings are unavailable because this backend has no boost endpoint or daily contribution history. Their UI explains this without calling retired endpoints.

## API contract used by the frontend

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/Category` | All categories with IDs, names, descriptions |
| GET | `/api/SocialMediaDefault` | All social platform IDs and names |
| GET | `/api/Entry` | Entries with decimal scores, categories, social profiles, and dates |
| POST | `/api/Entry` | Save an entry directly |

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

The current API accepts `imgUrl`, not a multipart upload. The frontend resizes uploaded images to fit within 300×250, preserves their aspect ratio, and stores the resulting WebP data URL in `imgUrl`. A dedicated server asset upload service remains future work. Frontend image inputs accept JPG, PNG, and WebP up to 5 MB.

Entry API responses use DTOs to avoid serializing circular EF navigation properties. No database model changes are introduced by this integration, so no new migration is needed beyond applying the repository's existing migrations.

## Checks

```bash
dotnet build src/backend/CrownRankApp.slnx
cd src/frontend
npm test
npm run lint
npm run build
npx playwright install chromium
npm run test:e2e
```

Browser tests intercept API responses and verify registration, decimal score submission, backend lookup options, category filtering, refresh, and unavailable feature states. They do not replace an end-to-end check against PostgreSQL.
