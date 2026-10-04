# Person 1 API integration

Verified against the merged develop source on 4 October 2026. This replaces the original Step 3 handoff: Contracts is populated and the six screen adapters are registered.

## Flow and ownership

MVC controller → journey (DTO-to-view-model mapping) → typed client → ApiClient → BearerTokenHandler → real API.

- IAuthApi / AuthApi: register, login, profile read/update.
- IBookingsApi / BookingsApi: requirement template, create, mine, detail, reschedule, cancel.
- IAdminBookingsApi / AdminBookingsApi: dashboard, bookings, qualified/free staff, confirm/reject.
- IStaffApi / StaffApi: assigned schedule, completion.
- P3's ICatalogApi is absent. Existing CatalogJourney and SlotPickers keep using the shared transport for real catalogue/availability reads. No competing catalogue DTO/client was introduced.
- P2's INotificationsApi, NotificationsApi and notifications screen are unchanged.

All typed clients use Exsensic.Contracts. Journey interfaces remain useful presentation boundaries: they map DTOs, group Upcoming/Past for display and assemble multi-request screens. They are mandatory controller dependencies; missing registration fails visibly at startup/resolution instead of silently disabling a feature. Registered clients and adapters are scoped. The bearer handler reads the current cookie token for each request and never stores a user token on a pooled handler.

## Authentication and errors

Only a real AuthResponse creates a cookie. Exact RoleNames define role claims and redirects. JWT is stored in protected AuthenticationProperties, never HTML, JavaScript, localStorage or sessionStorage. Cookie expiration never exceeds the returned token expiration or the 30-minute Web session limit. Logout is POST with antiforgery and clears the ticket. Return URLs must be local.

ErrorCodes comes from Contracts. Validation errors map to ModelState; field summaries link to controls. An expired session signs out and explains the sign-in redirect. 403 uses Access Denied; 404 uses the branded safe page. Conflict messages cover slot, version, staff, state and cancellation issues. Retry-After is shown and temporarily disables relevant submissions; no mutation is automatically retried. Server errors never expose response bodies or stacks.

Mutation success redirects to a fresh API read. RowVersion is passed through unchanged; failed forms retain the submitted version and require explicit reload on a conflict. API business rules, authorization, State/Observer behavior and qualified/free staff selection stay in the backend.

## Local dependencies and verification

Use the HTTPS Web/API launch profiles. The checked-in development API root is https://localhost:7184/. Trust the local development certificate and configure the documented API user-secrets before a role-based rehearsal. LocalDB is installed; package restore, build and existing unit/integration suites pass. Missing seed settings mean no local demo accounts/services. Do not substitute fabricated UI data or authentication.

See ../../../docs/accessibility.md for the dated test matrix and limitations; see ../../../docs/demo-script.md for the live rehearsal and teammate handoffs.
