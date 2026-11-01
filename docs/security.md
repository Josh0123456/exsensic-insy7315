# Security controls

How the system protects client, staff and booking data. Owner: Dean (P3). Related: [CONTRACTS.md](CONTRACTS.md) (roles, error codes, settings), [operations.md](operations.md) (Azure, secrets, access restriction), [migrations.md](migrations.md).

## 1. OWASP Top 10:2025 mapping

| Category | Risk here | Control in this codebase | Evidence |
|---|---|---|---|
| A01 Broken Access Control | Client B reads or changes Client A's booking; a client calls an admin endpoint | `AuthorizationPolicies` on every controller, fallback policy requires sign-in, `BookingAccessHandler` (owner client, assigned staff or admin) returns 404 for anyone else | `OwnershipTests`, `BookingAccessHandlerTests`, `AdminRouteTests` |
| A02 Security Misconfiguration | Pages framed, content sniffed, server advertised, API reachable from the internet | Security headers in both apps (section 5), `AddServerHeader = false`, HSTS and HTTPS redirect outside Development, API access restriction to the Web app's outbound IPs | `SecurityHeaderTests` |
| A03 Software Supply Chain Failures | A vulnerable NuGet package or Action ships | NuGet audit on restore, `dotnet list package --vulnerable` in `ci.yml`, Dependabot weekly for NuGet and Actions (`.github/dependabot.yml`) | CI run |
| A04 Cryptographic Failures | Passwords or tokens readable | Identity PBKDF2 password hashes, HTTPS only in Azure, JWT signed with a ≥ 32-character key held outside Git, Web cookies `Secure` + `HttpOnly` | Identity defaults, config |
| A05 Injection | Crafted input changes a query or a page | EF Core parameterised queries only (no raw SQL), requirement keys checked against the category template, Razor output encoding | `InputValidationTests` |
| A06 Insecure Design | Double booking under load; admin locks themselves out | Filtered unique index `UX_Bookings_ActiveSlot`, `RowVersion` optimistic concurrency, State pattern decides every transition, admin can't deactivate self | `ConcurrentBookingTests`, `AdminSelfLockoutTests` |
| A07 Identification and Authentication Failures | Password guessing, a stolen token after the account is disabled | Lockout after 5 failures for 15 minutes, login rate limit 5/min per IP, one generic login error, security stamp checked on every request | `AuthApiTests`, `RateLimitTests`, `SecurityStampTests` |
| A08 Software and Data Integrity Failures | Someone rewrites history or merges unreviewed code | Protected `develop`/`main`, merge commits only, CI and `pr-checks.yml` required, CODEOWNERS review | Branch protection, PR history |
| A09 Security Logging and Alerting Failures | Nobody notices a break-in | `ILogger<T>` to Application Insights, failed sign-ins logged by user id (no email or password) | Application Insights |
| A10 Mishandling of Exceptional Conditions | A 500 leaks a connection string or stack trace | `ProblemDetailsExceptionHandler` returns a generic 500 with a `traceId`; details only in logs | `ProblemDetailsExceptionHandlerTests` |

## 2. Authentication and authorisation

**Identity.** ASP.NET Core Identity stores users in the same database (`ExsensicDbContext` inherits `IdentityDbContext`). Password policy: at least 10 characters with upper case, lower case and a digit (`AuthenticationExtensions.cs`). Emails are unique.

**JWT.** `POST /api/v1/auth/login` checks the password with lockout counting (`AccountService.LoginAsync`) and returns a JWT from `TokenService`. The token carries the user id, role and the user's **security stamp**, is valid for `Jwt:LifetimeMinutes` (60), and is validated for issuer, audience, signature and lifetime with one minute of clock skew.

**Security stamp.** `RejectStaleTokensAsync` (`AuthenticationExtensions.cs`) runs on every request and rejects a token whose user no longer exists, is deactivated, or whose stamp has changed. `PUT /admin/users/{id}/active` changes the stamp, so a disabled user's live token stops working on the next request rather than at expiry. Reactivating does not revive the old token.

