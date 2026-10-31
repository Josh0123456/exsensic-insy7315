# Expo notes: booking lifecycle and business rules

Speaker: Daniel (P2). These notes support the booking segments of the run sheet in [demo-script.md](../demo-script.md) (admin review, staff completion, and the rules behind reschedule, cancel and reject). They follow the same honesty rules: never change the clock, the database or a setting to make a step succeed, and say plainly when a fallback is used.

Related: [state-observer-tradeoffs.md](../state-observer-tradeoffs.md) (the design reasoning), [chapter-05-booking-logic-api.md](chapter-05-booking-logic-api.md) (the evidence), [CONTRACTS.md](../CONTRACTS.md) section 3 (the rules).

## 1. The 30-second version

"A booking has four statuses: Requested, Confirmed, Completed and Cancelled. Each status is a small class in the business layer that says what you may do next. Every time the status changes, the booking records one event, and two observers write the history row and the notifications, all in a single save. The database has the final say: a unique index means a slot can never hold two active bookings, even if two people click at the same moment."

## 2. Live transitions: what to say and what is happening

Use the references (A, B, C, D, E) from the run sheet. Say the left column out loud; the right columns are for questions.

| Run sheet | You say | Request underneath | The state decides | Observers and database |
|---|---|---|---|---|
| 04:50 Admin confirms **A** | "The list only offers staff who are qualified for this service and free at this time. The server checks again when I confirm." | `PUT /admin/bookings/{id}/confirm` with the staff member and the booking's version | `RequestedState.Confirm()` returns Confirmed | History row (Requested to Confirmed); client notified; staff member notified they are assigned. One save |
| 05:40 Client reloads **A** | "It is now Confirmed. The history shows who did what and when, and the notification was created by the same change." | `GET /bookings/{id}` | None (read only) | Shows the rows written above |
| 07:00 Staff opens **A** | "Staff see only their own assigned work, with the client's requirements." | `GET /staff/me/bookings` | None | Schedule lists Confirmed and Completed work for that staff member |
| 07:30 Staff completes **B** | "I'm using a different booking whose time has already started, because a future booking can't be completed early. That rule is not a button we hid; the server refuses it." | `PUT /staff/bookings/{id}/complete` | `ConfirmedState.Complete()` allows it only if now is not before the slot start | History row (Confirmed to Completed); client notified. Completed is final |
| 08:00 Client reschedules **C** | "Moving a confirmed booking sends it back to Requested, because an admin must check the staff member is free at the new time." | `PUT /bookings/{id}/reschedule` | `ConfirmedState.Reschedule()` returns Requested | History row; assigned staff notified; the old slot is free again |
| 09:00 Client cancels **D** | "A client can cancel a confirmed booking up to 24 hours before it starts. After that they must contact us; an admin can still cancel." | `PUT /bookings/{id}/cancel` | `Cancel()` returns Cancelled; the cut-off is checked first for clients | History row; slot freed; staff notified |
| 09:35 Admin rejects **E** (optional) | "There is no 'Rejected' status. A rejection is Cancelled with the reason recorded, and the client sees why." | `PUT /admin/bookings/{id}/reject` | `RequestedState.Reject()` returns Cancelled | Reason stored as `Rejected: ...`; client notified with the reason; slot free |

If a step is refused, read the message aloud and say what it proves, for example: "That's the server refusing a rule, which is the point of the demo." Do not retry in a way that hides it.

### If a live step fails

| Problem | Say | Do |
|---|---|---|
| Staff unavailable at confirm | "Someone else was assigned to that time first; the server checks again at confirm." | Pick another staff member the list offers |
| "Someone else changed this record" | "The page was out of date. Nothing was saved. That's the version check working." | Reload, review, repeat once |
| Cut-off blocks cancelling **D** | "This one is inside the 24-hour window, so the client is refused." (a real result worth showing) | Use another eligible booking for the success path; never move the clock |
| Staging slow or down | State it | Use the saved run or the evidence in chapter 5; do not claim it was live |

## 3. Questions on business rules

**Why are there only four statuses?** Fewer statuses mean fewer transitions to get wrong. A rejection and a cancellation both end the booking and free the slot, so a rejection is Cancelled with a reason that starts "Rejected:". Rescheduling is an action, not a status.

**Who can do what?** Clients book, reschedule and cancel their own bookings. Admins confirm, reject, and can also reschedule and cancel. Staff only complete bookings assigned to them. Admins can also complete.

**Why can't a client cancel inside 24 hours?** By then staff have prepared. The window is a setting (`BookingPolicy:ClientCancelCutoffHours`, default 24) and applies only to clients cancelling a Confirmed booking. A Requested booking can be cancelled at any time, and an admin can cancel at any time.

**Is there a cut-off for rescheduling?** No. The rule is only on cancelling. A moved booking goes back to Requested and needs approval again, which is the control. If the business wants a reschedule cut-off, it would be a new rule and a contract change.

**Why can't staff complete a booking early?** A service can't have been delivered before it starts. The state class refuses it, so it holds even if someone calls the API directly.

**Can two clients book the same slot?** No. The code checks first so users get a friendly message, but the real guard is a unique index in the database on the slot for Requested and Confirmed bookings. In testing, 20 clients booking one slot at the same moment produced one booking and 19 "slot unavailable" answers.

**What if an admin and a client change the same booking at the same time?** Every change sends the version the person last saw. If it is stale, the change is refused with a "changed by someone else" message. We tested a cancel and a confirm racing: exactly one wins and the history agrees with the final status.

