# Person 1 EXPO speaking and visual evidence

> **PR review status — 28 October 2026:** Earlier local build/test passes are historical execution evidence. A later forced, uncached restore FAILED with Pkcs 9.0.11 NU1902/NU1903; the referenced GitHub CI run remains failed. The October 5 staging E2E failure needs latest-deployment verification. These are external dependency/test-setup blockers, not proven Person 1 regressions. See [owner handover](ci-staging-handover.md) and [verification summary](evidence/verification-summary.md). Review-ready draft does not mean merge-ready or final requirements/accessibility sign-off.


Prepared 28 October 2026. This is a rehearsal script and visual specification, not evidence that a presentation was delivered. Arrange final FR numbering from the approved Part 1 source before using labels on slides. Use verified captures only; no passwords/personal booking briefs. Do not project Markdown files as the presentation.

## Four-minute individual segment

| Time | Driver / screen | Short spoken explanation | Visual and evidence | Fallback / transition |
|---|---|---|---|---|
| 00:00–00:30 | Christopher, Home | “Exsensic's clients need one place to browse services, request a time and track progress. The portal brings those steps together while the agency keeps control of approval.” | One problem-to-journey slide: Browse → choose slot → brief → Requested → review → Confirmed | State this as the system's documented problem; confirm original interview detail from Part 1 |
| 00:30–01:00 | Christopher, Services/detail | “Clients can compare services before contacting the team. We kept the black, charcoal and lime identity because the lecturer praised the design system.” | Current populated desktop service capture beside genuine phone capture; captions show version/date | If service API unavailable, show dated accepted screenshots and disclose failure; never claim live success |
| 01:00–01:40 | Christopher, booking and detail | “The wizard checks for a real available slot before asking for the brief. Requested means the request was saved; Confirmed means Exsensic approved it.” | Annotated slot, requirements and saved-reference screenshots; live demo only on authorised demo data | FR12 secondary reference from CONTRACTS; exact Part 1 wording still pending |
| 01:40–02:15 | Christopher, filters | “The specific UX feedback was missing filter busy states. Applying filters now gives one visible and spoken loading message, prevents repeat activation and resets when you come back.” | Before: baseline Admin form lacks hook. After: show real code hook + short browser recording; cite 12 DOM checks, not API E2E success | Narrate the code: opt-in markup → shared handler → guard/status → pageshow recovery. Explain no business logic changed |
| 02:15–02:50 | Christopher, phone/keyboard | “At 390 pixels the navigation and forms fit. Tab reveals Skip to content. Keyboard users can reach the main content. Human NVDA acceptance remains a separate recorded test.” | Real home-390 and register-1280 captures; show live Tab/Enter skip and menu Escape | 40 public page/width inspections passed structural checks; additional selected populated checks passed; do not generalise to every edge case |
| 02:50–03:25 | Christopher, requirements/evidence slide | “Each requirement needs the feature, verification and a business benefit. FR12 tracks booking progress; FR25 has API-returned eligible staff. Other exact numbers await the original Part 1 list.” | Five feature/benefit cards: catalogue/self-service; availability/less repeated coordination; history/traceability; approval/business control; staff schedule/shared preparation | Use original FR labels only after source reconciliation. Do not turn the feature list order into guessed IDs |
| 03:25–04:00 | Christopher, handoff | “Daniel will explain the API rules and state changes. Dean will explain the database, security and management screens. Josh will explain the deployment and CI evidence.” | Architecture diagram: Browser → MVC → separate API → Core/Data → SQL; names below owned areas | Leave time for questions; do not take over other members' sections |

## Visual aid / capture storyboard

1. Problem and five-stage client journey, with Exsensic name and role labels.
2. Before/after filter feedback: baseline `cd70da1` Admin form versus new hook and real pending state. Capture a genuine slow local/staging navigation; do not fabricate a screenshot.
3. Annotated real Services and booking screenshots, only after populated acceptance; circle category, SAST, selected time, brief fields and Requested.
4. Phone/desktop pair from this pass: `evidence/home-390.png`, `evidence/register-1280.png`. Add numbers and accessible captions in the team's deck.
5. Requirement → screen → observed evidence → benefit diagram; only approved FR numbers. FR12/25 secondary references must be labelled as such until reconciled.
6. Test slide: build 0 warnings/errors; 110 unit + 104 integration; 12 isolated DOM checks; 80 public inspections (40 failure-state baseline, 40 API-backed repeat), 80 selected populated inspections and 7 existing E2E passes. Put “human screen reader and original FR wording pending” visibly on the same slide.

Suggested display board: problem/journey left, two large annotated screens centre, outcomes/testing limits and QR to approved demo right. Josh supplies accepted public URL. Record any walkthrough with clear narration, not silent scrolling through files. No deck/video is claimed created by these notes.

## Likely questions and grounded answers

| Question | Answer |
|---|---|
| Why retain dark/lime? | The lecturer praised the token system; retaining identity avoids unnecessary redesign. Text status labels accompany colour. |
| How are duplicate actions prevented? | The shared pending guard stops a second submit/activation; selection stays in the payload; history and timeout recovery unlock the control. The backend remains authoritative. |
| Does aria-disabled alone stop clicks? | No. The event guard enforces it, and the control remains focusable so keyboard users are not stranded. |
| Did you human-test NVDA? | Not yet. The explicit pending checklist must be performed and signed by a human. |
| Are all FRs proven? | No. All 33 IDs are registered, but exact original wording is unavailable; only FR12/25 have secondary citations. We do not invent acceptance evidence. |
| What happens on API failure? | Friendly errors and reload differ from genuine empty results. No fake success or frontend-generated booking data is used. |
| Where are tokens stored? | The existing encrypted authentication properties/cookie session retains the access token; it is not exposed in browser storage or page JavaScript. |
| Why is rescheduled work Requested? | The final shared design requires the changed appointment to be reviewed again; the API makes that transition. |

## Rehearsal / team coordination

Christopher should rehearse twice with a timer, speak the code explanation aloud, and ask a teammate to interrupt with two questions. Record actual duration and trim content if over four minutes. Securely configure separate role browser profiles; arrange real eligible demo bookings for Daniel's confirm/complete/reschedule examples. Keep dated accepted screenshots as fallback and clearly disclose live failure. Record rehearsal date, reviewer and changes: ______.


Final local verification on 28 October: 7/7 existing E2E tests passed after installing their required Chromium runtime. Existing seed settings were found in user secrets and used only in process memory. The normal API initialiser populated its configured local demo records; no migrations were applied. An additional Web-owned audit passed 80 populated page/width inspections and 10 interactions across Client, Staff and Admin, including real creation, reschedule/cancellation, cancel-dialog Escape/focus return, keyboard Requested/week navigation and admin POST filtering. A second 40-case public audit passed with real catalogue data; the first 40-case API-failure audit is retained separately. Commit-safe evidence: evidence/verification-summary.md (historical local E2E), private-browser-results.json and public-populated-browser-results.txt. No production resources were touched.
