# Person 1 Task 3 final report section

> **PR review status — 28 October 2026:** Earlier local build/test passes are historical execution evidence. A later forced, uncached restore FAILED with Pkcs 9.0.11 NU1902/NU1903; the referenced GitHub CI run remains failed. The October 5 staging E2E failure needs latest-deployment verification. These are external dependency/test-setup blockers, not proven Person 1 regressions. See [owner handover](ci-staging-handover.md) and [verification summary](evidence/verification-summary.md). Review-ready draft does not mean merge-ready or final requirements/accessibility sign-off.


Christopher Doyle — ST10445478. Prepared 28 October 2026. Evidence version: working changes on feat/p1-task3-feedback-improvements, baseline cd70da1. PR: [TO BE INSERTED BY CHRISTOPHER AFTER REVIEW]. Commit: [TO BE INSERTED AFTER MANUAL COMMIT].

## 1. Role and responsibilities

My responsibility was the Exsensic frontend: visual design, client experience, accessibility, responsive layouts, client bookings, staff schedule/detail screens and the admin dashboard/booking-review interface. I also prepared requirements traceability, client training material and my EXPO explanation. Booking business rules, database implementation, security APIs and deployment remained with the responsible team members. Daniel's Notifications screen and Dean's Admin Services, Time Slots and Users screens were not edited.

## 2. Overview of previous work

The existing portal supports service discovery, slot selection, service-specific requirements and booking progress. Tasks 1 and 2 established the client problem and planned journeys, then delivered the dark Exsensic identity, reusable components, MVC screens and real typed API integration. The original Part 1 submission was not available in this checkout, so this account of earlier design decisions relies on the descriptions retained in CONTRACTS §12 and architecture §8.

Task 1 individual marking feedback was not available for verification; this section compares the submitted Part 1 baseline with the final implementation. The original submission itself must be supplied for precise wording and numbering reconciliation.

The frontend helps clients browse independently and track requests while Exsensic retains approval and employee assignment control. MVC consumes a separate API; presentation adapters map Contracts to views rather than creating booking data in the browser.

## 3. Lecturer feedback received

The following results and wording were supplied from Christopher's Task 2 feedback sheet. Group presentation marks are not solely my individual grade.

| Criterion | Mark | Feedback |
|---|---|---|
| Frontend Visual Design & Branding | 8/10 | “Good dark theme tokens with a lime brand colour, rem scales and status colours always paired with labels. Little to change; a robust, accessible design system.” |
| Frontend UX & Feedback | 8/10 | “Nice work on field validation messages, toasts, status labels and busy states on single-submit forms. Only small polish left: give the filter forms the same busy state.” |
| Responsiveness & Accessibility | 13/15 | “Well-built accessibility: a skip link, visible focus, reduced motion, ARIA and label attributes and Lighthouse checks. Still to do is the human screen-reader pass.” |
| Technical Demo & Implementation | 15/20 | “Slightly rushed.” |
| Requirements Alignment | 2/5 | “Better elaboration was needed.” |
| Communication & Professionalism | 7/10 | “The code walkthrough was not voiced over, and there were no supporting visual aids; the repo .md files were mainly used, so it did not feel like a presentation.” |

## 4. Task 3 improvement objectives

The implementation priority was consistent filter feedback without redesigning the praised interface. The documentation priorities were a clearer requirement-to-evidence explanation, a practical client guide and a paced, voiced EXPO segment. Accessibility work had to distinguish automated checks from the outstanding human screen-reader test.

## 5. Improvements implemented

| Before | Improvement and files | Behaviour after change | Verification / benefit |
|---|---|---|---|
| Admin booking filter lacked the shared busy hook | Views/Admin/Bookings.cshtml and site.js | Applying filters message, preserved status/date values, repeat-submit guard | Isolated DOM tests; less uncertainty during navigation |
| My Bookings, service categories and staff weeks had no pending feedback | Views/Bookings/Index.cshtml, Services/Index.cshtml, Staff/Index.cshtml | Real links retain URLs/keyboard use; guarded pending group with specific loading message | Isolated link checks; prevents repeated activation |
| Availability-range links had no feedback | Views/Book/Index.cshtml and Bookings/Reschedule.cshtml | Earlier/later date navigation uses the same shared behaviour | Compiles and shares tested opt-in handler; selected populated rendering passed |
| Pending message was visually hidden | Views/Shared/_Layout.cshtml and journeys.css | One visible, polite action status using existing brand tokens | Public layout renders; screenshot/DOM evidence; human speech check pending |
| A failed filter navigation could retain a lock | site.js | pageshow resets state and labels; 90-second recovery permits retry; no automatic resubmission | Component checks exercise recovery/history/cancelled validation |
| Requirements and presentation evidence was weak | docs/task3/person1 documents | Source-labelled FR register, benefit explanations, guide and timed visual storyboard | Documentation review; original source/rehearsal still required |

## 6. Frontend design and branding

