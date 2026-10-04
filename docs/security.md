# Security controls

How the system protects client, staff and booking data. Owner: Dean (P3).

## OWASP Top 10:2025 mapping

| Category | Risk | Control | Test |
|---|---|---|---|
| A01 Broken Access Control | Client B reads Client A's booking | `BookingAccessHandler`, 404 for another user | `OwnershipTests` |
| A02 Security Misconfiguration | API leaks its server name | `AddSecurityHeaders`, `Cache-Control: no-store` | `SecurityHeaderTests` |
| A03 Supply Chain Failures | Vulnerable NuGet package ships | CI runs `dotnet list package --vulnerable`, Dependabot opens PRs | CI |
| A04 Cryptographic Failures | Passwords in plain text | Identity PBKDF2; HTTPS outside Development | Identity defaults |
| A05 Injection | Crafted JSON changes a query | EF Core parameterisation; template validation | `InputValidationTests` |
| A06 Insecure Design | Double booking under load | Filtered unique index `UX_Bookings_ActiveSlot` | `ConcurrentBookingTests` |
| A07 Authentication Failures | Password guessing | Rate limit 5 per minute, lockout after 5 | `AuthApiTests` |
| A08 Integrity Failures | Someone rewrites history | Merge-commit-only, CI on every PR, `pr-checks.yml` | CI |
| A09 Logging Failures | Nobody notices a break-in | `ILogger<T>`, Application Insights | Application Insights |
| A10 Mishandling Exceptional Conditions | 500 leaks a connection string | Generic 500, traceId in logs only | `ProblemDetailsExceptionHandlerTests` |

## Auth flow

1. Client signs in at `/Account/Login`. The Web posts to `POST /api/v1/auth/login`.
2. The API validates the password and returns a JWT signed with `Jwt:SigningKey`.
3. The Web stores the JWT in the encrypted cookie ticket, never in localStorage.
4. `BearerTokenHandler` adds `Authorization: Bearer` on every later call.
5. The API validates the token and rejects it if the user is deactivated or the security stamp changed.

## Database integrity rules

| Rule | Why |
|---|---|
| Filtered unique index on active bookings | Two clients pressing Book at once both pass the code check. Only the database stops the second one. |
| `RowVersion` on Service, TimeSlot, Booking | A stale client is refused with 409 instead of silently overwriting. |
| `BookingRequirements (BookingId, FieldKey)` unique | One answer per field per booking. |
| `Services.Name` unique | No two services with the same name. |
| Restrict deletes on bookings and profiles | A user deletion can't cascade into history. |

## Secrets and logging

- Locally: `dotnet user-secrets`. In Azure: App Service settings, encrypted at rest.
- Never committed: `.env`, publish profiles, real connection strings.
- CI runs `gitleaks` on every push and PR.
- `ILogger<T>` only. Never log passwords, tokens, emails or requirement text.

## Out of scope

- **MFA.** Needs an email or SMS provider. Real cost, no rubric line.
- **WAF.** Azure Front Door's WAF needs Standard tier. The API already accepts traffic only from the Web app's IPs.
- **Email delivery.** In-app notifications work without a paid provider. The Observer pattern makes it one new class later.