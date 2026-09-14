# CrownRank mock console

From the repository root with .NET 10 installed:

```sh
dotnet run --project src/backend/CrownRank.ConsoleApp -- --database crownrank-console.db
```

The console creates or migrates the SQLite database on startup and stores resized images and mock-payment state in `assets/` next to it. It is a local test UI: mock payment choices cause no charge, admin password is `console-admin`, and the ranking currency is EUR in whole cents. Never deploy this executable or its credentials as a public service.

Start with **Admin → Add category**. Then submit a profile with one or more HTTPS social links and a real PNG, JPEG or WebP image, explicitly accept legal versions, review the amount, and select a mock outcome. A processing attempt can be revisited via **Payment status/complete** using its displayed ID, even after restarting the console. Successful confirmation publishes the entry and contributes to the ranking once. Boost a published entry by ID as its owner or a fan, then inspect global, category, daily and historical UTC leaderboards. Admin can inspect payment attempts and entries, update name, category, links or image, hide/restore/archive an entry, and create/edit/archive categories. Archive is the reversible-data-preserving replacement for destructive deletion.

Use the menus to exercise rejection cases: wrong admin password, archived category, invalid social URL, missing agreement, invalid image, failed or cancelled mock checkout, repeated payment status check, and hidden/archived entry visibility. The console displays action errors and saved/confirmed status; the integration tests cover the principal persistent flows.

This validates mock application behavior, not Stripe checkout, webhook authentication, production concurrency, or a public API/frontend. Replacing the gateway is only one part of enabling real payments: provider notifications, recovery, configuration, and security checks remain separate work.
