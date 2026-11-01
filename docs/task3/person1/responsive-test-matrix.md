# Task 3 responsive and accessibility test matrix

> **PR review status — 28 October 2026:** Earlier local build/test passes are historical execution evidence. A later forced, uncached restore FAILED with Pkcs 9.0.11 NU1902/NU1903; the referenced GitHub CI run remains failed. The October 5 staging E2E failure needs latest-deployment verification. These are external dependency/test-setup blockers, not proven Person 1 regressions. See [owner handover](ci-staging-handover.md) and [verification summary](evidence/verification-summary.md). Review-ready draft does not mean merge-ready or final requirements/accessibility sign-off.


28 October 2026. Windows, .NET SDK 10.0.401, installed Chrome via Playwright CLI 0.1.22 and Playwright module, local HTTPS MVC/API. No TLS/security bypass.

## Final executed scope

- Initial public/error audit: 40 page/width inspections with real API failure on Services.
- Repeated public audit after API startup: 40 inspections with populated real catalogue.
- Populated role audit: 80 inspections (16 page/state visits × five widths), all structural checks passed; 10 real interactions passed.
- Existing .NET E2E final run: 7 passed, 0 failed/skipped. Earlier configured run failed at missing Chromium startup; installed required runtime and reran successfully.
- Isolated actual site.js component regression: 12 assertions passed. These do not simulate backend success.

The configured normal API initialiser seeded its local demo data; no migrations applied. Credentials stayed in process memory. Test mutations affected local demo bookings only. No production action or fake API response.

| Width | Initial public | API-backed public repeat | Populated role inspections | Overflow / H1 / IDs / ARIA / labels |
|---|---:|---:|---:|---|
| 360px | 8 | 8 | 16 | All structural checks passed |
| 390px | 8 | 8 | 16 | All structural checks passed |
| 768px | 8 | 8 | 16 | All structural checks passed |
| 1024px | 8 | 8 | 16 | All structural checks passed |
| 1280px | 8 | 8 | 16 | All structural checks passed |

Checks: document scrollWidth versus viewport, one useful main H1, duplicate IDs, ARIA describedby/labelledby/controls targets, labels for controls; populated audit also detects feedback-error. Public audit checks asset responses. These are scoped structural inspections, not a full axe/Lighthouse or WCAG certification. Lighthouse executable unavailable; axe not invoked. No Task 2 score reused.

## Screen coverage

| Screen/state | Fresh browser evidence | Remaining limits |
|---|---|---|
| Home | Public all widths; actual keyboard skip/Menu checks | Human reader, 200% zoom |
| Login | Public all widths; Client/Staff/Admin real sign-ins passed | Full keyboard-only credential journey |
| Register | Public all widths; labels/ARIA intact | Registration submission and reader acceptance |
| Profile | Real signed-in render at all widths | Actual save/validation walkthrough |
| Services | Failure-state baseline and populated API results all widths | Category filtering edge cases/long descriptions |
| Service detail | Populated real service all widths | Other service types and arbitrary long content |
| Booking slot step | Populated date/radio choices all widths; selection/continue passed | Human touch/radio speech, unavailable/next-range edge cases |
| Booking requirements | Populated fields all widths; actual POST creation passed | Invalid-template/field summary keyboard acceptance |
| Booking confirmation | Real saved Requested booking all widths | Reader acceptance |
| My Bookings | Real cards all widths; keyboard Requested filter passed | Full filter set, pager, actual Back/refresh walkthrough |
| Booking detail | Real saved record all widths; reschedule/cancel reloads passed | Other histories/long values |
| Reschedule | Real slot screen all widths; actual mutation and refreshed Requested state passed | Conflict/stale token UI edge cases |
| Cancel | Screen all widths; actual mutation and refreshed Cancelled state passed | Cutoff denial and full Tab trap test |
| Staff schedule | Real schedule all widths; keyboard Next week passed | Full keyboard-only schedule journey |
| Staff detail | Assigned real detail all widths; existing E2E completion passed | Reader requirements/completion speech |
| Admin dashboard | Real metrics/queue all widths | Long queue and reader interpretation |
| Admin bookings | Unfiltered and Requested filtered states all widths; real POST/redirect preserved Status | Date errors, paging, long rows |
| Admin review | Real booking detail all widths; E2E confirms with API-supplied staff | Reject UI, all dialog keyboard variants |
| Staff ActionFailed | Source reviewed | Error-specific runtime rehearsal |
| Privacy/Access Denied/Not Found/Error | All widths; expected 403/404 retained; direct Error render 200 | Actual exception rehearsal/reader |
| Notifications | P2-owned; integration suite covers API | Human/read-mark UI acceptance with Daniel |
| Admin Services/Time Slots/Users | Present, P3-owned, no edits | Dean's presentation/accessibility acceptance |

