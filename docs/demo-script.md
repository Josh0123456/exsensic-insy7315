# Exsensic live demo and requirements alignment — Person 1

Prepared by role: Person 1 frontend/UX/demo coordination. Source baseline: `develop` at `c76e25a`, inspected 4 October 2026. Duration: about **14 minutes**, with optional follow-up questions.

## Evidence and honesty rules

The merged system has real authentication/profile, service catalogue, availability, requirement templates, booking create/read/change, staff completion, admin booking review and notifications. The frontend calls these APIs and reloads persisted state after successful changes.

**Admin Services, Admin Time Slots and Admin Users are not implemented in this checkout.** Their Contracts exist and `docs/CONTRACTS.md` describes their routes, but no matching controllers/screens were found. The management segment below is conditional and must not be presented as delivered before Dean's work merges and is tested.

The original Part 1 requirement list and `Exsensic_Part2_Team_Plan.pdf` are not in this repository. Only **FR12** (four-state booking lifecycle/rejection decision) and **FR25** (staff qualification assignment decision) are explicitly cited in `docs/CONTRACTS.md` §12. Other numbers are **unverified**, not invented. Christopher should obtain the approved Part 1 list and reconcile its numbering before submission; the functional alignment below remains usable meanwhile.

## Rehearsal setup — at least 30 minutes before presenting

- Christopher coordinates the flow and drives client UX/accessibility. Daniel explains booking rules and drives admin/staff booking actions. Dean owns catalogue/time-slot/user management and security/data explanations. Josh owns deployment/CI/Azure.
- Use three separate browser profiles labelled Client, Staff and Admin. Sign in through the real form with `[CLIENT DEMO ACCOUNT]`, `[STAFF DEMO ACCOUNT]`, `[ADMIN DEMO ACCOUNT]`. Do not put passwords or JWTs on slides, in this document, browser screenshots, URLs or logs.
- Use the team's agreed demo environment. The README names staging; verify it is healthy before presenting. For local use, HTTPS trust is now complete; configure API user-secrets as documented in README. This PC currently lacks seed/account settings and the real catalogue is empty, so it is not yet a ready demo environment.
- Record real booking references privately for: **A**, created live in a future available slot; **B**, already Confirmed, assigned to the staff demo account and already started; **C**, eligible for reschedule; **D**, eligible for cancellation (if Confirmed, outside the configured client cutoff); optional **E**, a Requested booking for rejection. These are rehearsal labels, never invented database records or fixed IDs.
- Choose B from real seeded/rehearsal data created through the team's supported workflow. A future booking created live cannot be completed immediately. Never change the machine clock, database status or API policy to make the demonstration succeed.
- Keep one known-good alternate available slot and record any existing Requests needed for fallback. Ensure the staff account is qualified and free for the service. Only select staff supplied by the API.
- Rehearse one full run on the exact deployed commit. Allow startup/database warm-up. Space sign-ins at least 13 seconds apart; if rate limited, obey Retry-After. Never hammer Retry.
- Prepare a clearly labelled recording of a prior successful run if available. The repository currently has no demo video/deck link; do not claim one exists. If no recording exists, the fallback is to explain the limitation and show already persisted data or source/tests.
- Hide passwords, personal information, secrets and the cloud portal's sensitive settings before screen sharing. Use only approved demo data. Do not make unrelated live changes.

## Minute-by-minute run sheet

Each segment's fallback must be announced honestly. Existing data may demonstrate a later state, but it must not be described as the result of a failed live action.

