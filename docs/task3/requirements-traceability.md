# Requirements traceability: FR01 to FR33

How each Part 1 functional requirement is met by the final system. Each row names the feature and the route, the automated test that proves it, and the benefit to Exsensic. Prepared 1 November 2026 from the code on `develop` (release v1.1.0, commit `cf6171c`).

## How to read the result

- **PASS**: an automated test (unit, integration or end-to-end) asserts the behaviour.
- **PARTIAL**: the feature is implemented and reachable, but no automated test asserts that specific behaviour. It is covered by manual use or by a wider journey test only.
- A PASS shows the behaviour works against the API and database. It does not certify the look of a screen or full accessibility; those are covered in the front-end chapter.
- The requirement wording is the short description from the Part 1 requirement list. The approved Part 1 document should be checked for the exact sentence before final marking.

The four booking statuses are Requested, Confirmed, Completed and Cancelled. A rejection is a cancellation with a reason (decision recorded in CONTRACTS.md section 12).

## Client requirements

| FR | Requirement | Feature and route | Proof (test) | Result | Benefit to Exsensic |
|---|---|---|---|---|---|
| FR01 | Client registration | Register page (`/Account/Register`); `POST /api/v1/auth/register` | `Register_ValidRequest_Returns201WithClientToken`, `Register_DuplicateEmail_Returns409`, `Register_WeakPassword_Returns400WithPasswordError` | PASS | Clients create their own account, so staff do not enter client details |
| FR02 | Client login and logout | Sign in and Sign out; `POST /api/v1/auth/login` | `Login_CorrectPassword_Returns200WithToken`, `Login_WrongPassword_Returns401GenericMessage`, `Login_AfterFiveWrongPasswords_AccountIsLockedOut`, end-to-end `Login_AsClient_LandsOnRoleHome` | PASS | Each client sees only their own bookings |
| FR03 | Browse services | Services page (`/Services`); `GET /api/v1/services` | End-to-end `HomePage_Loads_ShowsServices` | PASS | Clients see what Exsensic offers without emailing |
| FR04 | Service details | Service details page; `GET /api/v1/services/{id}` | None specific | PARTIAL | Clients read what a service includes before booking |
| FR05 | Select a service | Book button on a service; `/Book` | End-to-end `Journey_ClientBooks_AdminConfirms` | PASS | One clear starting point for a request |
| FR06 | Enter project requirements | Step 2 of booking, fields set per service; `GET /api/v1/services/{id}/requirement-template` | Unit `RequirementValidatorTests`; `CreateAsync_InvalidRequirements_ThrowsValidationAndSavesNothing`; end-to-end `StoredScript_InRequirements_IsShownAsText` | PASS | Staff get the details they need up front |
| FR07 | View available slots | Step 1 of booking; `GET /api/v1/services/{id}/availability` | End-to-end `Journey_ClientBooks_AdminConfirms` (client picks a live slot) | PASS | Clients see real availability, not guesses |
| FR08 | Select a free slot | Slot picker; slot rules in the booking service | `CreateAsync_SlotAlreadyBooked_ThrowsSlotUnavailable`, `CreateAsync_SlotFreedByCancellation_CanBeBookedAgain`, `ConcurrentBookingTests` (simultaneous requests for one slot) | PASS | No double bookings |
| FR09 | Submit a booking request | Submit booking; `POST /api/v1/bookings` | `CreateAsync_ValidRequest_SavesBookingHistoryNotificationAndRequirements`; end-to-end journey | PASS | The request is stored with a reference and starts as Requested |
| FR10 | Request confirmation | Confirmation page after submit (`/Bookings/{id}/Confirmation`) | Covered through the booking journey only | PARTIAL | Clients see their reference straight away |
| FR11 | View My Bookings | My bookings page; `GET /api/v1/bookings/mine` | `Mine_TwoClients_ReturnsOnlyCallersBookingsNewestSlotFirst`, `Mine_StatusFilter_ReturnsOnlyThatStatus` | PASS | Clients follow all their requests in one place |
| FR12 | Track booking status | Booking details with history; `GET /api/v1/bookings/{id}` | `Get_Owner_ReturnsFullDetails`, `Get_OtherClient_Returns404LikeMissingBooking`, `BookingTransitionEvidenceTests` | PASS | Clients see Requested, Confirmed, Completed or Cancelled and why |
| FR13 | Reschedule | Reschedule page; `PUT /api/v1/bookings/{id}/reschedule` | `Reschedule_Requested_MovesSlotAndFreesOldOne`, `Reschedule_Confirmed_ReturnsToRequestedAndKeepsStaff`, `Reschedule_ToTakenSlot_Returns409SlotUnavailable`, `RescheduleReapprovalTests` | PASS | Changes are re-approved before staff commit |
| FR14 | Cancel | Cancel page; `PUT /api/v1/bookings/{id}/cancel` | `Cancel_RequestedByClient_CancelsWithReason`, `Cancel_ConfirmedInsideCutoff_ClientRefusedAdminAllowed`, `Cancel_ConfirmedOutsideCutoffByClient_Cancels` | PASS | The 24-hour cut-off protects planned work |
| FR15 | Profile and contact updates | Profile page (`/Account/Profile`); `GET` and `PUT /api/v1/me` | None specific | PARTIAL | Clients keep their contact details current |

## Staff requirements

