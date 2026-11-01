# Person 1 Task 3 — feedback improvement and evidence log

> **PR review status — 28 October 2026:** Earlier local build/test passes are historical execution evidence. A later forced, uncached restore FAILED with Pkcs 9.0.11 NU1902/NU1903; the referenced GitHub CI run remains failed. The October 5 staging E2E failure needs latest-deployment verification. These are external dependency/test-setup blockers, not proven Person 1 regressions. See [owner handover](ci-staging-handover.md) and [verification summary](evidence/verification-summary.md). Review-ready draft does not mean merge-ready or final requirements/accessibility sign-off.


Date: 28 October 2026 (SAST). Christopher Doyle, ST10445478.
Baseline: `cd70da1`, clean `feat/p1-task3-feedback-improvements`. Local Windows, .NET SDK 10.0.401, Node 26.8.1, installed Chrome via Playwright CLI 0.1.22.

## Feedback and response

The following is the feedback supplied by Christopher, not independently retrieved from a marking portal. Presentation results include group delivery and are not solely Christopher's individual grade.

| Issue ID | Exact feedback and mark | Before / file or screen | Change made | Evidence | Result | Client benefit |
|---|---|---|---|---|---|---|
| P1-01 | Visual Design & Branding 8/10: “Good dark theme tokens with a lime brand colour, rem scales and status colours always paired with labels. Little to change; a robust, accessible design system.” | Existing tokens and dark layouts | Retained identity; added token-based visible action feedback without animation | `wwwroot/css/journeys.css`, `_Layout.cshtml` | Implemented; browser evidence recorded separately | Familiar design, clear progress |
| P1-02 | UX & Feedback 8/10: “Nice work on field validation messages, toasts, status labels and busy states on single-submit forms. Only small polish left: give the filter forms the same busy state.” | Admin booking filter form had no busy hook; three link-filter groups had none | Added form hook, descriptive busy labels, repeat guards, visible/polite feedback, history reset and 90-second recovery | `site.js`, four views, isolated browser regression | See evidence results; real authenticated filtering passed in additional audit | Reduces repeated requests and uncertainty |
| P1-03 | Responsiveness & Accessibility 13/15: “Well-built accessibility: a skip link, visible focus, reduced motion, ARIA and label attributes and Lighthouse checks. Still to do is the human screen-reader pass.” | Existing safeguards present; human test absent | Preserved safeguards; prepared role-specific human test record | `accessibility-manual-test-checklist.md`, `responsive-test-matrix.md` | PENDING HUMAN VERIFICATION | Practical assistive-technology acceptance process |
| P1-04 | Technical Demo & Implementation 15/20: “Slightly rushed.” | Limited pacing/evidence | Timed individual EXPO section and capture plan | `expo-speaking-evidence.md` | Notes prepared; rehearsal pending | Clearer explanation of the actual journey |
| P1-05 | Requirements Alignment 2/5: “Better elaboration was needed.” | No original Part 1 list in checkout; only FR12/25 citations | All 33 IDs recorded with explicit source gaps, separate verified feature inventory and business benefits | `requirements-traceability.md` | PARTIAL: original wording required | Prevents unsupported compliance claims |
| P1-06 | Communication & Professionalism 7/10: “The code walkthrough was not voiced over, and there were no supporting visual aids; the repo .md files were mainly used, so it did not feel like a presentation.” | Repository notes used as presentation | Speaking script, visual storyboard and spoken code explanation | `expo-speaking-evidence.md` | Prepared; actual delivery pending | Audience can understand problem, change and benefit |

## Inventory and ownership

Reviewed Person 1 controllers Account, Services, Book, Bookings, Staff, Admin, Home and JourneyController; all their corresponding views, presentation models, journey adapters and typed clients; shared navigation, layout, validation, cards, pager, slot picker, dialogs, badges/toasts; tokens, site/journey styles and site JavaScript; Program.cs, contracts, accessibility, demo, README and tests. API clients and adapters are registered against real Contracts; Web does not generate booking state. Existing error branches remain distinct from empty successful results.

Filters inspected: My Bookings Upcoming/Requested/Confirmed/Past/Cancelled links; Services All/category links; Staff Previous/This/Next week links; Admin status/from/to POST filter and Clear filters link. All four groups plus booking/reschedule availability-range links improved; Clear filters remains an ordinary navigation escape. No new filters invented. Notifications and Admin Services/Time Slots/Users were not modified.

## Operation and recovery

Admin filter uses its existing POST/antiforgery/redirect-to-GET route. Selected fields stay enabled and remain in the submitted payload. Its submit buttons stay focusable, with aria-disabled backed by the existing submit guard. Link filters retain real hrefs, native keyboard Enter activation and modified-click behaviour. A group is guarded only while navigating. One action-status region is visible and polite; there is no spinner or duplicate announcement. pageshow resets locks and labels, including bfcache restoration; pagehide cancels recovery timers. If navigation stalls, a 90-second timeout restores the filter and explains retry. This never retries a request automatically and never claims the API succeeded. A new page naturally replaces this transient state.

