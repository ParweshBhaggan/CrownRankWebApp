# CrownRank frontend

The frontend keeps API routes, HTTP requests, backend models, and UI behavior in separate files.

| Location | Responsibility |
| --- | --- |
| `src/shared/api/endpoints/*Endpoints.ts` | One file per backend entity. Named route constants and functions for IDs, names, and dates. Includes all routes currently exposed by those controllers. |
| `src/shared/api/models/` | Request and response models used by the frontend. |
| `src/shared/api/apiRequest.ts` | Reusable `apiGet`, `apiPost`, `apiPut`, and `apiDelete` functions. |
| `src/shared/api/httpClient.ts` | Base URL, fetch, JSON response parsing, API errors, and asset URLs. |
| `src/shared/api/services/` | Typed calls for the entity operations used by the frontend. |
| `src/shared/api/redirect.ts` | Explicit navigation to an HTTP or HTTPS URL. |
| `src/features/*/data/` | Prepare entry submissions, map entries into leaderboard creators, and confirm score boosts. |
| `src/features/*/application/` | Validation, ranking helpers, and React hooks. |
| `src/features/*/ui/` | Rendering and user interactions. |

An entry submission flows from the dialog through `createEntry`, then `postEntry`, `apiPost`, and `httpClient`. Leaderboards use `getEntries` or `getDailyEntries` and map the backend response into UI models. Lookup loading uses the category and social media default services directly.

Endpoint examples:

```ts
export const create_entry_endpoint = '/api/Entry'

export function boost_entry_endpoint(id: string): string
{
  return `/api/Entry/${encodeURIComponent(id)}/boost`
}
```

A GET returns the model specified by its type parameter; POST and PUT serialize their body as JSON. DELETE supports a 204 response. Request options accept headers, credentials, and an abort signal.

```ts
const categories = await apiGet<readonly ApiCategory[]>(get_all_category_endpoint)
const entry = await apiPost<ApiEntry>(create_entry_endpoint, request)
```

For a future endpoint returning a **JSON string URL**, read the URL first and redirect explicitly:

```ts
const url = await apiGet<string>(checkout_endpoint)
redirectToUrl(url)
```

If it returns `{ url: string }`, read that response model and pass `response.url` instead. Checkout endpoints return a Stripe URL; the payment feature redirects explicitly and verifies payment on the return page. See ../../docs/stripe-payments.md.

## Formatting and checks

```sh
npm ci
npm run format
npm run format:check
npm test
npm run lint
npm run build
npx playwright install chromium
npm run test:e2e
```

The formatter expands JSX and compound statements, uses parenthesized arrow parameters, and places function and class opening braces on the next line. Simple expressions stay compact; complex JSX returns use `(parameters) => (...)`. Named props interfaces keep component parameter lists readable. Use `npm run format` to preserve this style; direct Prettier formatting alone uses a different brace style.

