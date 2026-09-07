# CrownRank

CrownRank is a fan-powered leaderboard for content creators. A creator can enter with a username, at least one social profile, an optional photo, and a contribution amount of their choice—without creating an account. Anyone can later contribute another payment to boost that creator's position.

> The frontend currently uses realistic dummy data and a simulated checkout adapter. Persistence, uploads, Stripe Checkout, webhook processing, and live ranking APIs are planned for the backend milestones.

## Technology

- React, TypeScript, and Vite
- ASP.NET Core on .NET 10
- Entity Framework Core with PostgreSQL
- Stripe (integration planned; configuration placeholders included)
- Docker Compose for local PostgreSQL

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

The backend follows Clean Architecture: dependencies point inward toward `Domain`; `Api` wires implementations together. The frontend is organized by business feature, with separate `domain`, `application`, `data`, and `ui` layers. Repository and payment gateway interfaces keep dummy implementations replaceable by HTTP and Stripe adapters.

The public experience intentionally has no login, registration, or account-setup pages. The creator-entry dialog validates a username, one or more HTTPS social links, an optional image, and a user-defined contribution of at least $1.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://www.docker.com/) with Docker Compose

## Run locally

1. Start PostgreSQL:

   ```bash
   docker compose up -d postgres
   ```

2. Restore and run the API:

   ```bash
   dotnet restore CrownRank.slnx
   dotnet run --project src/backend/CrownRank.Api --urls http://localhost:5080
   ```

3. In another terminal, install and run the frontend:

   ```bash
   cd src/frontend
   npm install
   npm run dev
   ```

4. Open `http://localhost:5173`. The API health endpoint is `http://localhost:5080/api/health`.

## Configuration

Local non-secret defaults live in `src/backend/CrownRank.Api/appsettings.json`. Override sensitive values with environment variables or .NET user secrets; never commit live credentials.

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

1. Creator aggregate, social-profile validation, ranking rules, and initial EF migration
2. Public leaderboard API and responsive homepage integration
3. Multi-step creator submission form with image upload
4. Stripe Checkout for user-defined entry contributions and arbitrary boosts
5. Idempotent Stripe webhooks that activate entries and apply boosts only after confirmed payment
6. Creator detail/boost pages, moderation tools, observability, and deployment

## Important implementation rules

- Never trust a payment-success redirect; only a verified Stripe webhook may credit a payment.
- Store money as integer minor units and keep an immutable payment/contribution ledger.
- Make webhook processing idempotent by enforcing uniqueness on the Stripe event and payment identifiers.
- Validate and normalize social URLs server-side. Require at least one supported social profile.
- Scan and constrain uploaded files, generate server-owned filenames, and use object storage outside local development.
- Because adult creators may participate, hosting, payment processing, content moderation, age/consent controls, and jurisdiction-specific compliance must be reviewed before launch. Stripe eligibility must be confirmed for the exact business model.

## License

No license has been selected yet. All rights are reserved unless the repository owner adds one.