The near-black, charcoal, white and lime identity was retained because it was praised and already consistent with Exsensic's agency presentation. The change adds a small token-based status panel rather than a new visual system. Existing spacing, radii, headings, button treatment and written status labels remain. No additional package or animation was introduced.

Fresh captures: Figure P1-1, Home at 390px (`evidence/home-390.png`); Figure P1-2, Registration at 1280px (`evidence/register-1280.png`). These are genuine public-screen captures, not populated booking evidence. Annotate them before final Word/deck publication.

## 7. UX and filter feedback improvements

The real admin filter remains POST with antiforgery and a redirect to its shareable read-only URL. Controls are not disabled during payload collection, so selected values remain available. The submit button stays focusable; aria-disabled communicates the pending state and an event guard prevents another submission. Link filters retain native navigation, Enter activation and modified-click/new-tab behaviour.

Only one shared status region announces progress; the same message is visible. Browser restoration clears the pending state and original labels are restored. A stalled filter can be retried after the recovery interval. No request is repeated automatically and no transient busy state claims backend success. Client-side invalid submissions do not leave the form locked. The existing error branches remain distinct from genuine zero-result views.

## 8. Accessibility improvements

The source review covered labels, headings, landmarks, validation summaries, required fields, slot groups, status labels, pagination, dialog focus handling and responsive alternatives in Person 1 screens. Existing skip navigation, focus styles, encoded validation, native dialogs, reduced-motion and forced-colour rules were retained.

Eighty public page/width inspections and 80 populated inspections found one useful H1 per main region, no duplicate IDs, no broken ARIA references and no unlabelled form controls. Actual keyboard checks confirmed Skip to content and mobile-menu Escape/focus return after the opening transition. Twelve component assertions exercised the shared filter script. These structural checks do not certify WCAG compliance. Lighthouse was unavailable and axe was not run; no historical score is reused.

A human NVDA/Narrator pass remains **PENDING HUMAN VERIFICATION**. The prepared checklist supplies action, expected speech, keyboard behaviour, result, notes and defect fields across Client, Staff and Admin booking journeys. No human test has been falsely marked complete.

## 9. Responsive testing

| Width | Fresh result | Limitation |
|---|---|---|
| 360px | Eight public/error routes: no horizontal document overflow; structural checks passed | Selected populated views passed; long-content edge cases pending |
| 390px | Same eight routes passed; Home keyboard/navigation check and screenshot | Core role flows passed; human touch walkthrough pending |
| 768px | Same eight routes passed | Selected populated admin table/cards checks passed |
| 1024px | Same eight routes passed | Long private content pending |
| 1280px | Same eight routes passed; Registration screenshot | Core booking flows passed; remaining edge cases pending |

Routes were Home, Services, Login, Register, Privacy, Access Denied, Not Found and Error. Services initially displayed an honest API-failure state; a second 40-case public audit verified its real populated catalogue. An additional 80 selected populated role page/width inspections passed. Expected 403/404 responses are separate from broken routes. No static asset failures were recorded. Kaspersky-injected inline-style CSP violations were observed and security headers were not weakened to suppress them. Selected private views and cancel-dialog focus return passed. Arbitrary long references, human touch testing and full assistive-technology acceptance remain open before a production-ready claim.

## 10. Requirements alignment

The complete FR01–FR33 register is in `requirements-traceability.md`. It connects IDs to source availability, original need, implementation, route/API, owner, evidence, method, result, business benefit and outstanding work. All 33 IDs are present, but the approved original wording is unavailable. CONTRACTS §12 provides secondary context for FR12 (four-state lifecycle) and FR25 (staff qualification), not complete original statements. Accordingly no requirement is marked PASS solely because its screen exists.

The separate feature inventory explains verifiable business benefits: catalogue self-service reduces information requests; API availability reduces repeated appointment coordination; booking history improves traceability; admin review preserves assignment control; staff schedules bring work and requirements together. Exact per-ID mappings must be reconciled from Part 1 rather than guessed from these feature groups.

Final design differences are explicit: Requested, Confirmed, Completed and Cancelled; rejection is Cancelled with a reason; rescheduling applicable bookings returns them to Requested; slot-first then requirements; Azure final hosting; separate MVC/API; in-app notifications. These are final decisions and do not silently replace original Part 1 history.

## 11. User guide and change management

The client guide contains 19 procedures covering access, account management, services, booking, status/history, rescheduling, cancellation, notifications, profile changes and recovery. Each procedure has a purpose, route, numbered actions, expected result and screenshot instructions. Control names were checked against current views. Technical setup and Staff/Admin guide sections remain with their owners.

For handover, demonstrate a real authorised demo request, let a client practise the same journey, explain Requested versus Confirmed and SAST, then provide the illustrated guide and approved support contact. Record questions/defects and repeat acceptance after fixes. Final URL, help contact and populated screenshots remain publication dependencies.

## 12. Testing and quality assurance

