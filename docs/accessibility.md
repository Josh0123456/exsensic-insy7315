# Accessibility and responsive review — Person 1

Inspection and implementation date: **4 October 2026 (SAST)**. Baseline: `develop`, `c76e25a`. This is an evidence record, not a WCAG certification or a claim that every authenticated journey passed browser testing.

## Scope and current limits

Every existing Razor screen was source-reviewed, including P2's Notifications page. P2 notification logic/client and P3-owned backend code were not changed. Admin Services, Admin Time Slots and Admin Users controllers/views are absent in this checkout; their runtime accessibility cannot be tested.

The .NET 10.0.401 SDK and LocalDB work. API startup created the normal `ExsensicDev` schema through the existing migration, then skipped demo data because `Seed:DemoPassword` is missing. There is no API user-secrets file or E2E account configuration. The user has now trusted the existing HTTPS development certificate. A restarted Web process successfully reads the real API over HTTPS (the services response is an empty list). Windows/.NET and Chrome accept HTTPS; the in-app browser still reports a certificate authority error, so its interactive checks used the existing public HTTP routes only. No certificate checks were bypassed, no fake sign-in was added and no synthetic production/UI data was introduced. Login and Register have Chrome Lighthouse and real MVC TestServer evidence, but account submission and complete client/staff/admin workflows still need a local or staging rehearsal with configured accounts/data.

## Screen coverage

“Source” means markup, controllers, adapters, shared components and stylesheet review. “Browser” means the actual local MVC page; catalogue checks covered its honest API-failure state before the Web restart and its real empty state afterward, not populated results. TestServer runs the real Web application in memory and does not replace authentication.

| Screen | Source | Runtime evidence | Remaining check |
|---|---|---|---|
| Home | Reviewed | Browser at all five widths; Lighthouse; keyboard skip/menu | Human screen reader |
| Login | Reviewed | Real MVC TestServer render, safe return URL, antiforgery; Chrome Lighthouse | Credentials and keyboard sign-in |
| Register | Reviewed | Real MVC TestServer render and invalid POST with linked errors; Chrome Lighthouse | Interactive registration/keyboard validation if authorized |
| Profile | Reviewed; email is read-only | Anonymous access challenges correctly | Real profile save and keyboard validation |
| Service catalogue | Reviewed | Browser failure and real empty states at all widths; Lighthouse | Populated database responses |
| Service detail | Reviewed | Compiles | Real service and long content |
| Booking Step 1 | Reviewed; radio validation linked | Anonymous access challenges correctly | Real availability, touch and keyboard date/radio selection |
| Booking Step 2 | Reviewed; template help/errors linked | Compiles | Real template, invalid fields, successful create |
| Booking confirmation | Reviewed | Compiles | Fresh GET after a real booking |
| My Bookings | Reviewed | Anonymous access challenges correctly | Real paging, statuses, Upcoming/Past filters |
| Booking detail | Reviewed | Compiles | Real requirements, history and action eligibility |
| Reschedule | Reviewed | Typed transport preserves RowVersion | Real slot conflict and stale-version workflow |
| Cancel | Reviewed; policy wording corrected | Compiles | Real deadline rejection and confirmation dialog |
| Notifications | Reviewed; implementation left intact | Anonymous access challenges correctly | Real messages, mark-read and role-specific links |
| Staff schedule | Reviewed | Anonymous access challenges correctly | Populated schedule and previous/next week |
| Staff booking detail / action failed | Reviewed | Compiles | Completion, errors, modal keyboard/focus return |
| Admin dashboard | Reviewed | Anonymous access challenges correctly | Real counts, queues and responsive metrics |
| Admin bookings | Reviewed; filter errors corrected | Compiles | Populated table/cards at 767/768 breakpoint |
| Admin booking review | Reviewed | Typed clients registered | Qualified/free staff, reject/confirm and stale-version dialogs |
| Admin Services / Time Slots / Users | **Absent** | Not testable | P3 implementation and subsequent cross-screen review |
| Privacy | Reviewed | Browser all widths; Lighthouse; TestServer | Final privacy content belongs to team |
| Error | Reviewed | Browser all widths; Lighthouse | Actual unexpected failure rehearsal |
| Not Found | Reviewed; status middleware added | Real unknown URL returns branded 404; browser all widths; Lighthouse | Human screen reader |
| Access Denied | Reviewed | Real 403; browser all widths; Lighthouse | Human screen reader |