**What happens to the slot when a booking is rejected, cancelled or completed?** Only Requested and Confirmed bookings hold a slot, so it is free again straight away.

**What happens after a confirmed booking is rescheduled?** It goes back to Requested and keeps its staff member as a proposal. The admin sees that person offered again and re-approves; the history shows the whole round trip. A proposal does not count as the person being busy, only a Confirmed booking does, so nobody can be assigned to two overlapping Confirmed bookings. (We found and fixed a bug here while checking the journey in the Web pages.)

**Why must the staff member be qualified and free?** Each service has the staff who can deliver it. The confirm list shows only qualified, free people, and the server checks again so two bookings can't send one person to two places.

**What times are used?** Slot dates and times are South African time (UTC+2). Audit timestamps are stored in UTC.

**How do notifications work?** They are in-app, written by an observer in the same save as the status change, so a change can't happen without its notification. Email is a stretch goal and not built. People aren't notified of their own actions.

**What does the client see if their booking is rejected?** A notification with the admin's reason, and the booking shows as Cancelled with that reason.

**Why does someone else's booking say "not found" instead of "forbidden"?** So nobody can tell whether a booking exists by guessing ids. A wrong role gets "forbidden".

**How is the API protected?** Role policies on every route, a per-booking access check, rate limits (sign-in 5 per minute, registration 3 per minute, booking creation 10 per 10 minutes), standard error responses with no stack traces, and security headers. Details are in chapter 5 and `security.md`.

**Is the API documented?** Yes. `docs/openapi.json` lists all 34 operations and is checked by tests against the real routes. It is a file in the repository, deliberately not an endpoint on the deployed API.

**How do you know it works?** Unit tests (110) cover the status rules, and integration tests (303 in total) run the real API against SQL Server for the whole journey, the races and the error contract. We also drove the real Web pages through the same journey on a development machine (chapter 5, section 5.10). CI runs them on every pull request. See chapter 5.

**What is not done or not proven?** Be direct: there is no load test; the Web pages were checked by script rather than in a browser, and the end-to-end browser tests run against staging and are separate; notifications are in-app only; rejections share the Cancelled status, so counting them means filtering on the reason. (Full list: chapter 5, section 5.9.)

**What would you do with more time?** A load test, email notifications through another observer (the pattern is built for it), and a reschedule cut-off if the business wants one.

## 4. Code evidence, only if asked

Show the code only when a marker asks "show me". One file at a time, narrated in one or two sentences. Zoom the editor in; do not scroll.

| If asked about | Open | Say |
|---|---|---|
| "Where are the rules?" | `src/Exsensic.Core/Bookings/States/ConfirmedState.cs` | "Each status is a class. This one allows reschedule, cancel and complete, and complete is refused before the slot starts. Anything not listed is refused automatically by the base class." |
| "Where do history and notifications come from?" | `src/Exsensic.Core/Bookings/Observers/BookingEventDispatcher.cs` | "The booking records what happened. This hands each event to every observer just before the save." |
| "How do you stop a double booking?" | `src/Exsensic.Data/Configurations/BookingConfiguration.cs` (the `UX_Bookings_ActiveSlot` index) | "A unique index on the slot, only for Requested and Confirmed. The database refuses a second one." |
| "How are conflicting edits handled?" | `src/Exsensic.Core/Bookings/BookingRules.cs` (`ChangeAsync`) | "Load, compare the version, apply the change, let the observers add rows, one save in one transaction." |
| "Prove it" | `tests/Exsensic.IntegrationTests/Bookings/ConcurrentBookingTests.cs` and `BookingTransitionEvidenceTests.cs` | "These send simultaneous requests through the real API and check there is exactly one winner." |

Running the tests live is optional and slow (they start a database). If you do, use a terminal prepared beforehand and say how long it takes; otherwise show the last green CI run for the commit being demonstrated, with its timestamp. Do not show a run you did not check.

## 5. Facts to have ready

| Fact | Value |
|---|---|
| Statuses | Requested, Confirmed, Completed, Cancelled (final: Completed, Cancelled) |
| Client cancel cut-off | 24 hours, Confirmed bookings only, clients only (a setting) |
| Rate limits | Sign-in 5/min per IP, registration 3/min per IP, booking creation 10 per 10 min per user |
| API size | 34 operations; 15 owned by P2 |
| Error codes | `validation_failed`, `unauthenticated`, `forbidden`, `not_found`, `slot_unavailable`, `invalid_transition`, `concurrency_conflict`, `staff_unavailable`, `slot_has_booking`, `duplicate`, `cancel_window_closed`, `rate_limited`, `server_error` |
| Tests (on `develop` after the P2 pull requests) | Unit 110 passed; integration 303 passed; build 0 warnings |
| Pull requests | #67 published API description, #68 lifecycle and race tests, #69 error-code fix |

Re-run the build and tests on the exact commit you present, and update the last row if the numbers changed.

## 6. Before you present

- [ ] Rehearse one full run on the exact deployed commit, with bookings A to E recorded privately.
- [ ] Check the test counts above against the latest green CI run.
- [ ] Have `ConfirmedState.cs`, `BookingConfiguration.cs`, `BookingRules.cs` and the two test files open in tabs, with the editor zoomed in.
- [ ] Hide passwords, tokens and personal data on every shared screen.
- [ ] Decide in advance who answers sign-in, security and deployment questions (Dean and Josh) and say so rather than guessing.
