# Project-wide CI / staging handover — Dean and Josh

Prepared 28 October 2026 by Christopher's Person 1 review. Documentation only; no dependency, backend, database, existing E2E, workflow or Azure files modified. GitHub status below describes the two referenced runs, not an assertion about every newer deployment.

## 1. CI restore failure — current confirmed blocker

Run: [40671793733](https://github.com/Josh0123456/exsensic-insy7315/actions/runs/40671793733), job [122760134559](https://github.com/Josh0123456/exsensic-insy7315/actions/runs/40671793733/job/122760134559). GitHub reports build-test failed at Restore packages on 28 October 2026. Build, unit/integration tests and later security scan steps were skipped.

Exact diagnostic messages (repeated for Data, API and IntegrationTests):

```text
error NU1902: Warning As Error: Package 'System.Security.Cryptography.Pkcs' 9.0.11 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-2fmv-rv2c-59x7
error NU1903: Warning As Error: Package 'System.Security.Cryptography.Pkcs' 9.0.11 has a known high severity vulnerability, https://github.com/advisories/GHSA-846f-f764-76mx
```

Affected paths:

- `src/Exsensic.Data/Exsensic.Data.csproj`: EF Core SQL Server PackageReference introduces the transitive dependency.
- `src/Exsensic.Api/Exsensic.Api.csproj` and `tests/Exsensic.IntegrationTests/Exsensic.IntegrationTests.csproj`: consume Data and inherit the affected dependency.
- `Directory.Build.props`: TreatWarningsAsErrors=true correctly promotes NuGet audit warnings to errors.
- `.github/workflows/ci.yml`: Restore packages correctly enforces the audit. This is not a reason to remove the gate.
- Generated `obj/project.assets.json`: resolved package graph; generated output, never a manual fix or commit target.

Confirmed with `dotnet nuget why Exsensic.sln System.Security.Cryptography.Pkcs`:

```text
Exsensic.Data
  Microsoft.EntityFrameworkCore.SqlServer 10.0.12
    Microsoft.Data.SqlClient 6.1.6
      System.Security.Cryptography.Pkcs 9.0.11
```

A fresh `dotnet restore Exsensic.sln --force --no-cache --nologo` reproduced NU1902/NU1903 locally and exited 1. Earlier local builds/tests passed before this refreshed audit and remain historical results only. They do not establish current clean-restore or CI readiness. Cached audit/restore state can explain the different local result; the cache contents were not independently reconstructed.

### Safest minimal remediation candidate — Dean reviews, Josh validates CI

Keep the current dependency graph otherwise stable and explicitly resolve the affected transitive package to **9.0.21**, the patched version in the same 9.x family. A narrow candidate is an explicit reference in the existing ItemGroup of `src/Exsensic.Data/Exsensic.Data.csproj`:

```xml
<PackageReference Include="System.Security.Cryptography.Pkcs" Version="9.0.21" />
```

This snippet is a recommendation only and has NOT been applied. Data is the introducing project, so its consumers should inherit the resolved patch. Dean must verify actual resolution across Data/API/IntegrationTests rather than adding unrelated references to all consumers. Upgrading the introducing SqlClient/EF dependency to an approved patched release is an alternative, but do not broaden framework upgrades unnecessarily. Microsoft also lists 10.0.13 as patched; selecting that family introduces a wider version change than the targeted 9.x patch. These are verified patched baselines for the two named advisories, not an assurance that no newer advisory exists.

Official sources: [high severity advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx), [moderate severity advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-2fmv-rv2c-59x7). Both identify 9.0.21 and 10.0.13 as patched.

Acceptance steps for the owners:

1. Apply the approved package correction in their own coordinated work.
2. Run clean restore, dependency-chain inspection and `dotnet list Exsensic.sln package --vulnerable --include-transitive` against current advisory data.
3. Verify the resolved Pkcs version in all affected projects; do not hand-edit assets files or merely clear a cache.
4. Run Release build, unit and SQL integration suites, then the configured E2E suite on authorised demo data.
5. Push through the team's process and obtain a successful GitHub run for the corrected revision.

Do not disable NuGetAudit, add NoWarn/WarningsNotAsErrors exceptions for NU1902/NU1903, remove TreatWarningsAsErrors or weaken security checks.

## 2. Historical staging E2E failure — recheck latest deployment

Run: [37319256633](https://github.com/Josh0123456/exsensic-insy7315/actions/runs/37319256633), job [111796224888](https://github.com/Josh0123456/exsensic-insy7315/actions/runs/37319256633/job/111796224888). Logs date this failure to **5 October 2026**, not today's Person 1 pass. That run's staging build, deploy and catalogue readiness check succeeded; E2E finished with 6 passed, 1 failed, 0 skipped.

```text
Exsensic.E2ETests.BookingJourneyTests.StaffCompletes_SeededPastBooking [FAIL]
System.TimeoutException : Timeout 60000ms exceeded.
```

The failing wait at `tests/Exsensic.E2ETests/BookingJourneyTests.cs:42` requires:

```text
section.schedule-day containing .today-label
  -> article containing EXS-TODAY
  -> containing Confirmed
  -> first link with accessible name matching ^View booking
```

The expected UI match was absent for the entire wait. This establishes the failed selector, not conclusive proof of which database/setup condition caused it. No staging database was inspected or changed in this review.

Affected investigation paths:

- `tests/Exsensic.E2ETests/BookingJourneyTests.cs`: assumes an eligible seeded booking exists today.
- `tests/Exsensic.E2ETests/E2ETestBase.cs`: role account/environment, sign-in and base URL setup.
- `src/Exsensic.Data/Seed/DatabaseInitialiser.cs` and `DemoBookingSeeder.cs`: demo seed enablement, staff identity, daily fixture and slot creation.
- `.github/workflows/cd-staging.yml`: environment wiring and readiness; compare its configuration with the latest deployed revision.
- Person 1's `StaffController`, `StaffJourney`, `Views/Staff/Index.cshtml` and `_BookingCard.cshtml`: read-only comparison of displayed week, today marker and returned assignments if API data exists but no matching card renders. Do not presume the frontend is defective from the timeout alone.

### Investigation and safe correction — Josh with Dean

1. Identify the latest staging deployment revision and run the test against that revision. Do not treat an October 5 failure as proof of today's staging state; do not treat local success as staging clearance.
2. Inspect that job's private trace/screenshot artifact. Confirm successful Staff authentication, current SAST week/day, correct staff account, visible cards and any API error. Keep traces/cookies/private details out of the commit or chat.
3. Check seed execution logs and approved read-only database/API evidence: daily EXS-TODAY reference, Confirmed status, staff assignment, slot start already elapsed, and correct SAST date. Check that it is returned by GET /api/v1/staff/me/bookings for the requested week.
4. Check earlier test/rehearsal consumption. Completing a daily fixture makes that particular record unavailable for another completion test. The current seeder replenishes it on startup; a rerun without API restart or a run after the day changes may not have a fresh fixture. Slot creation may also return no slot. These are hypotheses to verify, not a proven historical root cause.
5. Coordinate a repeatable, isolated eligible demo fixture and explicit readiness before the test. Avoid reliance on shared long-lived staging bookings or catalog readiness alone. Ensure sequential/concurrent runs cannot consume one another's fixture.
6. Assert preconditions with a useful failure (date, account label, expected fixture status; no secrets), then exercise real completion. Keep API business rules authoritative. Do not skip the test, fake a past booking in Web, bypass timing/authorization or merely lengthen the timeout.
7. Rerun on latest staging and repeat with a fresh valid fixture to prove repeatability. Record head revision, date, result and owner sign-off.

The current local suite passed 7/7 with real local seed configuration on 28 October. This is evidence that the local path can work, not proof that the older staging setup was repaired.

## 3. Separation from Person 1 changes

Person 1 changed opt-in frontend filter feedback and shared status presentation, Web-owned browser review scripts, and authorised documentation. Data package versions, domain logic, seeding, existing E2E tests, workflows and Azure were not changed. The vulnerable dependency chain exists in the baseline; the staging failure predates this pass. No evidence links either failure to the new filter attributes or JavaScript.

Current status: **Person 1 diff ready for review; clean solution restore blocked; referenced GitHub CI remains failed; historical staging E2E requires latest-deployment verification.** A draft PR can describe these external blockers. Do not call the branch merge-ready or CI green until GitHub confirms the required checks for its revision.
