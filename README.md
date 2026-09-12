# CrownRank

CrownRank is a public creator leaderboard with a React frontend and an ASP.NET Core/PostgreSQL API. Users do not register or log in. Creator records use one `Name` field and do not store location.

## Backend foundation

The rewritten backend deliberately uses one API project. EF Core is the unit of work; there are no repository wrappers or duplicated persistence abstractions. Creating a creator and its opening contribution uses one `SaveChangesAsync` call, which EF executes transactionally. A mock Boost likewise uses one database write.

Database uniqueness constraints protect usernames, entry references, and payment references. Repeating the same entry or Boost reference with the same details is idempotent; reusing it for different details returns HTTP 409. Invalid input returns RFC-style problem details instead of an unhandled exception.

Mock payments are the only provider in this foundation. They confirm immediately and never contact Stripe. Stripe will be reintroduced only after CRUD and Boost persistence have been exercised reliably against PostgreSQL.

No migration is included and the application never creates or updates the schema at startup. Generate, inspect, and apply migrations yourself after pulling model changes.

## Local setup

Prerequisites: .NET 10 SDK, Node.js 24, and PostgreSQL.

Store the PostgreSQL connection string outside source control:

```powershell
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=crownrank;Username=postgres;Password=YOUR_PASSWORD" `
  --project src/backend/CrownRank.Api
```

Create and apply your migration:

```powershell
dotnet ef migrations add InitialBackendFoundation `
  --project src/backend/CrownRank.Api `
  --startup-project src/backend/CrownRank.Api

dotnet ef database update `
  --project src/backend/CrownRank.Api `
  --startup-project src/backend/CrownRank.Api
```

Run the API and frontend in separate terminals:

```powershell
dotnet run --project src/backend/CrownRank.Api
```

```powershell
cd src/frontend
npm ci
npm run dev
```

The API listens at `http://localhost:5080`; the frontend uses `http://localhost:5173` by default.

## API

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/health` | API process health |
| GET | `/api/creators` | Global ranking |
| GET | `/api/creators/{id}` | Creator details |
| GET | `/api/rankings/daily/{yyyy-MM-dd}` | UTC daily ranking |
| POST | `/api/creators` | Create a creator and mock-confirm its opening Rank Up |
| PUT | `/api/creators/{id}` | Update name, username, category, and social profiles |
| DELETE | `/api/creators/{id}` | Hide a creator while retaining its contribution history |
| POST | `/api/payments/checkout` | Mock-confirm an idempotent creator Boost |

Mutation routes are available only in the Development environment until a production authorization and payment design is completed.

Money is represented by .NET `decimal`, PostgreSQL `numeric(18,2)`, and dollar-based JSON fields. Integer minor units are not used inside the domain or database.

## Checks

```powershell
dotnet test CrownRank.slnx

cd src/frontend
npm test
npm run lint
npm run build
npm run test:e2e
```

The API integration suite uses SQLite in memory and covers multiple creators, ordering, CRUD, entry idempotency, Boost idempotency, invalid input, duplicate usernames, daily ranking, and production route restrictions. PostgreSQL integration and concurrency stress tests are the next backend milestone after this foundation is accepted.
