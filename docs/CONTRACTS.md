# Exsensic — Shared Contract (INSY7315 Task 2)

> **This file is the single source of truth for names.** Every person and every AI agent uses
> exactly these names, values, routes and error codes. Nobody renames anything here on their own.
>
> **To change the contract:** post the proposed change in the group chat, get a yes from the
> owner of the affected area, then the owner edits this file in its own PR
> (`docs(contract): ...`). Agents must never edit this file.

Lives at `docs/CONTRACTS.md` in the repo.

---

## 1. Solution and project references

```
Exsensic.sln
src/Exsensic.Web          ASP.NET Core MVC front end        → Contracts
src/Exsensic.Api          ASP.NET Core Web API              → Core, Data, Contracts
src/Exsensic.Core         Domain + business logic layer     → Contracts
src/Exsensic.Data         EF Core, Identity, migrations     → Core, Contracts
src/Exsensic.Contracts    DTOs, enums, constants (no deps)  → nothing
tests/Exsensic.UnitTests         xUnit, no database         → Core, Contracts
tests/Exsensic.IntegrationTests  xUnit + WebApplicationFactory + SQL Server → Api
tests/Exsensic.E2ETests          Playwright for .NET against a deployed URL
```

Layer rules (checked by an architecture test in CI):

- **Web never references Core, Data or Api.** Web talks to the API over HTTP only.
- **Core never references Data, Api or ASP.NET Core MVC.** Core uses `IAppDbContext` (an interface in Core that Data implements).
- **Contracts references nothing.**

Target framework: **.NET 10**, pinned in `global.json`. Nullable enabled. Warnings as errors.

---

## 2. Roles

`RoleNames` (in `Exsensic.Contracts/Auth/RoleNames.cs`):

| Constant | Value | Who |
|---|---|---|
| `RoleNames.Client` | `"Client"` | Businesses booking Exsensic services. Only role public registration can create |
| `RoleNames.Staff` | `"Staff"` | Exsensic employees assigned to bookings |
| `RoleNames.Admin` | `"Admin"` | Exsensic management. Created by the seeder only |

Authorisation policies (in `Exsensic.Api/Security/AuthorizationPolicies.cs`):
`ClientOnly`, `StaffOnly`, `AdminOnly`, `StaffOrAdmin`, and the resource policy `BookingAccess`
(owner client, assigned staff, or admin). The fallback policy requires an authenticated user, so
anonymous endpoints must say `[AllowAnonymous]` explicitly.

Rate-limit policies (in `Exsensic.Api/Security/RateLimitPolicies.cs`): `login` (5 per minute per IP),
`register` (3 per minute per IP), `booking-create` (10 per 10 minutes per user).

---

## 3. Enums (in `Exsensic.Contracts/Enums/`)

**`BookingStatus`** — stored in the database as a string, max 20 characters:

| Value | Meaning | Final? |
|---|---|---|
| `Requested` | Submitted by the client, waiting for admin approval. Also where a rescheduled booking returns to | No |
| `Confirmed` | Approved by admin, staff assigned | No |
| `Completed` | Service delivered, marked by assigned staff or admin | **Yes** |
| `Cancelled` | Cancelled by client/admin, or rejected by admin (reason stored) | **Yes** |

There is **no** `Pending`, `Rejected` or `Rescheduled` status. A rejection is `Cancelled` with a
`CancellationReason` that starts with `"Rejected: "`. Rescheduling is an action, not a status.

**Legal transitions** (enforced by the State pattern in Core — nowhere else):

| From | Confirm | Reject | Reschedule | Cancel | Complete |
|---|---|---|---|---|---|
| Requested | → Confirmed (Admin) | → Cancelled (Admin, reason required) | stays Requested (owner Client, Admin) | → Cancelled (owner Client, Admin) | ✗ |
| Confirmed | ✗ | ✗ | → Requested (owner Client, Admin) | → Cancelled (Client outside cut-off, Admin anytime) | → Completed (assigned Staff, Admin; only after slot start) |
| Completed | ✗ | ✗ | ✗ | ✗ | ✗ |
| Cancelled | ✗ | ✗ | ✗ | ✗ | ✗ |

✗ = `409` with code `invalid_transition`.

**`ServiceCategory`**: `Photoshoot`, `Website`, `Instagram`, `TikTok`.

**`NotificationType`**: `BookingRequested`, `BookingConfirmed`, `BookingRejected`, `BookingCancelled`,
`BookingRescheduled`, `BookingCompleted`, `StaffAssigned`.

---

## 4. Entities

