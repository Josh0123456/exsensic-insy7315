# Step 3: MVC API plumbing

This guide describes Web infrastructure only. No account screens, feature endpoints, DTO placeholders or business rules are implemented.

## Configuration and registration

`Program.cs` validates `Api:BaseUrl` at startup and registers the typed shared transport `ApiClient`, `BearerTokenHandler`, `ApiProblemMapper`, and `IHttpContextAccessor`. Existing hosting registrations/middleware are preserved.

Development configuration uses `https://localhost:7184/api/v1/`, matching the API HTTPS launch profile. Change `Api:BaseUrl` in Web development configuration or override it with environment variable `Api__BaseUrl`. No endpoint is hard-coded in controllers or Razor.

Production has no fallback address: deployment must set `Api__BaseUrl` to the API's HTTPS base including `/api/v1/`. The setting must be absolute and contain no credentials, query or fragment. Only Development allows loopback HTTP. A missing trailing slash is normalized. Use relative paths without leading slashes (for example the contract's `auth/login` once its DTOs are available). A leading slash would discard the API path prefix and is rejected. The bearer handler also rejects resolved URIs outside the configured base.

The transport has a 30-second timeout, a 4 MiB response buffer limit and a 64 KiB error-parsing limit. Automatic redirects and cookie containers are disabled. No automatic retries are installed: blindly replaying booking mutations would be unsafe. No certificate-validation bypass is installed.

## Authentication shell and Step 4 handoff

Web uses the standard Cookies authentication scheme, with `__Host-Exsensic.Auth` (HttpOnly, Secure=Always, path=/, SameSite=Lax, no domain). Sliding expiration is disabled; the default ticket lifetime is 30 minutes. Authentication runs before authorization, after routing and the existing forwarded-header middleware.

Step 4 must validate a successful API authentication response, create the real user principal and store the JWT in AuthenticationProperties using `StoreTokens` with the standard name `access_token`. Set the ticket's `ExpiresUtc` no later than the API's `ExpiresAtUtc` (and the desired Web session limit). Do not renew the cookie beyond JWT expiry. Sign in/out using the Cookies scheme. Do not use localStorage, JavaScript-readable token cookies or static/default HTTP authorization headers.

BearerTokenHandler reads the token from the current request's authentication properties for each outgoing request. It never caches a user/token in its pooled handler. Guests and contexts without tokens remain anonymous. API cookies cannot leak between users because the outbound cookie container is disabled.

`/Account/Login` and `/Account/AccessDenied` are configured destinations only; their screens intentionally do not exist until Step 4. No global authenticated fallback policy was added, so the existing Home/Privacy pages remain public. Later protected controllers need normal authorization attributes.

Use the Web **HTTPS** launch profile when testing forms/authentication locally. Secure cookies deliberately are not relaxed for HTTP. In deployed multi-instance environments, hosting must provide persistent/shared ASP.NET Core Data Protection keys for cookie continuity. Tokens in authentication properties are protected as part of the authentication ticket; they are not separately exposed to browser scripts. Review cookie size once the real JWT/claims exist.

## Antiforgery

MVC globally applies AutoValidateAntiforgeryTokenAttribute to unsafe HTTP methods. The antiforgery cookie is HttpOnly, Secure=Always, SameSite=Strict. Later forms should use MVC POST form tag helpers, which generate the normal antiforgery input. Non-tag-helper forms need the normal Html.AntiForgeryToken helper. No JavaScript workaround or blanket exemption was added.

## Shared transport

Inject `ApiClient` into later typed API client implementations (never register those clients as singletons). It exposes:

- `SendAsync<TResponse>`: bodyless JSON-response request.
- `SendJsonAsync<TRequest, TResponse>`: JSON request and response.
- `SendNoContentAsync`: bodyless request where success needs no body.
- `SendJsonNoContentAsync<TRequest>`: JSON request where success needs no body.