**Web session.** The Web app keeps the JWT inside its encrypted auth cookie (`__Host-Exsensic.Auth`: `HttpOnly`, `Secure`, `SameSite=Lax`, 30 minutes, not sliding), never in browser storage. `BearerTokenHandler` attaches it to API calls. There is no refresh token: after 30 minutes the user signs in again (see section 9).

**Policies.** `AuthorizationPolicies`: `ClientOnly`, `StaffOnly`, `AdminOnly`, `StaffOrAdmin`, and the resource policy `BookingAccess`. The fallback policy requires an authenticated user, so a new endpoint is private unless it is explicitly marked anonymous. Roles are checked by the API; the Web hides buttons only for convenience.

**Least privilege.**
- Registration only ever creates **Client** accounts. Staff accounts are created by an admin; the only admin comes from the seeder (`Seed:AdminEmail`).
- An admin can't deactivate their own account (`AdminUsersController.SetActive`, 400), so the system can't be left without an admin.
- Staff see only bookings assigned to them. Clients see only their own.
- The deploy pipeline uses a managed identity with Website Contributor on the resource group only (operations.md).

## 3. Object-level authorisation: 404, not 403

`BookingAccessHandler` allows the booking's client, its assigned staff member, or an admin. Everyone else gets **404 `not_found`**, the same response as for a booking that doesn't exist.

Why 404: a 403 would confirm that booking `42` exists and belongs to someone else, which lets an attacker enumerate references and learn how busy the business is. With 404 a stranger can't tell "not yours" from "not there". Role checks on whole endpoints (for example a client calling `/admin/users`) still return 403, because the endpoint's existence is public in the contract.

The check runs before any change, so a refused reschedule or cancel leaves the booking untouched (`OwnershipTests`).

## 4. Rate limiting

`RateLimitPolicies.cs`, fixed window, partitioned per visitor:

| Policy | Limit | Key | Why |
|---|---|---|---|
| `login` | 5 per minute | client IP | Slows password guessing across many accounts; lockout covers guessing one account |
| `register` | 3 per minute | client IP | Stops a script mass-creating accounts |
| `booking-create` | 10 per 10 minutes | user id (IP if anonymous) | Stops one client filling the calendar |

A rejected request is **429 `rate_limited`** with a `Retry-After` header. The Web app forwards the visitor's IP in `X-Exsensic-Client-IP`, so the limit applies per person, not per Web server (all traffic reaches the API from the Web app's few outbound IPs).

## 5. Security headers

Set on every response, including errors, in `AddSecurityHeaders` (`Hosting/HostingExtensions.cs` in each app).

| Header | API | Web | Why |
|---|---|---|---|
| `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'` | `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'` | The API only returns JSON, so nothing may load. The Web loads all CSS and JS from `wwwroot` with no inline code and no CDN, so an injected `<script>` can't run. `data:` images are needed because the local Bootstrap CSS draws its form icons as `data:` SVGs. `form-action 'self'` stops a form being pointed at another site. |
| `X-Content-Type-Options` | `nosniff` | `nosniff` | The browser must trust the declared content type, so a JSON or text response is never run as script. |
| `X-Frame-Options` | `DENY` | `DENY` | Clickjacking: no page can be framed (older browsers that ignore `frame-ancestors`). |
| `Referrer-Policy` | `no-referrer` | `strict-origin-when-cross-origin` | Booking URLs contain ids; other sites get at most the origin. |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | same | The app needs none of these, so a compromised page can't ask for them. |
| `Cache-Control` | `no-store` | (default) | API responses hold personal data and must not sit in shared caches. |
| `Strict-Transport-Security` | `UseHsts()` outside Development | same | Browsers refuse plain HTTP after the first visit. |

The Web app does not need `connect-src` for the API: the browser never calls the API directly, the Web server does.

## 6. Data protection and POPIA

The Protection of Personal Information Act (POPIA) applies because the system stores information about identifiable people.

**What is stored and why**