Earlier local execution on 28 October 2026: build PASS (later clean restore FAILED; see the review status above), zero warnings/errors; solution tests PASS with 110 unit and 104 integration tests, zero failures/skips in those suites; site.js syntax PASS; diff whitespace check PASS. The initial solution run left E2E disabled. Existing seed secrets were subsequently found and supplied in process memory. The first configured run failed 7/7 at startup due to missing Chromium; its required browser runtime was installed. The final configured run passed all 7 tests with zero failures/skips, covering role sign-ins, real creation/confirmation, staff completion and stored-script encoding.

Browser verification: 40 initial public/error route-width inspections, 40 further API-backed public inspections, 80 populated role inspections with 10 interactions, and 12 isolated DOM assertions using actual site.js. The component fixture has no fake users/API data and is explicitly not an authenticated end-to-end test. Commit-safe summaries and repeatable scripts are included; raw outputs remain local-only. Existing test projects owned by other members were not edited. The first sandboxed build failed because Windows service access was denied; the approved unsandboxed build/test runs succeeded.

## 13. Evidence of contributions

Code paths changed: Web Views/Admin/Bookings, Views/Bookings/Index and Reschedule, Views/Services/Index, Views/Staff/Index, Views/Book/Index, Views/Shared/_Layout, wwwroot/js/site.js and wwwroot/css/journeys.css. Added Web-owned browser scripts: tests/filter-feedback.browser.js and tests/public-pages.browser.js, catalog-filters.browser.js and private-pages.browser.cjs.

Documentation: feedback-improvement-log, requirements-traceability, accessibility-manual-test-checklist, responsive-test-matrix, client-user-guide-draft, expo-speaking-evidence and this report; dated updates to accessibility.md and demo-script.md; sanitised browser evidence and genuine public screenshots. Git evidence is baseline cd70da1 plus this uncommitted diff. No future commit or PR identifier is fabricated.

## 14. Remaining limitations

The original Part 1 source is required for exact FR wording. Core populated Client/Staff/Admin flows passed with existing local seed configuration. Final staging, full keyboard-only and remaining edge-case acceptance are still required. The human reader checklist must be executed and signed. Lighthouse/axe scores were not generated. Presentation rehearsal, screenshot annotations, final help/website details and final deployed acceptance are human/team tasks. Dean and Daniel must accept their owned screens; Josh owns CI/deployment. This pass does not claim to resolve older deployment or backend findings without fresh evidence.

## 15. Reflection and conclusion

Task 3 addresses the lecturer's specific filter-feedback request while retaining the established design. The improvement makes pending navigation clearer, reduces accidental repeated actions and preserves normal history behaviour. The real API-backed browser runs verified core journeys; the new evidence structure also makes their limits visible. Requirements and presentation material are more explicit, but exact Part 1 reconciliation, human assistive-technology acceptance, final edge-case testing and a rehearsed populated demonstration are still necessary before final submission.

## Final improvement table

| Task 2 feedback | Original mark | Improvement | Verification | Evidence | Status |
|---|---|---|---|---|---|
| Retain robust visual system | 8/10 | Existing identity preserved, visible branded feedback | Public layout and token/source review | Screenshots, journeys.css | Implemented; final human visual review pending |
| Add filter busy state | 8/10 | Six real filter/date-range groups and shared recovery | 12 isolated DOM assertions; real role filtering passed | site.js, browser results | COMPLETED, MANUAL CHECK PENDING |
| Human reader test missing | 13/15 | Complete role-specific NVDA/Narrator record prepared | Human execution not performed | Human checklist | PENDING HUMAN VERIFICATION |
| Rushed demo | 15/20 | Four-minute segment with fallback and handoffs | Notes reviewed; rehearsal not performed | EXPO document | Preparation complete; delivery pending |
| Better requirements elaboration | 2/5 | 33-ID register and feature/benefit inventory | Original wording unavailable; core role E2E passed | Traceability | PARTIAL — SOURCE REQUIRED |
| Silent walkthrough/no visual aids | 7/10 | Spoken code explanation, screenshot storyboard, real captures | Recording/deck/rehearsal still human work | EXPO document and captures | Preparation complete; delivery pending |


### Final local execution note

Final local verification on 28 October: 7/7 existing E2E tests passed after installing their required Chromium runtime. Existing seed settings were found in user secrets and used only in process memory. The normal API initialiser populated its configured local demo records; no migrations were applied. An additional Web-owned audit passed 80 populated page/width inspections and 10 interactions across Client, Staff and Admin, including real creation, reschedule/cancellation, cancel-dialog Escape/focus return, keyboard Requested/week navigation and admin POST filtering. A second 40-case public audit passed with real catalogue data; the first 40-case API-failure audit is retained separately. Commit-safe evidence: evidence/verification-summary.md (historical local E2E), private-browser-results.json and public-populated-browser-results.txt. No production resources were touched.

Additional live catalogue acceptance: keyboard category filtering returned only matching API-backed cards; actual Back and refresh restored usable controls (four checks PASS). Source test scripts were corrected for timing and empty-data assumptions during this pass; no application code was changed in response to a false harness failure. Human screen-reader testing and exact Part 1 wording remain pending.