| Time | Driver / role / browser | Screen and action | Short talking point | FR evidence | If the live action fails |
|---|---|---|---|---|---|
| 00:00–00:35 | Christopher / guest / Client profile | Home; show black/charcoal/lime identity, then Sign in | “Exsensic brings creative services and booking progress into one consistent workspace.” | UX/design; exact FR number unverified | Show already loaded Home; explain a startup delay |
| 00:35–01:15 | Christopher / Client | Login with approved account. Registration is an optional pre-agreed alternative, not a compulsory duplicate account | “The real API authenticates the client; the website keeps the session in a protected cookie.” | Account/auth group; number unverified | Use an existing approved signed-in Client profile; disclose that login was not completed live |
| 01:15–02:00 | Christopher / Client | Services; filter a category, open a real service | “Services, descriptions and durations come from the catalogue API.” | Catalogue group; number unverified | Clear filters; if API fails, show the friendly failure and retry once when appropriate |
| 02:00–02:45 | Christopher / Client | Book Step 1; select an available date and radio slot | “The first booking step asks for time. Availability is checked by the backend.” | Availability/booking group; number unverified | Choose an alternate API-returned slot; if none exist, show a rehearsed existing request and state the limitation |
| 02:45–03:35 | Christopher / Client | Book Step 2; show template labels, briefly trigger one required-field error, follow summary link, complete requirements | “The second step adapts the requirements to the selected service. Errors lead you back to the field.” | Requirements + accessibility; number unverified | Correct the form; never invent a successful submission or change backend validation |
| 03:35–04:10 | Christopher / Client | Submit once; confirmation; record A's real reference; open My Bookings/detail | “This Requested booking has been saved. Requested is not a confirmed appointment.” | FR12 lifecycle; create/read group number otherwise unverified | For slot conflict choose another returned slot; for uncertain response check My Bookings before retrying |
| 04:10–04:50 | Daniel / Admin / Admin profile | Sign in if needed; dashboard; requested queue | “Management sees the real approval queue and current booking counts.” | Admin overview; number unverified | Open an existing Requested booking; disclose use of rehearsal data |
| 04:50–05:40 | Daniel / Admin | Review A; read requirements; select an API-returned qualified/free staff member; confirm dialog and submit | “The API supplies eligible staff and checks availability again when we confirm.” | **FR25** qualification; **FR12** Requested → Confirmed | If staff unavailable choose another returned option; if stale, explicitly reload before reviewing and submitting again |
| 05:40–06:15 | Christopher / Client | Refresh A detail; show Confirmed, assigned staff and history; Notifications | “A new read shows the confirmed state and the real notification created by the booking change.” | FR12; notifications number unverified | Reload once; if notification fails, show persisted booking history and report the notification issue |
| 06:15–07:00 | Daniel / Staff / Staff profile | Sign in; weekly schedule; open assigned A | “Staff see assigned work and the client's actual requirements.” | Staff schedule/detail; number unverified | Navigate to correct week; use another real assigned booking and disclose the change |
| 07:00–07:50 | Daniel / Staff | Open rehearsed B whose slot has started; Mark as completed; demonstrate modal; confirm; reload detail | “A future booking cannot be completed early. This separate started booking demonstrates the permitted transition.” | **FR12**, Confirmed → Completed | If not permitted, show the real rejection or an existing Completed booking; do not bypass the rule |
| 07:50–08:45 | Christopher / Client | Open separate C; Reschedule; choose a new available time; submit; refreshed detail/history | “The API rechecks the slot and version. Rescheduling a Confirmed booking returns it to Requested for approval.” | **FR12**; reschedule number otherwise unverified | On conflict reload/select another real slot; show unchanged real state if unsuccessful |
| 08:45–09:35 | Christopher / Client | Open separate D; review cancellation policy, reason and confirmation; submit; show Cancelled | “Cancellation has a clear confirmation and an API-enforced cutoff.” | **FR12**; cancellation number otherwise unverified | If cutoff closed, show friendly feedback and use an eligible rehearsal booking; never claim a rejected request succeeded |
| 09:35–10:05 | Daniel / Admin, optional | Reject E with a reason; show Cancelled and recorded rejection reason | “There is no fifth Rejected status: rejection is Cancelled with its reason.” | **FR12** | Skip if no eligible Requested booking; explain using an existing persisted rejected/cancelled example |
| 10:05–11:00 | Dean / Admin | **Conditional:** Admin Services and Admin Time Slots; show real catalogue update/availability management only after those screens merge | “Catalogue and slot administration are owned by the data/security workstream.” | Management group; exact numbers unverified | **Current checkout: state that these screens are missing.** Show the contract/known limitation, not a fabricated UI. No direct database edits |
| 11:00–12:00 | Christopher / Client | Show responsive layouts at phone and tablet width, cards, filters and readable controls | “The same booking journey fits a phone without losing labels or actions.” | Responsive UX; number unverified | Show verified public-page mobile layout; do not claim untested authenticated layouts passed |
| 12:00–12:50 | Christopher / guest or Client | Keyboard skip link, menu, visible focus, labelled fields/error link; explain modal Escape/focus and textual statuses | “Users can find the main content, follow field errors and understand status without relying on color.” | Accessibility; number unverified | Demonstrate verified skip/menu behavior; disclose pending NVDA and authenticated keyboard checks |
| 12:50–13:35 | Josh / deployment presenter | Brief handoff to actual GitHub commit/green CI and Azure deployment evidence | “Josh will show how this exact version is tested and hosted.” | Deployment evidence; number unverified | Show a previously saved genuine run with timestamp/commit; do not report current CI/cloud state without checking it |
| 13:35–14:00 | Christopher / coordinator | Close on real booking progress and the requirements table below | “We have traced the client experience through real requests, approvals and delivery, with the remaining dependencies clearly recorded.” | Alignment | Name any segments that could not run and the evidence that remains pending |