## Verification ledger

- Earlier local build: `dotnet build Exsensic.sln`, PASS (latest clean restore now fails), 0 warnings / 0 errors.
- Fresh solution test run: 110 unit PASS, 104 integration PASS; 0 failures / 0 skips in those suites.
- E2E final run: 7 passed, 0 failed/skipped. Initial solution run disabled E2E; first configured attempt failed 7/7 at startup because Chromium was missing. Runtime installed; rerun passed.
- JavaScript syntax and git diff --check: PASS. Git reports LF→CRLF normalisation advisories, not whitespace failures.
- Browser results: see `evidence/` and responsive matrix. Isolated DOM checks exercise actual site.js; additional real authenticated checks are summarised in evidence/verification-summary.md; raw role results are local-only.
- Sandboxed Git/.NET/Chrome initially failed. Repository access fixed Git; approved unsandboxed execution was needed for Windows SDK/services/browser. No Git configuration or branch was changed.

Final local verification on 28 October: 7/7 existing E2E tests passed after installing their required Chromium runtime. Existing seed settings were found in user secrets and used only in process memory. The normal API initialiser populated its configured local demo records; no migrations were applied. An additional Web-owned audit passed 80 populated page/width inspections and 10 interactions across Client, Staff and Admin, including real creation, reschedule/cancellation, cancel-dialog Escape/focus return, keyboard Requested/week navigation and admin POST filtering. A second 40-case public audit passed with real catalogue data; the first 40-case API-failure audit is retained separately. Commit-safe evidence: evidence/verification-summary.md (historical local E2E), private-browser-results.json and public-populated-browser-results.txt. No production resources were touched.

## Remaining dependencies

Christopher: obtain approved Part 1 FR wording; perform NVDA/Narrator checks; review screenshots and rehearse. Local accounts/data were found and used successfully; team must repeat final staging acceptance. Josh: deployment/CI acceptance and final website/help URLs. Daniel: notification human acceptance and remaining edge cases. Dean: admin catalogue/users/time-slot acceptance. The reproduced dependency restore failure and historical staging fixture failure are handed over separately; no backend changes were made.

## Completion checklist

- [x] Correct branch and clean baseline verified
- [x] Actual supplied feedback reviewed; branding preserved
- [x] All existing Person 1 filter groups improved
- [x] Error/empty branches retained separately (source review)
- [x] Human screen-reader checklist prepared
- [ ] Human NVDA/Narrator pass — PENDING HUMAN VERIFICATION
- [x] Populated client/staff/admin automated checks and keyboard filter interactions (not full keyboard-only role walkthroughs)
- [ ] Original FR01–FR33 wording reconciled with Part 1
- [x] Business benefits and Part 1 design changes documented
- [x] Client guide, EXPO notes and final-report draft prepared
- [x] Fresh build, unit and integration tests pass
- [x] E2E execution limitation recorded
- [x] No fake production data, secrets or backend changes introduced
- [ ] Commit/PR evidence — Christopher supplies after manual Git review

See responsive matrix for width-specific verification; no historical screenshot or Task 2 result is presented as fresh Task 3 evidence.

## Final acceptance checklist (Task 3)

- [x] Correct feature branch and safe starting tree
- [x] Reviewed supplied actual Task 2 feedback
- [x] Filter busy states implemented for all six Person 1 navigation/form groups
- [x] Errors and empty-result branches remain distinct
- [x] Existing branding preserved
- [x] Populated client UX smoke test
- [x] Populated staff UX smoke test
- [x] Populated admin booking UX smoke test
- [x] Public Home keyboard skip/menu checks
- [x] Automated public structural accessibility checks
- [ ] Human NVDA/Narrator check — PENDING HUMAN VERIFICATION
- [x] 360px public/error and selected populated scope verified
- [x] 390px public/error and selected populated scope verified
- [x] 768px public/error and selected populated scope verified
- [x] 1024px public/error and selected populated scope verified
- [x] 1280px public/error and selected populated scope verified
- [ ] Original requirements wording mapped and accepted — original document missing
- [x] Implemented feature business benefits explained
- [x] Available Part 1 design differences documented
- [x] Feedback improvements recorded
- [x] Client guide draft created
- [x] EXPO notes and capture plan prepared
- [ ] Current clean restore/build — blocked by external vulnerable dependency
- [x] Unit tests pass
- [x] Integration tests pass
- [x] E2E non-execution accurately reported
- [x] Changed-file review found no added credentials/secrets; no Git commit made
- [x] No fake backend/user data introduced
- [x] Changed paths remain within authorised ownership
- [x] Final report section drafted with explicit limits
- [x] Actual code changes ready for human review
- [ ] Commit/PR details — Christopher's manual step