| Data | Where | Purpose (POPIA condition 3, purpose specification) |
|---|---|---|
| Full name, email, password hash | `AspNetUsers` | Sign-in and identifying who made a booking |
| Company name, phone | `ClientProfiles` | Contacting the client about their booking |
| Job title, services they deliver | `StaffProfiles`, `StaffServices` | Assigning qualified staff |
| Booking requirements (free text such as a description) | `BookingRequirements` | Delivering the service the client asked for |
| Status history, notifications | `BookingStatusHistory`, `Notifications` | Audit trail and in-app updates |

No ID numbers, banking details, health or other special personal information are collected (minimality, condition 4).

**Who can see what**

| Role | Can see |
|---|---|
| Client | Their own profile and their own bookings |
| Staff | Bookings assigned to them, including that client's name, email and phone, which they need to prepare and get in touch |
| Admin | All bookings and the user list (name, email, role, active) |

**Safeguards (condition 7).** Encrypted connections everywhere, Azure SQL encryption at rest, the API not reachable from the internet, object-level checks (section 3), and no personal data in logs (`ILogger<T>` logs user ids, never emails, passwords, tokens or requirement text).

**Retention.** Deactivating an account stops all access but keeps the record, because bookings and their history are business records. There is no automated deletion or anonymisation yet; see section 9.

## 7. Threat model (top five)

| # | Threat | Likely path | Mitigation | Residual risk |
|---|---|---|---|---|
| 1 | Another client's booking is read or changed | Changing the id in a URL or API call | `BookingAccess` policy, 404 for non-owners | Low |
| 2 | Account takeover by password guessing | Repeated `/auth/login` attempts | Lockout after 5, rate limit 5/min per IP, password policy | Medium without MFA |
| 3 | A disabled staff member keeps access | Reusing a token issued before deactivation | Security stamp checked on every request | Low |
| 4 | Two clients book the same slot | Simultaneous requests | Filtered unique index rejects the second insert, 409 `slot_unavailable` | Low |
| 5 | Secrets leak through Git | Committing a connection string or key | user-secrets locally, App Service settings in Azure, gitleaks in CI, GitHub push protection | Low |

## 8. Secrets

| Secret | Local | Azure |
|---|---|---|
| `ConnectionStrings:Default` | `dotnet user-secrets` | App Service setting on each API app |
| `Jwt:SigningKey` | `dotnet user-secrets` | App Service setting |
| `Seed:AdminEmail`, `Seed:AdminPassword`, `Seed:DemoPassword` | `dotnet user-secrets` | App Service settings |

App Service settings are encrypted at rest and readable only by people with access to the resource group; operations.md explains why this replaced Key Vault (CONTRACTS.md §9 still says Key Vault and should be brought in line). Deployments use OpenID Connect with a managed identity, so no Azure password is stored in GitHub.

Never committed: `.env`, `appsettings.*.json` with real values, publish profiles, connection strings, signing keys. `gitleaks` runs in CI on every push and pull request, and GitHub secret scanning push protection is on. Tests generate a random signing key per run.

## 9. Known gaps

- **No MFA.** Needs an email or SMS provider. Lockout, rate limits and the password policy reduce, but don't remove, the risk.
- **No refresh tokens.** Users sign in again after 30 minutes. Simpler and safer than storing refresh tokens, but less convenient.
- **No WAF.** Azure Front Door's WAF needs a higher tier. The API accepts traffic only from the Web app's outbound IPs.
- **No automated retention or deletion.** POPIA requires records to be kept no longer than needed. There is no self-service account deletion or scheduled anonymisation of old bookings; an admin can only deactivate an account.
- **No admin audit log.** Booking status changes are recorded in `BookingStatusHistory`, but admin catalogue and user changes (service edits, slot blocks, deactivations) are not recorded with who made them.
- **No role change endpoint.** A user's role can't be changed after creation (it isn't in CONTRACTS.md). A staff member who becomes an admin needs a new account.
- **Antiforgery is not covered by an automated test.** The Web app validates antiforgery tokens on every unsafe request (`AutoValidateAntiforgeryTokenAttribute` in `Program.cs`, `__Host-` cookie, `SameSite=Strict`), but the integration tests host only the API. Evidence is manual.
- **Email delivery.** In-app notifications only; the Observer pattern makes email one new observer later.
