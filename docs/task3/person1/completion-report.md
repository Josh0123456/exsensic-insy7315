# Person 1 Task 3 completion report — 28 October 2026

> **PR review status — 28 October 2026:** Earlier local build/test passes are historical execution evidence. A later forced, uncached restore FAILED with Pkcs 9.0.11 NU1902/NU1903; the referenced GitHub CI run remains failed. The October 5 staging E2E failure needs latest-deployment verification. These are external dependency/test-setup blockers, not proven Person 1 regressions. See [owner handover](ci-staging-handover.md) and [verification summary](evidence/verification-summary.md). Review-ready draft does not mean merge-ready or final requirements/accessibility sign-off.


## A. Repository and branch

Repository: C:\Users\Chris\AndroidStudioProjects\exsensic-insy7315. Branch: feat/p1-task3-feedback-improvements. Baseline cd70da1. Starting tree clean. Ending tree contains only this pass's authorised Web/documentation changes. No commit, push, merge, deployment or branch change performed. Initial Git access error was sandbox-specific; read-only unsandboxed Git succeeded and normal Git worked after repository access was granted. No Git configuration was changed.

## B. Lecturer feedback assessment

The supplied exact feedback and marks, before/after changes, verification and limitations are recorded in feedback-improvement-log.md and the final-report section. Branding was preserved; missing filter busy feedback was still present and is implemented. Human reader testing remains pending. Weak requirements explanation and rushed/unvoiced presentation are addressed by evidence drafts and a paced spoken storyboard, not by claiming a rehearsal/delivery occurred.

## C. Changes implemented

Added opt-in pending feedback to Admin booking filters, My Bookings filters, Services categories, Staff week navigation, booking availability ranges and reschedule availability ranges. Shared script guards repeat requests/activations, preserves payload values, restores labels on history return and provides stalled-navigation recovery. Shared status is visible and politely announced; styles use existing tokens. No new business rules or dependency packages.

## D. Filter-form improvements

| Existing interaction | Type | Change |
|---|---|---|
| Admin status/from/to Apply filters | POST then redirect | data-single-submit + filter recovery + Applying filters message |
| My Bookings Upcoming/Requested/Confirmed/Past/Cancelled | GET links | Group pending/repeat/history handling |
| Services All/category | GET links | Group pending/repeat/history handling |
| Staff Previous/This/Next week | GET links | Group pending/repeat/history handling |
| Book Earlier/Show later dates | GET links | Shared available-date pending message |
| Reschedule Earlier/Show later dates | GET links | Shared available-date pending message |
| Admin Clear filters | Navigation escape | Kept ordinary link; no business mutation |
| Date selection within slot picker | Local radio/date interaction | Existing behaviour retained; not misrepresented as server navigation |

## E. Accessibility audit

40 public/error page-width inspections: one main H1, no duplicate IDs, no missing referenced ARIA targets and no unlabelled form controls. Existing focus, validation, slot fieldsets, modal handling and textual statuses retained. Public keyboard skip/Menu checks passed. Human reader checklist covers Client/Staff/Admin with explicit pending result fields. Lighthouse executable unavailable; axe not invoked. No full WCAG compliance claim.

## F. Responsive audit

360/390/768/1024/1280: eight public/error routes each; no document overflow detected. Real screenshots of Home 390 and Registration 1280 visually inspected. An additional 80 populated inspections and 10 interactions passed; arbitrary long-content/edge-case coverage is still limited. Details and screenshots: responsive-test-matrix.md and evidence/.

## G. UX/design consistency

Existing near-black/charcoal/lime identity retained. One visible action-status region also provides polite speech feedback, with no new animation. Computed text contrast on charcoal: lime 12.42:1, white 14.64:1, muted text 7.97:1. Public Services API failure rendered as an error, never an empty result. No static asset failures recorded. Expected 403/404 console responses and Kaspersky-injected style CSP violations are identified separately; no security headers weakened.

## H. Requirements mapping

requirements-traceability.md registers FR01–FR33 with all requested columns. Exact original wording unavailable; FR12/FR25 have only secondary CONTRACTS citations. 33 IDs accounted for; zero original-wording acceptances claimed; real feature E2E results recorded separately. Separate role feature inventory links actual screens/APIs, methods and benefits without guessing per-ID assignments.

## I. Part 1 documentation improvements

Documented four final statuses, rejection as Cancelled with reason, rescheduling back to Requested when applicable, slot-first wizard, separate MVC/API, Azure final hosting and in-app notifications. Original Task 1 marking feedback unavailable; history is not silently rewritten. Adoption/training and handover approach is included.

## J. Client user guide

client-user-guide-draft.md covers all 19 requested client procedures with purpose, route, numbered actions, expected result, troubleshooting and screenshot/caption instructions. Final website/support placeholders must be filled by the team. Notification instructions await Daniel's final label acceptance; no Notifications code edited.

## K. EXPO preparation evidence

