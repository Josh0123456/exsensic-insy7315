# Operations runbook — Exsensic

How Exsensic is hosted, deployed, monitored and rolled back. Owner: Josh (P4).
Names only; **no secret values belong in this file** (docs/CONTRACTS.md §9).

> Draft. Lines marked **TODO** are filled in once the Azure resources exist.

## 1. What runs where

All resources live in resource group `rg-exsensic-insy7315` (South Africa North) on one
**B1 Linux App Service plan**, `asp-exsensic` (docs/CONTRACTS.md §12, decision 12).

| Environment | Web (MVC front end) | API | Database |
|---|---|---|---|
| Staging | `app-exsensic-web-staging` · TODO URL | `app-exsensic-api-staging` · TODO URL | `sqldb-exsensic-staging` on `sql-exsensic` |
| Production | `app-exsensic-web` · TODO URL | `app-exsensic-api` · TODO URL | `sqldb-exsensic-prod` on `sql-exsensic` |

Supporting resources: Key Vault `kv-exsensic`, Application Insights `appi-exsensic-staging` /
`appi-exsensic-prod`, deploy identity `id-exsensic-deploy` (GitHub OIDC, no stored password).

Health endpoints on every app (anonymous, outside `/api/v1`):
`/health` (the process is up) and `/health/ready` (the API can reach its database).

## 2. Where each setting lives

| Setting | Used by | Value lives in |
|---|---|---|
| `ConnectionStrings__Default` | API | Key Vault, referenced from App Service settings |
| `Jwt__SigningKey` | API | Key Vault |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes` | API | `appsettings.json` (not secret) |
| `Seed__AdminEmail`, `Seed__AdminPassword`, `Seed__DemoPassword` | API | Key Vault |
| `BookingPolicy__ClientCancelCutoffHours` | API | `appsettings.json` (default 24) |
| `Database__MigrateOnStartup` | API | App Service setting (`true` in Azure only) |
| `Api__BaseUrl` | Web | App Service setting |
| `ASPNETCORE_ENVIRONMENT` | Both | App Service setting (`Staging` / `Production`) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Both | Set by App Service when monitoring is enabled |

Locally, the same keys come from `dotnet user-secrets` (README → Run locally).

GitHub (Settings → Secrets and variables → Actions):

| Name | Kind | Purpose |
|---|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | Secret | OIDC sign-in as `id-exsensic-deploy` |
| `E2E_CLIENT_EMAIL` / `_PASSWORD`, `E2E_STAFF_*`, `E2E_ADMIN_*` | Secret | Demo accounts for the end-to-end tests |
| `STAGING_DEPLOY_ENABLED` | Variable | `true` switches on staging deployment |
| `E2E_ENABLED` | Variable | `true` runs the end-to-end tests after each staging deploy |
| `PRODUCTION_DEPLOY_ENABLED` | Variable | `true` switches on production deployment |
| `PRODUCTION_APPROVERS` | Variable | GitHub usernames allowed to approve a production deploy |

## 3. How a change reaches staging and production

1. A feature branch (`feat/pN-…`) is opened as a pull request into `develop`.
   **`ci`** builds, runs unit and integration tests, checks for vulnerable packages and
   scans for secrets. Another team member reviews and merges with a merge commit.
2. The merge to `develop` triggers **`cd-staging`**: unit tests → publish → deploy the API,
   then the web app → smoke test `/health`, `/health/ready` and `/` → Playwright end-to-end
   tests (`e2e` job). The API applies pending EF Core migrations on start-up.
3. For a release, Josh opens `release/vX.Y.Z` → `main`. The merge triggers **`cd-production`**:
   the same tests and publish, then the run **pauses and opens a GitHub issue**. A team member
   in `PRODUCTION_APPROVERS` (not the person who started it) comments `approve`; only then does
   it deploy to production and smoke-test it. `deny`, or no answer within 60 minutes, stops it.

Why an issue and not GitHub's built-in environment reviewers: those need a paid plan on a
private repository. The issue gate gives the same "a second person approves" control.

## 4. Rolling back

Pick the fastest that fits:

1. **Re-run the last good deployment.** Actions → `cd-production` (or `cd-staging`) → open the
   last green run → **Re-run all jobs**. A re-run deploys that run's original commit.
   Artifacts are kept for 14 days.
2. **Revert the change.** Revert the bad merge on `develop` in a pull request, then release
   again. Use this when the bad commit must not be redeployed later.
3. **Database:** migrations only move forward. Never delete a merged migration; fix forward
   with a new migration (Dean). Before risky releases, note the Azure SQL point-in-time
   restore window (TODO: retention days).

Afterwards, check `/health/ready` on both apps and the smoke-test step in the run.

## 5. Reading logs and failures

- **A pipeline failed:** Actions → the red run → the red step. For `e2e`, download the
  `e2e-traces` artifact and open a `.zip` at https://trace.playwright.dev to replay what the
  browser saw.
- **The app returns errors:** Application Insights (`appi-exsensic-…`) → **Failures** for failed
  requests and exceptions, **Logs** for queries. Every API error response carries a `traceId`
  (docs/CONTRACTS.md §7) that matches the request in Application Insights.
- **The app will not start:** App Service → **Log stream**, or **Diagnose and solve problems**.
- Logs never contain passwords, tokens, emails or requirement text (docs/CONTRACTS.md §10).

## 6. Cost and access

- **Budget alert:** Cost Management budget on `rg-exsensic-insy7315`, TODO amount, email at 80%.
  Expected cost is about US$15/month (B1 plan; Azure SQL free offer).
- **Azure access:** Owner on the resource group is granted through Privileged Identity
  Management and must be **activated** (Azure portal → My roles → Activate) before each session.
- **API access restriction:** the API apps accept traffic only from their web app's outbound
  IP addresses (App Service → Networking → Access restriction). The deployment (SCM) site keeps
  its own rules so GitHub Actions can still deploy. This replaces the VNet and private endpoint
  in the Part 1 design (decision 12), which need a higher, more expensive tier.

## 7. Known limitations

- **B1 tier:** one small machine shared by four apps; slower under load.
- **No deployment slots** (they need Standard tier), so a deploy briefly restarts the app
  instead of swapping in a warmed-up copy.
- **Cold starts:** the first request after a restart, or after an idle Azure SQL free-offer
  database auto-pauses, can take several seconds.
- **No GitHub branch protection** on the free private repository: the review-before-merge rules
  are kept by agreement (CONTRIBUTING.md) and are visible on every pull request.
- **End-to-end tests create real bookings** on staging; the seeded slots (30 weekdays) cover the
  project's lifetime.
