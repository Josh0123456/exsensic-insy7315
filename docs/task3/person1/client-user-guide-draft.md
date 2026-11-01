# Client user guide — Task 3 draft

> **PR review status — 28 October 2026:** Earlier local build/test passes are historical execution evidence. A later forced, uncached restore FAILED with Pkcs 9.0.11 NU1902/NU1903; the referenced GitHub CI run remains failed. The October 5 staging E2E failure needs latest-deployment verification. These are external dependency/test-setup blockers, not proven Person 1 regressions. See [owner handover](ci-staging-handover.md) and [verification summary](evidence/verification-summary.md). Review-ready draft does not mean merge-ready or final requirements/accessibility sign-off.


Exsensic Booking and Service Management System. Client section by Christopher Doyle. Version 28 October 2026. This describes the current routes/control labels from source; core sign-in, booking, tracking, reschedule and cancellation procedures passed against local real demo data; final human guide acceptance remains required.

## Access, contents and help

Use the website address supplied by Exsensic: **[TEAM-VERIFIED WEBSITE URL]**. Use a current supported browser, internet connection and JavaScript for enhanced validation/dialogs. A working website/API and valid account are required. Do not use the localhost test address as a public link. Josh supplies final access/setup details; Exsensic supplies **[APPROVED SUPPORT CONTACT]** before publication.

Contents: account (§1–4), services (§5–6), booking (§7–10), tracking (§11–13), changes (§14–15), notifications/profile (§16–17), problems/help (§18–19). Times are shown in SAST. A request is not a confirmed appointment.

Four public/catalogue screenshots are supplied where referenced; other screenshot entries are capture instructions, not existing images. Capture only the final accepted demo version, redact personal information, and do not show passwords, tokens or private client briefs. Each capture should have numbered highlights matching the procedure and a caption. Existing Task 2 screenshots must not be relabelled as current.

## 1. Open the website

Purpose: reach Exsensic safely. Navigate: [TEAM-VERIFIED WEBSITE URL].
1. Open your browser and enter the address supplied by Exsensic.
2. Check the Exsensic name and HTTPS address.
3. Select Services, Sign in or Register in navigation; on phones open Menu first.
Expected: branded Home and useful navigation. If unavailable, check your connection and the approved address; contact support if the problem persists.
Screenshot G01: current Home at phone width; highlight Menu and Services. Fresh unannotated reference: `evidence/home-390.png` (add annotations before guide publication).

## 2. Register a client account

Purpose: create your own account. Navigate: Register → /Account/Register.
1. Enter Full name, Company name, Email address and Phone number.
2. Read the displayed password guidance; enter Password and Confirm password identically.
3. Select Create client account once.
4. Correct any highlighted fields and resubmit if validation asks you to.
Expected: a successful registration starts your client session. An error is not successful registration. Do not repeatedly register if a response is uncertain; try Sign in or ask support.
Screenshot G02: label the registration fields and button. Fresh unannotated reference: `evidence/register-1280.png`.

## 3. Sign in

Purpose: access your bookings. Navigate: Sign in → /Account/Login.
1. Enter the email and password registered for your account.
2. Select Sign in once and wait for feedback.
3. If sign-in fails, correct your details or follow the error guidance.
Expected: your client workspace is available; My bookings appears. For an expired session, sign in again. Avoid sharing credentials.
Capture G03: blank sign-in form with labels; no password or personal email.

## 4. Sign out

Purpose: end your session, especially on a shared computer. Navigate: main navigation.
1. Open Menu on a phone if needed.
2. Select Sign out.
3. Check Sign in/Register are shown again.
Expected: session ended; protected pages require sign-in again. Capture G04: signed-out navigation.

## 5. Browse services

Purpose: compare available agency services. Navigate: Services → /Services.
1. Read each service card.
2. Select All services or a category to narrow the catalogue.
3. Wait for the selected results; use another category if a successful result is empty.
Expected: real available services from Exsensic. An API failure has separate feedback and Reload current page; it is not an empty catalogue.
Capture G05: populated catalogue captures are evidence/services-390.png and evidence/services-1280.png; annotate category and details links.

## 6. Open service details

Purpose: understand an offering before booking. Navigate: a service's details link → /Services/{id}.
1. Open the relevant service card link.
2. Read the description, duration and displayed pricing information.
3. Decide whether this service suits your needs.
Expected: details for the selected service. Capture G06: one approved demo service and its booking control.

## 7. Start a booking

Purpose: request the chosen service. Navigate: service detail → Book this service → /Book/{serviceId}.
1. Select Book this service.
2. Sign in if prompted.
3. Check the service name before choosing a time.
Expected: Step 1 of 2. Capture G07: service identity and step indicator.

## 8. Choose an available time

Purpose: select an appointment returned by the system. Navigate: Step 1, time slot.
1. Read the timezone notice (SAST).
2. Choose an available date.
3. Select a time radio option for that date.
4. Select Continue to requirements.
Expected: Step 2 displays the selected service/time. If unavailable, choose another returned slot; a slot is not reserved merely by viewing it.
Capture G08: available date, selected time, timezone and Continue to requirements.

## 9. Enter service requirements

