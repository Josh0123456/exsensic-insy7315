# Human assistive-technology acceptance record

> **PR review status — 28 October 2026:** Earlier local build/test passes are historical execution evidence. A later forced, uncached restore FAILED with Pkcs 9.0.11 NU1902/NU1903; the referenced GitHub CI run remains failed. The October 5 staging E2E failure needs latest-deployment verification. These are external dependency/test-setup blockers, not proven Person 1 regressions. See [owner handover](ci-staging-handover.md) and [verification summary](evidence/verification-summary.md). Review-ready draft does not mean merge-ready or final requirements/accessibility sign-off.


Status: **PENDING HUMAN VERIFICATION**. Prepared 28 October 2026; no human NVDA or Narrator test was performed by this agent.

Tester: ______ Date/time: ______ Windows version: ______ Browser/version: ______ NVDA/Narrator version: ______ Environment/build: ______ Demo account labels (no passwords): ______

## Procedure

1. Use the authorised local/staging site with valid HTTPS, configured Client/Staff/Admin demo accounts and suitable real demo bookings. Do not change production data for this test.
2. Start NVDA. Use NVDA+N for settings and Speech Viewer to capture announcements without secrets. Ctrl stops speech, H/Shift+H moves headings, D landmarks, F form fields, Tab/Shift+Tab controls, Enter activates links/buttons, Space toggles radios/checkboxes; arrows select radio choices. NVDA+Space switches browse/focus modes when necessary. Keyboard layouts may differ; confirm keys in NVDA help.
3. Alternatively start Narrator with Win+Ctrl+Enter, enable scan mode as appropriate, and use Tab/Shift+Tab and Enter/Space. Record which reader was actually used. Do not mix results from different readers without labelling them.
4. Run each row with the reader enabled, record actual speech and focus behaviour, mark PASS/FAIL, and link a defect/fix. Password characters and tokens must not be captured.
5. Repeat failed rows after fixes; attach dated results. Automated DOM checks cannot replace these rows.

Expected speech below describes meaning, not exact synthesiser wording.

| ID / page | Action | Expected announcement | Expected keyboard behaviour | PASS/FAIL | Notes / actual speech | Defect/fix |
|---|---|---|---|---|---|---|
| C01 Home | Tab to skip link, Enter, navigate headings/landmarks | Skip to content; main landmark; one useful H1 | Skip moves focus to main; logical onward order | PENDING | | |
| C02 Services | Read cards and category links; activate category twice | Service names; descriptive details links; current category; one loading message | Enter follows link; repeat guarded; Back restores usable links | PENDING | | |
| C03 Service detail | Read description, duration/price; Book | Service heading and clear booking action | Navigate to actual selected service | PENDING | | |
| C04 Login | Read labels, submit empty/invalid fields, then valid demo credentials | Required email/password; linked errors; busy message; successful role destination | Summary receives focus; links move to fields; no trap | PENDING | | |
| C05 Registration | Review labels/password help, invalid email and mismatch | Help and validation associated with each relevant field | Correct errors without losing context; native/unobtrusive validation prevents pending lock | PENDING | | |
| C06 Booking Step 1 | Choose date and radio time, try continuing without selection | Date selected/unavailable; time group legend; required/timezone/error | Arrows select radio; only active date slots can submit; visible focus | PENDING | | |
| C07 Booking Step 2 | Read template fields/help; trigger required errors; submit valid brief | Field labels, required state, field and summary errors; one submitting message | Summary-to-field links work; single submit | PENDING | | |
| C08 Confirmation/detail | Read actual reference, status, assigned staff/history | Requested is a request; status words and dated history readable | Links have purpose; chronological history understandable | PENDING | | |
| C09 My Bookings | Change Upcoming/Requested/Confirmed/Past/Cancelled; paginate | Current filter, loading once, page number, booking references/status labels | Enter applies; Back restores controls; tab order follows cards | PENDING | | |
| C10 Reschedule | Choose real slot; submit; reload booking | Selected time, errors/conflicts if returned; new persisted Requested state when applicable | No hidden radios focusable; retry/reload actionable | PENDING | | |
| C11 Cancel | Review reason; open dialog; Escape; reopen and confirm eligible booking | Dialog name, explanation, Keep booking and confirmation controls | Focus starts on cancel; Tab stays in dialog; Escape closes; focus returns; confirmed state read from API | PENDING | | |
| C12 Profile/logout | Edit profile; save; sign out | Labels, readonly email, saving/success; signed-out navigation | Focus and errors usable; Sign out works by keyboard | PENDING | | |
| S01 Staff schedule | Change week; read assigned work | Date range; one loading message; dated booking headings | Previous/This/Next week usable; Back resets guard | PENDING | | |
| S02 Staff detail | Read client brief/requirements | Booking/service/status and all requirement labels/values | Logical heading/order; no inaccessible content | PENDING | | |
| S03 Completion | Open confirmation for eligible started booking; cancel then complete | Dialog and API result; completed state after reload | Escape, focus trap/return; double submit guarded | PENDING | | |
| A01 Admin dashboard | Read counts and requested queue | Metric names/values and booking links | Logical landmarks/headings; links focusable | PENDING | | |
| A02 Admin filters | Set status and dates; invalid range; valid Apply filters; rapid repeat; Back | Labels/errors, Applying filters once; results versus failure distinct | Selections retained; errors actionable; restored page unlocked | PENDING | | |
| A03 Admin results | Read table on desktop and cards on phone; paginate | Column/row context; reference, client, service, date, status and staff | Only displayed representation in tab order; pager labelled | PENDING | | |
| A04 Admin review/confirm | Read brief; choose API-returned staff; confirm | Staff label/options; confirmation; real Confirmed status | No client-side qualification calculations; focus returns on cancel | PENDING | | |
| A05 Admin reject | Enter reason, open dialog; cancel then reject authorised demo booking | Required reason, confirmation; Cancelled and rejection reason | Keyboard works; never announces a Rejected status | PENDING | | |
| X01 Error/access/404 | Read page and safe return links | Clear problem/title and useful recovery; no internal exception | Recovery links reachable; focus visible | PENDING | | |
| X02 Failure/empty | Rehearse approved API failure separately from successful zero results | Failure is an alert; genuine empty result has distinct wording | Reload usable; no false success | PENDING | | |

Notifications are P2-owned: coordinate a separate human read/mark-read check; do not change that UI here. Dean's management screens need owner acceptance separately.

Acceptance requires all relevant rows PASS or a documented accepted limitation with owner/date. Record actual device/zoom, 200% zoom, reduced motion and phone width as additional notes. This document alone does not close the lecturer's human-test feedback.