Owner of entity **properties and EF configuration**: Dean (P3).
Owner of **behaviour methods on `Booking`** (State pattern, events): Daniel (P2).
`Booking` is a `partial class` split over two files so they never edit the same file:
`Core/Entities/Booking.cs` (properties, Dean) and `Core/Bookings/Booking.Behaviour.cs` (methods, Daniel).

`IAppDbContext` (in `Core/Abstractions/`, Dean) exposes a `DbSet<T>` per Core entity plus `SaveChangesAsync`
and `BeginTransactionAsync`. `ExsensicDbContext` in Data implements it.

Database start-up (Dean): `Exsensic.Data.Seed.DatabaseInitialiser.InitialiseAsync(IServiceProvider services,
bool seedDemoData, CancellationToken ct)` applies migrations, ensures roles and the first admin, and adds demo
data when `seedDemoData` is true (Development and Staging only).

| Entity | Project | Key properties |
|---|---|---|
| `ApplicationUser : IdentityUser<Guid>` | Data | `FullName`, `IsActive`, `CreatedAtUtc` |
| `ClientProfile` | Core | `UserId` (PK), `CompanyName`, `Phone` |
| `StaffProfile` | Core | `UserId` (PK), `JobTitle` |
| `Service` | Core | `Id`, `Name`, `Category`, `Description`, `DurationMinutes`, `BasePrice?`, `IsActive`, `RowVersion` |
| `StaffService` | Core | `StaffUserId` + `ServiceId` (composite PK) |
| `TimeSlot` | Core | `Id`, `SlotDate` (DateOnly), `StartTime` / `EndTime` (TimeOnly), `IsBlocked`, `BlockReason?`, `RowVersion` |
| `Booking` | Core | `Id`, `Reference`, `ClientUserId`, `ServiceId`, `TimeSlotId`, `StaffUserId?`, `Status`, `CancellationReason?`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion` |
| `BookingRequirement` | Core | `Id`, `BookingId`, `FieldKey`, `FieldValue` |
| `BookingStatusHistory` | Core | `Id`, `BookingId`, `FromStatus?`, `ToStatus`, `ChangedByUserId`, `ChangedAtUtc`, `Note?` |
| `Notification` | Core | `Id`, `UserId`, `BookingId?`, `Type`, `Message`, `IsRead`, `CreatedAtUtc` |

Core entities refer to users by `Guid` id only — **no navigation property to `ApplicationUser`** in Core.

Database rules that must exist (Dean):
- Filtered unique index `UX_Bookings_ActiveSlot` on `Bookings.TimeSlotId` **WHERE Status IN ('Requested','Confirmed')**
- Unique: `Services.Name`, `TimeSlots (SlotDate, StartTime)`, `Bookings.Reference`, `BookingRequirements (BookingId, FieldKey)`
- Check constraints: status values; `EndTime > StartTime`; `DurationMinutes BETWEEN 15 AND 480`
- Indexes: `Bookings (ClientUserId, Status)`, `Bookings (StaffUserId, Status)`, `Bookings (Status, CreatedAtUtc)`, `TimeSlots (SlotDate, IsBlocked)`, `Notifications (UserId, IsRead)`
- `RowVersion` is a SQL `rowversion` concurrency token on `Service`, `TimeSlot`, `Booking`
- Deleting a `TimeSlot`, `Service` or user that has bookings is **restricted**, never cascaded
- A slot's availability is **derived**, never stored: not blocked, starts in the future, no `Requested`/`Confirmed` booking, and for a given service the slot must be **at least as long as the service's `DurationMinutes`**

Time: slot dates/times are **South African local time (SAST, UTC+2)**. Audit timestamps are **UTC `DateTimeOffset`**, names end in `Utc`. Get "now" from the injected `TimeProvider`, never `DateTime.Now`.

---

## 5. DTOs (in `Exsensic.Contracts/`, all `sealed record`)

`RowVersion` travels as a **base64 string** in every DTO that has one.

| Folder | DTOs | Owner |
|---|---|---|
| `Common` | `PagedResult<T>(Items, Page, PageSize, TotalCount)`, `ErrorCodes` (constants) | Daniel |
| `Auth` | `RegisterRequest(FullName, CompanyName, Email, Phone, Password, ConfirmPassword)`, `LoginRequest(Email, Password)`, `AuthResponse(AccessToken, ExpiresAtUtc, UserId, FullName, Role)`, `ProfileDto`, `UpdateProfileRequest` | Dean |
| `Catalog` | `ServiceDto`, `SaveServiceRequest`, `SetActiveRequest(IsActive, RowVersion)`, `TimeSlotDto`, `AvailableSlotDto(TimeSlotId, SlotDate, StartTime, EndTime)`, `GenerateSlotsRequest(FromDate, ToDate, Weekdays, StartTimes, DurationMinutes)`, `GenerateSlotsResult(Created, SkippedDuplicates)`, `BlockSlotRequest(Reason, RowVersion)` | Dean |
| `Requirements` | `RequirementTemplateDto(Category, Fields)`, `RequirementFieldDto(Key, Label, InputType, Required, MaxLength, Min, Max, Options)` | Daniel |
| `Bookings` | `CreateBookingRequest(ServiceId, TimeSlotId, Requirements)`, `BookingCreatedDto(Id, Reference, Status)`, `BookingSummaryDto`, `BookingDetailDto`, `BookingStatusHistoryDto`, `RescheduleBookingRequest(NewTimeSlotId, RowVersion)`, `CancelBookingRequest(Reason?, RowVersion)` | Daniel |
| `Admin` | `DashboardDto`, `ConfirmBookingRequest(StaffUserId, RowVersion)`, `RejectBookingRequest(Reason, RowVersion)`, `StaffOptionDto(UserId, FullName, JobTitle)` | Daniel |
| `Admin` | `UserSummaryDto`, `CreateStaffRequest`, `SetUserActiveRequest(IsActive)` | Dean |
| `Staff` | `CompleteBookingRequest(RowVersion)` | Daniel |
| `Notifications` | `NotificationDto(Id, Type, Message, BookingId, IsRead, CreatedAtUtc)` | Daniel |

`CreateBookingRequest.Requirements` is `Dictionary<string, string>`; keys must match the
service category's requirement template or the API returns `400 validation_failed`.

---

## 6. API routes (base path `/api/v1`)

| Method and route | Roles | Success | Owner |
|---|---|---|---|
| `POST /auth/register` | Anonymous (rate limited) | 201 `AuthResponse` | Dean |
| `POST /auth/login` | Anonymous (rate limited) | 200 `AuthResponse` | Dean |
| `GET /me` · `PUT /me` | Signed in | 200 `ProfileDto` | Dean |
| `GET /services` · `GET /services/{id}` | Anonymous | 200 | Dean |
| `GET /services/{id}/availability?from=&to=` | Anonymous | 200 `AvailableSlotDto[]` (max 60-day range) | Dean |
| `GET /services/{id}/requirement-template` | Anonymous | 200 `RequirementTemplateDto` | Daniel |
| `GET /admin/services` · `POST /admin/services` · `PUT /admin/services/{id}` · `PUT /admin/services/{id}/active` | Admin | 200 / 201 | Dean |
| `GET /admin/timeslots?from=&to=` · `POST /admin/timeslots/generate` | Admin | 200 / 201 `GenerateSlotsResult` | Dean |
| `PUT /admin/timeslots/{id}/block` · `PUT /admin/timeslots/{id}/unblock` · `DELETE /admin/timeslots/{id}` | Admin | 204 | Dean |
| `GET /admin/users?role=&page=` · `POST /admin/users` · `PUT /admin/users/{id}/active` | Admin | 200 / 201 / 204 | Dean |
| `POST /bookings` | Client (rate limited) | 201 `BookingCreatedDto` + `Location` | Daniel |
| `GET /bookings/mine?status=&page=` | Client | 200 `PagedResult<BookingSummaryDto>` | Daniel |
| `GET /bookings/{id}` | `BookingAccess` | 200 `BookingDetailDto` | Daniel |
| `PUT /bookings/{id}/reschedule` · `PUT /bookings/{id}/cancel` | `BookingAccess` (client owner or Admin) | 200 `BookingDetailDto` | Daniel |
| `GET /admin/dashboard` | Admin | 200 `DashboardDto` | Daniel |
| `GET /admin/bookings?status=&from=&to=&page=` | Admin | 200 `PagedResult<BookingSummaryDto>` | Daniel |
| `GET /admin/staff?serviceId=&timeSlotId=` | Admin | 200 `StaffOptionDto[]` (qualified and free) | Daniel |
| `PUT /admin/bookings/{id}/confirm` · `PUT /admin/bookings/{id}/reject` | Admin | 200 `BookingDetailDto` | Daniel |
| `GET /staff/me/bookings?from=&to=` | Staff | 200 `BookingSummaryDto[]` | Daniel |
| `PUT /staff/bookings/{id}/complete` | Assigned Staff, Admin | 200 `BookingDetailDto` | Daniel |
| `GET /notifications/mine` · `PUT /notifications/{id}/read` | Signed in | 200 / 204 | Daniel |
| `GET /health` · `GET /health/ready` | Anonymous (no `/api/v1` prefix) | 200 / 503 | Josh |

Paging: `page` from 1, `pageSize` default 20, max 100.

---

## 7. Errors

Every error is RFC 9457 **ProblemDetails** with a `code` extension and a `traceId`. Never a stack trace.

| HTTP | `code` (constant in `ErrorCodes`) | When |
|---|---|---|
| 400 | `validation_failed` | Bad input. Body is `ValidationProblemDetails` with per-field errors |
| 401 | `unauthenticated` | No/expired token. **Login failure uses one generic message** for wrong email and wrong password |
| 403 | `forbidden` | Signed in, **wrong role** |
| 404 | `not_found` | Missing — **and also when the booking exists but is not yours** (never reveal it exists) |
| 409 | `slot_unavailable` | Slot taken, blocked or in the past |
| 409 | `invalid_transition` | Action not allowed from the current status |
| 409 | `concurrency_conflict` | `RowVersion` is stale — someone changed it first |
| 409 | `staff_unavailable` | Staff not qualified or already booked at that time |
| 409 | `slot_has_booking` | Admin tried to block/delete a slot with an active booking |
| 409 | `duplicate` | Email or service name already exists |
| 409 | `cancel_window_closed` | Client cancelling inside the cut-off window |
| 429 | `rate_limited` | Too many requests. `Retry-After` header set |
| 500 | `server_error` | Unexpected. Generic message only; details in the logs |

---

## 8. MVC routes (Exsensic.Web)

| URL | Controller | Owner |
|---|---|---|
| `/` · `/Privacy` · `/Error` | `HomeController` | Christopher |
| `/Account/Login` · `/Account/Register` · `/Account/Profile` · `POST /Account/Logout` | `AccountController` | Christopher |
| `/Services` · `/Services/{id}` | `ServicesController` | Christopher |
| `/Book/{serviceId}` (step 1: slot) · `/Book/{serviceId}/Details` (step 2: requirements) | `BookController` | Christopher |
| `/Bookings` · `/Bookings/{id}` · `/Bookings/{id}/Confirmation` · `/Bookings/{id}/Reschedule` · `/Bookings/{id}/Cancel` | `BookingsController` | Christopher |
| `/Notifications` (+ `NotificationBadge` view component in the nav) | `NotificationsController` | Daniel |
| `/Staff` · `/Staff/Bookings/{id}` | `StaffController` | Christopher |
| `/Admin` · `/Admin/Bookings` · `/Admin/Bookings/{id}` | `AdminController` | Christopher |
| `/Admin/Services` · `/Admin/Services/Create` · `/Admin/Services/{id}/Edit` | `AdminServicesController` | Dean |
| `/Admin/TimeSlots` · `/Admin/TimeSlots/Generate` | `AdminTimeSlotsController` | Dean |
| `/Admin/Users` | `AdminUsersController` | Dean |

Typed API clients in `Exsensic.Web/ApiClients/`: `IAuthApi`, `IBookingsApi`, `IAdminBookingsApi`, `IStaffApi`
(Christopher) · `ICatalogApi`, `IAdminCatalogApi`, `IAdminUsersApi` (Dean) · `INotificationsApi` (Daniel).
All share `BearerTokenHandler` and `ApiProblemMapper` (Christopher).

API controllers in `Exsensic.Api/Controllers/`: `AuthController`, `MeController`, `ServicesController`,
`AdminServicesController`, `AdminTimeSlotsController`, `AdminUsersController` (Dean) · `RequirementTemplatesController`,
`BookingsController`, `AdminBookingsController`, `StaffBookingsController`, `NotificationsController` (Daniel).
Health endpoints are mapped in `Program.cs` (Josh).

---

## 9. Configuration keys (names only — values never in Git)

| Key | Used by | Where the value lives |
|---|---|---|
| `ConnectionStrings:Default` | Api | user-secrets locally, Key Vault in Azure |
| `Jwt:Issuer` · `Jwt:Audience` · `Jwt:LifetimeMinutes` | Api | appsettings (not secret) |
| `Jwt:SigningKey` (≥ 32 chars) | Api | user-secrets / Key Vault |
| `Api:BaseUrl` | Web | appsettings.Development / App Service setting |
| `Seed:AdminEmail` · `Seed:AdminPassword` · `Seed:DemoPassword` | Api | user-secrets / Key Vault |
| `BookingPolicy:ClientCancelCutoffHours` (default 24) | Api | appsettings |
| `Database:MigrateOnStartup` (true in Azure only) | Api | App Service setting |
| `ConnectionStrings:Tests` | IntegrationTests | env var in CI, LocalDB default locally |

---

## 10. Code conventions (everyone, every agent)

- **XML doc comment (`///`) on every public class and public method**: what it does and why.
- Adopted external code or patterns: comment with a numbered reference `[n]` matching the README reference list (same numbered style as Part 1).
- **`ILogger<T>`** only — never `Console.WriteLine`. Never log passwords, tokens, emails or requirement text.
- `async`/`await` all the way; every async method takes a `CancellationToken`.
- Controllers are thin: validate shape → call a service → map result. **No business rules in controllers, none in the Web project.**
- EF queries: `AsNoTracking()` for reads, project to DTOs with `Select`, never `FromSqlRaw` with string concatenation.
- Razor: never `Html.Raw` on user content. Every form is `method="post"` with tag helpers; antiforgery is global.
- No inline `style=` attributes or `<style>` blocks — all CSS in `wwwroot/css`.
- Test names: `Method_Scenario_ExpectedResult`.