## Accessibility changes and retained behavior

- One H1 supplied by each page/header; reusable empty-state titles are styled paragraphs so they cannot create a skipped heading level in different contexts. Existing cards use H3 below section H2s.
- Server validation summaries are focusable, labelled and link to fields. The final browser review found that MVC appended its original list alongside the replacement list; the TagHelper now clears the framework content, and a regression assertion verifies exactly one list. Hidden concurrency tokens never receive misleading focus links. Messages use encoded TagBuilder text, never user-content raw HTML.
- Client validation updates `aria-invalid`/`aria-describedby`, builds linked summaries and focuses them. Template control IDs use the same MVC sanitizer as summary anchors. Numeric requirement help is associated with the field.
- Slot radio groups require a selection, identify invalid state and reference their field error and timezone. Disabled/hidden dates cannot submit a stale selection.
- Visible labels and required/optional wording remain; password guidance now reflects the merged Identity policy. Profile email explicitly explains that it cannot be edited.
- Skip link targets focusable `main`; shared header/nav/main/footer landmarks remain. Public keyboard checks confirmed the skip link, Enter to open the menu, Escape to close it and return to the Menu button.
- Native confirmation dialogs retain cancel-first focus, Tab/Shift+Tab wrapping, Escape and focus return to the initiating button. **Source-reviewed only for authenticated actions; real-account keyboard verification remains pending.**
- Written status names accompany all status colors. Rejection stays Cancelled with its reason. Existing table captions/row/column headers and mobile definition-list cards remain.
- Submission state changes button text, exposes `aria-busy` and a polite live status, and blocks duplicate submits without removing submitter values. Back/forward navigation restores controls. Retry-After windows temporarily disable further submissions; no requests are automatically replayed.
- API failure is distinct from no results. Expired sessions explain why sign-in is required. Unknown URLs show a branded 404. Browser-facing exceptions use the generic error screen even in Development; diagnostics remain in logs.
- The decorative E logo is SVG geometry within an `aria-hidden` wrapper. This resolves Lighthouse's label/accessible-name mismatch without changing the brand.
- Reduced-motion styling was retained; forced-colors focus/selected-control borders were added. Focus outlines remain lime and visible.

## Responsive and consistency changes

Explicit intrinsic-width constraints prevent grids, navigation, cards and action groups widening the page. Long references/messages/status labels wrap. Phones use 16px outer gutters and smaller panel padding; action buttons, slot labels and filter controls fit one column. Navigation wraps correctly while collapsed below 992px. Admin tables remain desktop-only from 768px, with labelled booking cards below that breakpoint. Inputs and primary actions retain 48px minimum height; navigation/filter links and relevant secondary text actions provide 44px targets where practical. No inline styles, alternate theme or replacement Bootstrap appearance was introduced.

| Width | Browser pages checked | Horizontal page overflow | H1 / IDs / description references |
|---|---|---|---|
| 360px | Home, Privacy, Services failure/empty, Access Denied, Not Found, Error | None | One H1 each; no duplicate IDs or missing descriptions |
| 390px | Same six pages | None | Pass; mobile menu keyboard check also performed |
| 768px | Same six pages | None | Pass |
| 1024px | Same six pages | None | Pass |
| 1280px | Same six pages | None | Pass |

These 30 page/width observations, plus five repeated catalogue-empty observations after restarting Web, do **not** establish that populated authenticated tables, dialogs or long database values fit at every width. Repeat the matrix on every pending screen with real data.

## Contrast checks