## Actual keyboard and feedback results

Home 390px: Tab reached Skip to content; Enter focused main-content; keyboard opened Menu and Escape returned focus to Menu after transition. Real client Requested link activated by keyboard; staff Next week activated by keyboard; admin Apply filters activated by keyboard and preserved Requested. Cancel dialog Escape returned focus to its triggering button. These are targeted keyboard checks, not a complete mouse-free walkthrough of every role.

Ten populated interactions PASS: three real role sign-ins, actual booking creation, Requested keyboard filter, reschedule with refreshed detail, cancel dialog Escape/focus return, cancellation with refreshed detail, staff keyboard next-week, admin POST filter retaining Requested. The normal backend remains authoritative.

Twelve component checks PASS: busy state, label, submitted value retained, repeat prevention, history reset, changed value retained, timeout recovery, modified clicks, link busy/repeat/history, cancelled validation does not lock. History/timer checks dispatch events/callbacks deterministically; full physical Back/refresh and wall-clock 90-second failure still require spot checks.

Actual failure and populated service results are independently recorded. Successful zero-result rendering is source-reviewed; the first populated harness attempt also encountered a genuinely empty Requested list and was corrected to create its own local booking. Initial harness H1 samples during navigation were corrected by waiting for main content before inspecting; this was a test timing issue, not a UI code change.

## Visual evidence and contrast

Fresh captures: `evidence/home-390.png`, `register-1280.png`, `services-390.png`, `services-1280.png`. Public demo content only; no passwords or private booking briefs. Home/registration captures visually inspected; services captures checked separately. Add annotations and captions for final guide/deck. Other selected populated screens have machine-readable evidence rather than fabricated screenshots.

Brand tokens retained; contrast on #20231f charcoal computed as lime #c4f455 12.42:1, white #f5f6f2 14.64:1, muted #b5b9b1 7.97:1. This checks those token pairs, not every possible rendered state. No new motion introduced; existing reduced-motion/forced-colour support retained.

No public CSS/JS/SVG/icon failures recorded. Browser console included expected 403/404 response entries and Kaspersky-injected style CSP violations. Security policy was not relaxed.

## Repeatability and remaining human acceptance

Executable scripts: `src/Exsensic.Web/tests/public-pages.browser.js`, `filter-feedback.browser.js`, `private-pages.browser.cjs`. The private runner uses the installed Playwright module via P1_PLAYWRIGHT_MODULE and E2E_* settings, never writes credentials and produces only structural metadata/results. Supply secrets securely, never in a command history literal. Existing .NET suite uses its documented variables. Its staff-completion scenario consumes an eligible seeded record; prepare valid authorised demo state before repeating it rather than altering domain rules.

Commit-safe evidence is in verification-summary.md and the public/filter JSON files. Raw CLI/build/test output and private role metadata are retained locally outside the repository.

Human NVDA/Narrator acceptance remains **PENDING HUMAN VERIFICATION**. Complete the separate checklist, touch/zoom/long-content testing, full keyboard-only role walkthroughs, remaining filter/error edge cases and final staging rehearsal. Original Part 1 FR wording is still required for exact requirements sign-off.

## Additional live filter verification

The read-only catalogue filter regression passed four checks: keyboard category activation, real returned cards matching Instagram, actual browser Back restoring unlocked controls, and refresh restoring unlocked controls. Evidence: evidence/verification-summary.md (four catalogue checks); executable Web-owned script: tests/catalog-filters.browser.js. No responses were mocked. This supplements the deterministic history tests and remaining role-filter edge-case checklist.