## 11. Approved NuGet packages

Adding anything not on this list needs a yes in the group chat first.

`Microsoft.AspNetCore.Identity.EntityFrameworkCore` · `Microsoft.EntityFrameworkCore.SqlServer` ·
`Microsoft.EntityFrameworkCore.Design` · `Microsoft.EntityFrameworkCore.Tools` ·
`Microsoft.AspNetCore.Authentication.JwtBearer` · `Microsoft.AspNetCore.OpenApi` · `Scalar.AspNetCore` ·
`Microsoft.Extensions.Http.Resilience` · `Microsoft.ApplicationInsights.AspNetCore` ·
`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` ·
Tests: `xunit` · `xunit.runner.visualstudio` · `Microsoft.NET.Test.Sdk` · `coverlet.collector` ·
`Microsoft.AspNetCore.Mvc.Testing` · `NSubstitute` · `Microsoft.Playwright` · `NetArchTest.Rules`

---

## 12. Decisions that change the Part 1 design

Each entry records a decision that changes or clarifies the Part 1 design, so the Task 3 final report can be updated to match.

| # | Decision | Replaces / clarifies (Part 1) | Why |
|---|---|---|---|
| 1 | **Azure** is the only hosting platform (App Service, Azure SQL, Key Vault, Application Insights) | AWS wording in Security and Running Costs | .NET-native, and Part 1's deployment plan and cloud diagram already use Azure. Security and cost sections are rewritten for Azure in Task 3 |
| 2 | Four booking statuses: Requested, Confirmed, Completed, Cancelled. Rejection = Cancelled with reason | FR12 and the State-pattern class list | Matches the state diagram (Figure 14); one list everywhere |
| 3 | xUnit, WebApplicationFactory and Playwright for .NET for testing | Jest / React Testing Library | Jest is a JavaScript tool; the app is C# |
| 4 | Service has Category, DurationMinutes, nullable BasePrice, IsActive | basePrice-only domain model | Matches the booking flow and allows free consultations |
| 5 | Booking wizard order: time slot first, then requirements | Proposed Booking Process text | The client knows a slot exists before writing a brief |
| 6 | ASP.NET Core Identity's default PBKDF2 password hashing | Argon2id recommendation | Framework-maintained; Part 1 lists PBKDF2 as acceptable |
| 7 | StaffServices link table decides which staff can deliver a service | FR25 had no data behind it | Lets the admin see only qualified, free staff |
| 8 | Slot availability is derived (not blocked, future, no active booking, long enough), never stored | TimeSlot.isAvailable | A stored flag can drift out of sync |
| 9 | Pull requests merge with **merge commits**, never squash or rebase | "Squash-and-merge" in the DevOps section | Keeps each member's commits visible; the rubric marks history |
| 10 | Another user's booking returns **404**, a wrong role returns **403** | Not specified | Doesn't reveal that a booking exists (OWASP IDOR guidance) |
| 11 | Observer notifications are **in-app** (Notifications table + page); email is a stretch goal | Email implied | Works on the hosted system without a paid email service; the pattern is the same |
| 12 | Hosting option 1: one B1 Linux plan, separate staging and production apps, API restricted to the web app's outbound IPs | Staging slots + VNet + private endpoint (Figure 17) | Deployment slots need Standard tier; cost. Trade-off explained in the README |
| 13 | Booking is a partial class: properties (P3) and behaviour (P2) in separate files | – | Two owners never edit the same file |