| FR | Requirement | Feature and route | Proof (test) | Result | Benefit to Exsensic |
|---|---|---|---|---|---|
| FR16 | Staff login | Staff sign in; same sign-in route | End-to-end `Login_AsStaff_LandsOnRoleHome` | PASS | Staff see a staff workspace |
| FR17 | Assigned bookings | My schedule (`/Staff`); `GET /api/v1/staff/me/bookings` | `Schedule_MixedBookings_ReturnsOnlyOwnConfirmedInTimeOrder`, `Schedule_NonStaff_Returns403` | PASS | Staff see only their own confirmed work |
| FR18 | Booking information for staff | Staff booking details (`/Staff/Bookings/{id}`) | `Get_AssignedStaffOnly_Returns200ElseOtherStaff404` | PASS | Staff have what they need for the job |
| FR19 | Client requirements for staff | Requirements shown on the staff booking page | Shown on the page; no test asserts the requirement values specifically | PARTIAL | Staff prepare with the client's brief |
| FR20 | Staff schedule | Weekly schedule | `Schedule_MixedBookings_ReturnsOnlyOwnConfirmedInTimeOrder`, `Schedule_DefaultAndInvalidRanges_Returns200Or400` | PASS | Staff plan their week |
| FR21 | Mark booking complete | Complete button; `PUT /api/v1/staff/bookings/{id}/complete` | `Complete_AssignedStaffAfterStart_CompletesAndNotifiesClient`, `Complete_BeforeSlotStart_Returns409InvalidTransition`; end-to-end `StaffCompletes_SeededPastBooking` | PASS | The client is told when work is finished |

## Admin requirements

| FR | Requirement | Feature and route | Proof (test) | Result | Benefit to Exsensic |
|---|---|---|---|---|---|
| FR22 | Admin login | Admin sign in | End-to-end `Login_AsAdmin_LandsOnRoleHome` | PASS | A separate, protected workspace |
| FR23 | See all bookings | Admin bookings (`/Admin/Bookings`); `GET /api/v1/admin/bookings` | `List_OneDate_RequestedFirstOldestFirst`, `List_StatusFilterAndPaging_ReturnsRequestedSecondPage` | PASS | One queue for every request |
| FR24 | Approve or reject | Confirm and Reject; `PUT /api/v1/admin/bookings/{id}/confirm` and `/reject` | `Confirm_QualifiedFreeStaff_ConfirmsAndNotifies`, `Reject_WithReason_CancelsWithPrefixedReason`, `Reject_EmptyReason_Returns400ValidationFailed` | PASS | Admins keep control of what is accepted |
| FR25 | Assign qualified, free staff | Staff selector; `GET /api/v1/admin/staff` | `StaffOptions_MixedStaff_ListsOnlyQualifiedActiveFreeStaff`, `Confirm_UnqualifiedStaff_Returns409StaffUnavailable`, `Confirm_StaffBookedAtSameTime_Returns409StaffUnavailable` | PASS | Only suitable, free people are offered |
| FR26 | Manage slots | Time slots (`/Admin/TimeSlots`); generate and delete | `GenerateSlots_SameRequestTwice_CreatesThenSkipsDuplicates`, `DeleteSlot_WithActiveBooking_Returns409SlotHasBooking` | PASS | Availability is created in bulk without duplicates |
| FR27 | Block slots | Block and Unblock; `PUT /api/v1/admin/timeslots/{id}/block` | `BlockThenUnblockSlot_Returns204AndRecordsTheReason`, `BlockSlot_WithActiveBooking_Returns409SlotHasBooking` | PASS | Holidays and unavailable times are closed safely |
| FR28 | Add services | Add service; `POST /api/v1/admin/services` | `CreateService_ValidRequest_Returns201AndAppearsInAdminList`, `CreateService_DuplicateName_Returns409Duplicate` | PASS | New offers go live without a developer |
| FR29 | Edit or deactivate a service | Edit and Toggle; `PUT /api/v1/admin/services/{id}` | `UpdateService_NewDetails_Returns204AndSavesThem`, `DeactivateService_Returns204AndHidesItFromThePublicCatalogue` | PASS | Retired services leave the public catalogue |
| FR30 | View client information | Client contact details on the admin booking page | Shown on the page; no test asserts the contact fields specifically | PARTIAL | Admins can contact the client |
| FR31 | Admin manages bookings | Confirm, reject and cancel at any time | `Cancel_ConfirmedInsideCutoff_ClientRefusedAdminAllowed`, `Confirm_*` and `Reject_*` tests | PASS | Admins resolve exceptions |
| FR32 | Manage user accounts | Users (`/Admin/Users`): list, create staff, activate and deactivate; `GET`, `POST` and `PUT /api/v1/admin/users` | `CreateStaff_ExistingEmail_Returns409Duplicate`, `CreateStaff_WeakPassword_Returns400WithPasswordError`, `AdminSelfLockoutTests` | PASS | Staff accounts are managed without database access |
| FR33 | Dashboard overview | Admin home; `GET /api/v1/admin/dashboard` | `Dashboard_NewAndCancelledBookings_UpdatesStatusCounts`, `Dashboard_BookingsOnDifferentDays_SortsIntoTodayAndNextSevenDays` | PASS | A quick view of what needs attention |

## Summary

| Result | Count |
|---|---|
| PASS | 28 |
| PARTIAL | 5 (FR04, FR10, FR15, FR19, FR30) |
| Not implemented | 0 |

The five PARTIAL rows are features that exist and were used in the demonstration, but no automated test asserts them on their own. They are listed here so the gap is visible, not hidden.

User management is limited to creating staff accounts, listing users and activating or deactivating them. Editing an existing user's details is not implemented.