expo-speaking-evidence.md provides a four-minute segment, spoken code explanation, five feature/benefit examples, visual storyboard, owner transitions, questions/answers and failure fallbacks. Two genuine public captures supplied. Actual deck assembly, annotations, rehearsal and narration recording remain human work.

## L. Actual tests

Earlier local build: PASS (superseded for current clean-build readiness by the failed fresh restore), 0 warnings/errors. Fresh solution tests: 110 unit and 104 integration PASS; 0 failed, 0 skipped in these suites. E2E final run: 7 passed, 0 failed/skipped. Existing seed secrets were discovered, configured in process memory and used against local demo data. First configured run failed at missing-browser startup; installing its existing required runtime resolved this. Twelve isolated DOM regression assertions PASS; forty public browser inspections passed the recorded structural checks. JavaScript syntax and git diff --check PASS. LF-to-CRLF advisories are normalisation warnings, not whitespace errors. Commit-safe aggregate and public structural records are in evidence/. Raw outputs are preserved locally outside the repository.

Authenticated core journeys and selected keyboard filters passed in the additional audit. Full keyboard-only role walkthroughs and remaining edge cases are not claimed. Isolated DOM checks use the actual script with minimal test controls; they do not fabricate API responses or authentication. Node's request context did not trust the development TLS certificate; the fixture fetches only the public script via existing loopback HTTP. Actual page checks used trusted HTTPS in Chrome. No trust/security setting was weakened.

Final local verification on 28 October: 7/7 existing E2E tests passed after installing their required Chromium runtime. Existing seed settings were found in user secrets and used only in process memory. The normal API initialiser populated its configured local demo records; no migrations were applied. An additional Web-owned audit passed 80 populated page/width inspections and 10 interactions across Client, Staff and Admin, including real creation, reschedule/cancellation, cancel-dialog Escape/focus return, keyboard Requested/week navigation and admin POST filtering. A second 40-case public audit passed with real catalogue data; the first 40-case API-failure audit is retained separately. Commit-safe evidence: evidence/verification-summary.md (historical local E2E), private-browser-results.json and public-populated-browser-results.txt. No production resources were touched.

## M. Security/ownership review

Full tracked diff and added-file inventory reviewed. Changes are restricted to Web and authorised docs. No new credentials, JWTs, connection strings, browser token storage, unsafe HTML, redirects or backend rules. Existing secure cookie properties, API authority, role restrictions, encoding, POST/antiforgery and confirmation behaviour retained. Test projects belonging to teammates were not edited. No external production actions. This review is scoped to this change, not a new full security certification.

## N. Remaining manual work

1. Obtain approved original Part 1 FR01–FR33 wording/version/page references and reconcile the matrix.
2. Repeat final staging acceptance with authorised Client/Staff/Admin accounts; local configured seed accounts already passed.
3. Complete remaining keyboard-only journeys, paging/back/refresh, date validation, successful-empty and conflict edge cases; core populated flows already passed at five widths.
4. Perform and sign the NVDA/Narrator checklist; record actual speech, defects and retests.
5. Coordinate final staging E2E/management acceptance with the owners. Local E2E already passed. The staff completion test consumes an eligible seeded booking; arrange valid test data before repeating that scenario.
6. Annotate accepted screenshots, insert final website/help contacts, rehearse and record the voiced EXPO segment.
7. Review diff, commit manually, then add real commit/PR identifiers to the report.

## O. Final report text

person1-final-report-section.md contains the complete 15-section ready-to-copy draft plus the final feedback/mark/improvement/verification/evidence/status table. It accurately distinguishes implemented changes from pending acceptance. Christopher must fill the marked evidence/source placeholders before final publication.

## P. Final changed-file inventory

See commit-inventory.md for absolute paths and the inventory appended below. The allowlist distinguishes modified and new reviewable files. Build outputs are generated/ignored, not submission source changes.

## Q. Recommended Git commit

Title: feat(web): address Person 1 Task 3 feedback and prepare evidence

- Add accessible filter busy feedback with repeat guards and history/stall recovery.
- Preserve Exsensic branding and existing API/security boundaries.
- Add Web browser regression scripts and fresh verification evidence.
- Prepare requirements register, client guide, screen-reader checklist and EXPO notes.
- Document actual results and limitations in the Person 1 final-report section.

Before committing: review evidence for privacy and accuracy; original FR source, authenticated acceptance and human reader testing remain open. No commit/push was performed.

## R. Overall Person 1 Task 3 status

**PARTIALLY COMPLETE — SPECIFIC BLOCKERS**.

The authorised code improvements and requested report/guide/evidence drafts are implemented. Fresh build, unit/integration and scoped browser checks pass. Exact original requirements mapping remains blocked by the missing original source; local populated core journeys now pass. Human screen-reader testing and actual EXPO rehearsal necessarily require human participation. These limitations are explicit throughout the deliverables; no complete submission or guaranteed-mark claim is made.

## Inventory

See [commit inventory](commit-inventory.md) for the exact allowlist and excluded local artifacts. The former machine-specific inventory and private raw output are excluded from the commit.
