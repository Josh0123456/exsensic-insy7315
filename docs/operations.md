# Operations runbook — Exsensic

How Exsensic is hosted, deployed, monitored and rolled back. Owner: Josh (P4).
Names only; **no secret values belong in this file** (docs/CONTRACTS.md §9).

## 1. What runs where

All resources live in resource group `rg-exsensic-insy7315` in **UAE North**, on one
**B1 Linux App Service plan**, `asp-exsensic` (docs/CONTRACTS.md §12, decision 12). The Azure for
Students subscription only allows a few regions; UAE North is the closest allowed one to
South Africa (South Africa North is blocked by the subscription's region policy).

| Environment | Web (MVC front end) | API | Database |
|---|---|---|---|
| Staging | https://app-exsensic-web-staging.azurewebsites.net | https://app-exsensic-api-staging.azurewebsites.net | `sqldb-exsensic-staging` on `sql-exsensic` |
| Production | https://app-exsensic-web.azurewebsites.net | https://app-exsensic-api.azurewebsites.net | `sqldb-exsensic-prod` on `sql-exsensic` |

Both databases use the **Azure SQL free offer** (serverless, auto-pause, US$0) and share one SQL
server admin login, which is stored only in each API's connection-string setting.
Deploy identity: `id-exsensic-deploy` (user-assigned managed identity, **Website Contributor** on
the resource group only, signed in from GitHub with OIDC, no stored password).

Every app: HTTPS only, minimum TLS 1.2, FTP and password-based publishing disabled, Always On,
App Service health check on `/health`.

Health endpoints on every app (anonymous, outside `/api/v1`):
`/health` (the process is up) and `/health/ready` (the API can reach its database).

## 2. Where each setting lives

| Setting | Used by | Value lives in |
|---|---|---|
| `ConnectionStrings__Default` | API | App Service setting (encrypted at rest; random password, never shown) |
| `Jwt__SigningKey` | API | App Service setting (random, 64 characters, different per environment) |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes` | API | `appsettings.json` (not secret) |
| `Seed__AdminEmail`, `Seed__AdminPassword`, `Seed__DemoPassword` | API | App Service setting, typed in the portal by Josh |
| `BookingPolicy__ClientCancelCutoffHours` | API | `appsettings.json` (default 24) |
| `Database__MigrateOnStartup` | API | App Service setting (`true` in Azure only) |
| `Database__SeedDemoData` | API | App Service setting: `true` on production so the live demo has services, slots and demo accounts (staging seeds by default) |
| `Api__BaseUrl` | Web | App Service setting |
| `ASPNETCORE_ENVIRONMENT` | Both | App Service setting (`Staging` / `Production`) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Both | Set by App Service when monitoring is enabled |

Locally, the same keys come from `dotnet user-secrets` (README → Run locally).

**Why App Service settings and not Key Vault:** the settings are encrypted at rest, never in Git,
and only readable by people with access to the resource group. Key Vault would add per-app
identities and role assignments for little extra protection at this size; it is the first
upgrade if the system grows.

GitHub (Settings → Secrets and variables → Actions):

| Name | Kind | Purpose |
|---|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | Secret | OIDC sign-in as `id-exsensic-deploy` |
| `E2E_CLIENT_EMAIL` / `_PASSWORD`, `E2E_STAFF_*`, `E2E_ADMIN_*` | Secret | Demo accounts for the end-to-end tests |
| `STAGING_DEPLOY_ENABLED` | Variable | `true` switches on staging deployment |
| `E2E_ENABLED` | Variable | `true` runs the end-to-end tests after each staging deploy |
| `PRODUCTION_DEPLOY_ENABLED` | Variable | `true` switches on production deployment |
| `PRODUCTION_APPROVERS` | Variable | GitHub usernames allowed to approve a production deploy |
| `RESOURCE_GROUP` | Variable (optional) | Overrides the default `rg-exsensic-insy7315` |

OIDC trust: the deploy identity has two federated credentials, `github-develop` and `github-main`.
GitHub now sends ID-based subjects, so each subject has the form
`repo:Josh0123456@166015569/exsensic-insy7315@1394624805:ref:refs/heads/<branch>`; a credential in the
older `repo:owner/name:ref:…` form fails with AADSTS700213.

## 3. How a change reaches staging and production

1. A feature branch (`feat/pN-…`) is opened as a pull request into `develop`.
   **`ci`** builds, runs unit and integration tests, checks for vulnerable packages and
   scans for secrets. Another team member reviews and merges with a merge commit.
2. The merge to `develop` triggers **`cd-staging`**: unit tests → publish → deploy the API,
   then the web app → smoke test `/health`, `/health/ready` and `/` → Playwright end-to-end
   tests (`e2e` job). The API applies pending EF Core migrations on start-up. For the smoke test
   the workflow briefly adds the runner's IP to the API's access restriction and always removes
   it afterwards (section 6).
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
   with a new migration (Dean). Azure SQL keeps point-in-time backups for 7 days, so a
   database can be restored to a moment before a bad release (Azure portal → the database →
   **Restore**, which creates a copy to point the API at).

Afterwards, check `/health/ready` on both apps and the smoke-test step in the run.

## 5. Reading logs and failures

- **A pipeline failed:** Actions → the red run → the red step. For `e2e`, download the
  `e2e-traces` artifact and open a `.zip` at https://trace.playwright.dev to replay what the
  browser saw.
- **The app returns errors:** App Service → **Log stream** shows live logs. Every API error
  response carries a `traceId` (docs/CONTRACTS.md §7) to search for. Application Insights is wired
  in the code and switches on when App Service monitoring is enabled
  (`APPLICATIONINSIGHTS_CONNECTION_STRING`); then use **Failures** and **Logs** there.
- **The app will not start:** App Service → **Log stream**, or **Diagnose and solve problems**.
- Logs never contain passwords, tokens, emails or requirement text (docs/CONTRACTS.md §10).

## 6. Cost and access

- **Cost:** only the B1 plan is paid, about US$13/month for all four apps; both databases use
  the free offer; the deploy identity and settings are free. Budget alert: set by the subscription
  owner on the resource group (US$20/month, email at 80%).
- **Azure access:** Owner on the resource group is granted through Privileged Identity
  Management and must be **activated** (Azure portal → My roles → Activate) before each session.
- **API access restriction:** both API apps accept traffic only from the web apps' possible
  outbound IP addresses (App Service → Networking → Access restriction, rules `web-outbound-*`,
  everything else denied). The deployment (SCM) site keeps its own, unrestricted rules so GitHub
  Actions can still deploy, and the smoke test adds a temporary `github-smoke-test` rule for the
  runner's IP. This replaces the VNet and private endpoint in the Part 1 design (decision 12),
  which need a more expensive tier. Consequence: nobody can call the deployed API directly from
  their own computer; test API changes locally and test the website on staging.

## 7. Known limitations

- **B1 tier:** one small machine shared by four apps; slower under load.
- **No deployment slots** (they need Standard tier), so a deploy briefly restarts the app
  instead of swapping in a warmed-up copy.
- **Cold starts:** the free-offer database pauses when idle and takes up to about a minute to
  resume, so the first request after a quiet period can time out. **Open the site a minute or
  two before a demo** to wake it. Keeping it awake permanently would use up the free allowance.
- **Shared outbound IPs:** on the Basic tier the web apps' outbound IPs are shared with other
  Azure customers on the same scale unit, so the access restriction keeps the API off the open
  internet but is not a private network. Staging's web app could also reach the production API.
- **No GitHub branch protection** on the free private repository: the review-before-merge rules
  are kept by agreement (CONTRIBUTING.md) and are visible on every pull request.
- **End-to-end tests create real bookings** on staging; the seeded slots (30 weekdays) cover the
  project's lifetime.