All accept HttpMethod, a relative path and CancellationToken. Only Contracts DTOs should fill the generic request/response types once they exist. Caller cancellation propagates; timeouts, network errors and malformed JSON become safe failures. The caller owns constructing/escaping route and query values from the documented API contract.

Check `ApiResult<T>.IsSuccess`, then `HasContent` before using Value. A 204/205, HEAD, empty response or JSON null has no DTO value. No-content methods intentionally ignore any successful response body. HTTP failures expose Error; failures before a response have null StatusCode and do not invent shared error codes. This envelope is a Web transport result, not a duplicated API DTO.

## ProblemDetails and feedback

ApiProblemMapper handles JSON ProblemDetails and ValidationProblemDetails (`code`, `traceId`, `errors` at the top level). It trusts the actual HTTP status over any body status. Missing, malformed, oversized or non-JSON errors receive a status-based fallback. Server title/detail are intentionally not surfaced: locally chosen summaries prevent diagnostic leakage and keep wording consistent. Every 5xx is generic regardless of its claimed code.

The mapper supports all documented wire codes. Contracts currently has no ErrorCodes class, so the mapper uses the exact wire strings directly in one switch. Replace those literals with the real shared constants when P2 merges them; no second ErrorCodes class exists.

For HTTP 400, field names and error arrays are retained (up to 100 fields, 10 messages each). Overlong, control-character or exception/stack-trace-shaped validation messages receive a generic field message. API validation messages must still be safe plain text; this layer cannot determine whether arbitrary prose contains private information. Do not put diagnostic detail or echoed passwords into API validation errors.

Both delta-seconds and HTTP-date Retry-After forms are retained on 429 responses as RetryAfterDelay and RetryAfterUtc. Screens can use these to display a countdown or retry time without parsing headers. TraceId is retained as bounded identifier text for correlation, never as raw exception content.

For redisplaying a form:
```csharp
result.Error!.AddToModelState(ModelState);
// Optional prefix when a screen nests fields under a model property:
// result.Error!.AddToModelState(ModelState, "Input");
```

For a redirect:
```csharp
result.Error!.AddToTempData(TempData);
```

AddToModelState adds the friendly page summary and individual field messages. Later screens must render a validation summary and asp-validation-for next to their labelled controls. ModelState is case-insensitive; adapt API field paths in the future typed screen if DTO and view-model names differ.

AddToTempData stores only the friendly summary using the existing ToastKeys.Error. The existing _Toasts partial displays encoded text in its polite live region. Neither helper uses Html.Raw or copies raw title/detail into a toast.

## Logging

The shared transport logs only HTTP status through ILogger<ApiClient>. It never logs request URLs, headers, tokens, bodies, field errors or exception objects. Default HttpClient loggers are removed for this client to avoid incidental URL/header logging. Existing hosting telemetry was not changed; its independent deployment configuration remains with the hosting owner.

## Missing contracts / ownership

No Person 1 feature client interface or implementation can yet be declared safely: Contracts currently contains only its project file.

| Client | Shared definitions needed | Owner |
| --- | --- | --- |
| IAuthApi | RegisterRequest, LoginRequest, AuthResponse, ProfileDto, UpdateProfileRequest; RoleNames for later sign-in/authorization | P3 |
| IBookingsApi | CreateBookingRequest, BookingCreatedDto, BookingSummaryDto, BookingDetailDto, BookingStatusHistoryDto, RescheduleBookingRequest, CancelBookingRequest; PagedResult and BookingStatus | P2 |
| IAdminBookingsApi | DashboardDto, ConfirmBookingRequest, RejectBookingRequest, StaffOptionDto; booking DTOs, PagedResult and BookingStatus | P2 |
| IStaffApi | CompleteBookingRequest and booking summary/detail/status definitions | P2 |

P2 also needs to merge Common/ErrorCodes. P3 needs to supply the real auth/me endpoints for Step 4 end-to-end sign-in/profile work. No catalogue/users/notifications clients belonging to other people were added. The shared transport can be reused by their own later clients.

Verification for this step is build/static inspection plus the requested existing solution test run. No tests were added to another person's test project.