Calculated from the committed sRGB tokens (relative-luminance formula), using charcoal `#20231f` unless noted. These samples exceed 4.5:1 for ordinary text, and the control border exceeds 3:1. Disabled-control colors are not claimed to meet normal-text thresholds.

| Foreground / background | Ratio |
|---|---:|
| White `#f5f6f2` / charcoal | 14.64:1 |
| Muted `#b5b9b1` / charcoal | 7.97:1 |
| Lime `#c4f455` / charcoal | 12.42:1 |
| Strong lime `#a9d83f` / charcoal | 9.52:1 |
| Near-black `#121511` / lime button | 14.39:1 |
| Control border `#85907c` / charcoal | 4.75:1 |
| Error text `#ff9eaa` / charcoal | 8.11:1 |

## Automated evidence

- `dotnet build Exsensic.sln`: passed, zero warnings/errors.
- `dotnet test Exsensic.sln`: 110 unit + 91 integration tests passed; zero failures/skips in those suites.
- E2E project intentionally does not run when `E2E_BASE_URL` is unset. Its absence from the run is **not** an E2E pass. Local Playwright browser cache and six E2E account variables are absent.
- 22 additional isolated frontend checks passed: real MVC routes, required DI registrations, real anonymous authorization challenges, invalid registration errors/password clearing, external return-URL sanitization, antiforgery, RowVersion serialization, conflict handling, Retry-After and generic server errors. The temporary test harness is kept with this inspection's external evidence, not in another person's test project. It uses a recording HTTP handler only for transport-unit checks, never to sign in or populate accessibility screens.
- `git diff --check`: passed. JavaScript syntax checked with `node --check`.
- Fresh Lighthouse accessibility audits ran through installed Chrome on HTTPS: Home, Privacy, Services, Access Denied, Not Found and Error, plus Login and Register. All eight recorded 100/100 and zero failed accessibility audits; the SVG logo-name fix passed. The reports retain a page-load timeout warning (results may be incomplete), plus expected HTTP 403/404 warnings on those feedback pages. These scores therefore are evidence with a tooling caveat, not full accessibility sign-off. HTML/JSON reports accompany the external inspection output. Error pages are intentionally audited with `--ignore-status-code`; TLS validation was not bypassed.
- Standalone axe DevTools was not driven. Lighthouse's accessibility audits provide automated axe-based evidence, supplemented by DOM/source/keyboard checks. No score is supplied for untested authenticated pages.
- No browser console warnings/errors were recorded in the six-page in-app browser pass. Missing-asset and full authenticated console checks still need the real-account rehearsal.

## Human checks before sign-off

1. HTTPS trust is complete. Configure the documented API secrets and approved demo accounts/data, or use the team's working staging environment. Restart long-running clients after trust changes.
2. Run the client keyboard path: sign in → catalogue → service → slot → requirements → confirmation → My Bookings → detail. Trigger empty/invalid fields; follow summary links and listen to errors. Confirm the API really persisted the booking.
3. Run staff sign in → schedule → detail → permitted completion; test dialog Tab/Shift+Tab, Escape, cancel, confirm and focus return. Do not alter the clock or bypass the start-time rule.
4. Run admin sign in → dashboard → bookings → review; confirm only returned qualified/free staff appear, and rejection is Cancelled with a reason. Test a concurrency conflict by changing the real booking in another authorized session, then explicitly reload.
5. At 360/390/768/1024/1280, inspect every populated screen in the matrix. Check 200% zoom, long reference/contact/requirement strings, 44px practical touch targets and the table/cards breakpoint. Check real API timeout, failure and empty states separately.
6. Spot-check NVDA or Windows Narrator: landmark/heading lists, visible label names, required fields, invalid/description announcements, summaries, radio groups, toasts and modal focus. Screen-reader automation was not performed.
7. After P3 screens merge, include them in the same audit; request teammate fixes for their business logic rather than duplicating it in Web.

**Sign-off:** Person 1 implementation and documentation are ready for review, but full Steps 11–12 acceptance remains pending authenticated browser, screen-reader and missing-P3-screen evidence.