## Requirements alignment

“Number unverified” is an explicit gap in available documentation. Replace it only after checking the approved Part 1 source; do not assign convenient sequential FR numbers.

| FR group | Requirement summary | Screen/demo action | Evidence shown | Presenter |
|---|---|---|---|---|
| Account/auth — number unverified | Register/sign in, session, profile | Login/Register/Profile | Real API response and protected role workspace; profile reload | Christopher; Dean for backend security |
| Catalogue — number unverified | Browse active services and detail | Services/category/detail | Real service description, duration and price where supplied | Christopher |
| Availability — number unverified | Select a valid service slot | Book Step 1 | API-returned dates/times; unavailable-slot feedback if encountered | Christopher; Daniel for rules |
| Requirements/create — number unverified | Collect category-specific requirements and persist a request | Book Step 2/confirmation | Real template, linked validation, actual reference and subsequent GET | Christopher |
| Booking read/history — number unverified | View own booking progress and history | My Bookings/detail | Real paging/history; Upcoming/Past are UI filters, not statuses | Christopher |
| **FR12**, as cited in CONTRACTS §12 decision 2 | Four booking statuses and legal lifecycle | Requested → Confirmed; separate started booking → Completed; reschedule/cancel/reject | Actual status badges/history; rejection is Cancelled plus reason | Christopher and Daniel |
| **FR25**, as cited in CONTRACTS §12 decision 7 | Assign qualified staff | Admin booking review | Only API-returned qualified/free staff options; confirmed assignment | Daniel; Dean for data model |
| Admin overview/review — number unverified beyond FR12/25 | Review requests, confirm/reject | Dashboard/bookings/review | Counts, queue, requirements and persisted action result | Daniel |
| Staff work — number unverified beyond lifecycle FR12 | View assigned schedule and complete allowed work | Staff schedule/detail/B | Assigned booking and real completed state after permitted start | Daniel |
| Notifications — number unverified | Inform users of booking changes | Notifications/badge | Real stored notification and read state | Daniel with Christopher's client handoff |
| Catalogue/slot/user management — number unverified | Maintain services, slots and users | P3 Admin screens | **Absent in current checkout; no runtime evidence** | Dean |
| Accessibility — exact FR/NFR identifier unverified | Labels, keyboard, contrast, focus and error support | Skip/menu/forms/dialogs/statuses | Dated `accessibility.md`, Lighthouse evidence and remaining human checks | Christopher |
| Responsive/consistent UX — identifier unverified | Usable phone/tablet/desktop workspace | Width changes and mobile cards | Five-width matrix; separate source versus runtime evidence | Christopher |
| Security — identifier unverified | Secure sessions, authorization, antiforgery, safe failures | Role access, sign-out, validation | Web implementation and passing tests; avoid exposing secrets | Dean; Christopher for visible UX |
| Deployment/quality — identifier unverified | Tested, hosted system with traceable version | GitHub/CI/Azure handoff | Actual commit and pipeline/deployment evidence supplied by owner | Josh |

## Completion checklist

- [ ] Configure and rehearse approved real Client/Staff/Admin accounts in the intended environment.
- [ ] Confirm every successful live mutation through a subsequent API-backed page reload.
- [ ] Rehearse error, conflict, cancellation cutoff and uncertain-submit recovery.
- [ ] Complete authenticated keyboard/responsive checks and NVDA/Narrator spot-checks from `accessibility.md`.
- [ ] Dean confirms whether management screens are merged; otherwise keep the management segment marked unavailable.
- [ ] Obtain Part 1 requirements and confirm exact FR/NFR numbers; only FR12/FR25 have repository evidence today.
- [ ] Josh supplies verified deployment/CI references; team supplies an actual backup video/deck if available.
- [ ] Christopher records which segments passed, which used declared fallbacks and which remain unverified.

The script is ready to rehearse. Full demonstration and requirements-numbering acceptance remain dependent on the real environment, missing teammate screens and original requirements source.
