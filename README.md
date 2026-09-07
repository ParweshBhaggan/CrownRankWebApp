# CrownRank

CrownRank is a fan-powered leaderboard for content creators. A creator can enter with a username, at least one social profile, an optional photo, and a contribution amount of their choice—without creating an account. Anyone can later contribute another payment to boost that creator's position.

The leaderboard now reads its development seed data from the ASP.NET Core API. PostgreSQL persists creators, social profiles, and an immutable contribution ledger. Checkout remains intentionally simulated behind a payment interface until Stripe is implemented.

## Technology

- React, TypeScript, and Vite
- ASP.NET Core on .NET 10
- Entity Framework Core with PostgreSQL
- Swagger UI and OpenAPI
- ImageSharp for safe, normalized local profile images
- Stripe-ready payment contracts with a local development adapter

## Repository layout

```text
src/
├── backend/
│   ├── CrownRank.Api/             # HTTP endpoints and composition root
│   ├── CrownRank.Application/     # Use cases and application contracts
│   ├── CrownRank.Domain/          # Business model and domain rules
│   └── CrownRank.Infrastructure/  # EF Core and external integrations
└── frontend/
    └── src/
        ├── app/                    # Application composition
        ├── pages/                  # Route-level UI
        ├── shared/                 # Cross-feature infrastructure
        └── styles/                 # Global design tokens and styles
tests/
└── CrownRank.ArchitectureTests/   # Dependency-boundary tests
```

The backend follows Clean Architecture: dependencies point inward toward `Domain`; `Application` owns use cases and ports; `Infrastructure` supplies EF Core, image, and payment adapters; `Api` is the composition root. PostgreSQL is referenced only by Infrastructure registration, so another relational provider can replace it without changing the domain or application layers. The frontend's leaderboard repository now uses the HTTP API.

The public experience intentionally has no login, registration, or account-setup pages. The creator-entry dialog validates a username, one or more HTTPS social links, an optional image, and a user-defined contribution of at least $1.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- PostgreSQL 16+ installed locally

## Run locally

1. Create a local PostgreSQL role and database (or use equivalent values of your choice):

   ```sql
   CREATE ROLE crownrank WITH LOGIN PASSWORD 'crownrank_dev';
   CREATE DATABASE crownrank OWNER crownrank;
   ```

2. If your local connection differs, set it with user secrets, then restore and run the API:

   ```bash
   dotnet user-secrets init --project src/backend/CrownRank.Api
   dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=crownrank;Username=crownrank;Password=crownrank_dev" --project src/backend/CrownRank.Api
   dotnet restore CrownRank.slnx
   dotnet run --project src/backend/CrownRank.Api
   ```

   Development startup creates the schema when needed and inserts 50 creators only when the database is empty.

3. In another terminal, install and run the frontend:

   ```bash
   cd src/frontend
   npm install
   npm run dev
   ```

4. Open `http://localhost:5173`. API health is available at `http://localhost:5080/api/health`; Swagger UI is at `http://localhost:5080/swagger`.

## API surface

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/health` | Local health check |
| `GET` | `/api/creators` | Creators with global and current UTC-day totals |
| `GET` | `/api/creators/{id}` | Creator details |
| `GET` | `/api/rankings/daily/{date}` | Backend-calculated ranking for a UTC calendar date |
| `POST` | `/api/creators` | Multipart creator entry with optional image |
| `DELETE` | `/api/creators/{id}` | Local-only deletion; must receive an admin policy before deployment |
| `POST` | `/api/payments/checkout` | Provider-neutral checkout contract using a development adapter |

`POST /api/creators` accepts `firstName`, `lastName`, `username`, `category`, optional `location`, `initialAmountCents`, optional `image`, and `socialProfilesJson`. Swagger documents and exercises the request.

## Profile images

- Accepted inputs: JPEG, PNG, WebP, GIF, and BMP.
- Maximum encoded upload: 8 MB; maximum decoded image: 25 megapixels.
- The server decodes the content, validates the real format, applies EXIF orientation, removes EXIF/ICC metadata, center-crops to 1024×1024, and writes a quality-82 WebP under `wwwroot/uploads/profiles`.
- Filenames are server-generated and local uploads are ignored by Git.
- `IProfileImageService` keeps the application independent of storage. Replace `LocalProfileImageService` with an S3, Azure Blob, Cloudflare R2, or equivalent adapter for deployment.
- Missing images use a repository-owned default avatar.
- ImageSharp uses the Six Labors Split License; verify that the deployment and company remain eligible or obtain the appropriate commercial license before launch.

## Configuration

Local non-secret defaults live in `src/backend/CrownRank.Api/appsettings.json`. Override database credentials and future payment secrets with environment variables or .NET user secrets; never commit live credentials.

```bash
dotnet user-secrets init --project src/backend/CrownRank.Api
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project src/backend/CrownRank.Api
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..." --project src/backend/CrownRank.Api
```

ASP.NET Core maps nested environment keys with double underscores, for example `ConnectionStrings__Database` and `Stripe__SecretKey`. Copy `src/frontend/.env.example` to `src/frontend/.env.local` to override the browser-facing API URL.

## Quality checks

```bash
dotnet test CrownRank.slnx
cd src/frontend
npm run lint
npm run build
```

Architecture tests protect the inward dependency rule and should grow alongside the solution.

## Product roadmap

1. Replace development schema creation with reviewed provider-specific migrations
2. Connect the entry and boost forms to backend checkout orchestration
3. Add Stripe Checkout plus idempotent, signature-verified webhooks
4. Credit contributions only from verified payment events
5. Add admin authentication and authorization to deletion/moderation endpoints
6. Move images to object storage, add malware/content moderation, observability, and deployment

## Important implementation rules

- Never trust a payment-success redirect; only a verified Stripe webhook may credit a payment.
- Store money as integer minor units and keep an immutable payment/contribution ledger.
- Make webhook processing idempotent by enforcing uniqueness on the Stripe event and payment identifiers.
- Validate and normalize social URLs server-side. Require at least one supported social profile.
- Scan and constrain uploaded files, generate server-owned filenames, and use object storage outside local development.
- The current `DELETE` endpoint is deliberately documented as unsecured local-development functionality. Never deploy it before adding an admin authorization policy.
- Because adult creators may participate, hosting, payment processing, content moderation, age/consent controls, and jurisdiction-specific compliance must be reviewed before launch. Stripe eligibility must be confirmed for the exact business model.

## License

No license has been selected yet. All rights are reserved unless the repository owner adds one.