Purpose: give Exsensic your project brief. Navigate: Step 2 → /Book/{serviceId}/Details.
1. Check service, slot and contact summary.
2. Complete the fields shown for this particular service.
3. Follow required-field, numeric and other displayed guidance.
4. Use Change time if the appointment is wrong; Edit profile if contact details need updating.
Expected: fields reflect the selected service; error summaries link to fields needing correction.
Capture G09: approved non-sensitive brief, field help and validation example.

## 10. Submit the booking request

Purpose: save a request for review. Navigate: Step 2.
1. Review the brief and time.
2. Select Submit booking request once; wait while the request is processed.
3. Read the confirmation and keep the booking reference.
4. Select View booking to inspect the saved record.
Expected: real saved booking, normally Requested. If a slot conflict occurs, select a different time. If the outcome is uncertain, check My bookings before submitting again.
Capture G10: confirmation and reference from an approved demo record.

## 11. Understand booking status

Purpose: know what has happened. Navigate: booking confirmation/detail or My bookings.
1. Read the written status badge.
2. Requested means submitted for review; Confirmed means approved and assigned.
3. Completed means the work was marked complete. Cancelled includes cancelled/rejected requests; read the recorded reason.
Expected: four statuses only. Upcoming/Past are viewing filters, not additional statuses. Capture G11: text badges and an approved history example.

## 12. Find My bookings

Purpose: locate your requests. Navigate: My bookings → /Bookings.
1. Select Upcoming, Requested, Confirmed, Past or Cancelled as needed.
2. Wait for the loading message and new results.
3. Use Previous/Next when more pages are available.
4. Open the relevant booking card.
Expected: bookings belonging to your account. Use Back to return; filter controls remain usable. Capture G12: populated cards, selected filter and pager.

## 13. Read booking detail/history

Purpose: check reference, requirements and progress. Navigate: /Bookings/{id}.
1. Read the service, appointment, written status and assigned staff when shown.
2. Review your requirements and dated history.
3. Use the available actions, or Back to my bookings.
Expected: actual persisted details. Not Found may mean the record is unavailable to this account; do not try another person's booking reference.
Capture G13: redacted detail/history with numbered callouts.

## 14. Reschedule

Purpose: request a different eligible appointment. Navigate: booking detail → Reschedule booking.
1. Open an eligible booking and select Reschedule booking.
2. Choose a newly available date/time.
3. Select Request new time and wait.
4. Read the refreshed booking and history.
Expected: new persisted time; applicable bookings return to Requested for approval. If the slot was taken or the booking changed, reload and choose again. If the action is absent, the booking is not currently offered for rescheduling.
Capture G14: selected replacement slot, then actual updated history.

## 15. Cancel

Purpose: cancel an eligible request. Navigate: booking detail → Review cancellation.
1. Read the booking and displayed cancellation policy.
2. Enter the requested reason.
3. Select Cancel this booking.
4. In the dialog choose Keep booking to stop, or Yes, cancel booking to send the request.
5. Check the refreshed booking for Cancelled and the reason.
Expected: cancellation only if accepted by the API. A closed cancellation window or changed booking is a failure, not a cancellation. Escape closes the dialog without submitting.
Capture G15: cancellation policy/dialog, then confirmed persisted outcome.

## 16. View notifications

Purpose: see in-app booking updates. Navigate: Notifications → /Notifications.
1. Open Notifications from signed-in navigation.
2. Read the listed update and follow its booking link if available.
3. Use the page's mark-read control where offered.
Expected: real in-app updates. Email delivery is not promised. This screen is maintained by Daniel; confirm final labels and annotate an accepted demo capture with him before publication.
Capture G16: non-sensitive notification and read indicator.

## 17. Update profile

Purpose: keep your contact details accurate. Navigate: Profile → /Account/Profile.
1. Edit Full name, Company name or Phone number as necessary.
2. Email address is read-only on this screen.
3. Select Save profile once.
4. Correct field errors or check the success feedback.
Expected: saved profile returned by the API. Capture G17: blank/redacted profile and save feedback.

## 18. Handle common problems

Purpose: recover without duplicate requests. Navigate: the current feedback/error page.
1. Field error: follow its summary link, correct the value and submit again.
2. Session expired: sign in again; reopen your booking.
3. Access Denied/Not Found: return to your own workspace or contact support.
4. Slot/staff conflict or outdated booking: reload and review current state before retrying.
5. Too many requests: wait the displayed retry period.
6. Service failure: wait, use Reload current page, and check My bookings before repeating creation.
Expected: actionable next step without exposed technical errors. Capture G18: safe validation and generic failure examples, separately from empty results.

## 19. Get help and complete training

Purpose: request assistance. Navigate: [APPROVED SUPPORT CONTACT].
1. Note the screen name, time and a short description of the problem.
2. Share only your own booking reference if requested through an approved support channel.
3. Never send your password, token or another client's information.
Expected: Exsensic can investigate. During handover, practise registration/sign-in, browsing, one approved demo request, status tracking, rescheduling and cancellation under supervision. Verify the help contact and final URL before publishing this guide.
Capture G19: approved help/contact information once supplied.
