# Task 3 change log: hosting, deployment and CI/CD (Josh, P4)

For each item: the earlier feedback, what changed, how it was checked, and what it means for Exsensic's users. Status is one of **Completed**, **Evaluated option** (considered and documented, not built) or **Pending** (not done yet; do not describe it as done). Task 2 marks and comments are from the marked feedback sheet.

Related: [chapter 7](chapter-07-hosting-cicd-operations.md), [operations.md](../operations.md), [migrations.md](../migrations.md).

## Summary

| # | Area | Task 2 mark | Status |
|---|---|---|---|
| 1 | Slots and autoscaling | Hosting stability 14/15 | Pipeline support Completed; S1 slot demonstration Evaluated option |
| 2 | Failing build from a vulnerable package | CI/CD 19/20 | Completed |
| 3 | Dependency updates | CI/CD 19/20 | Completed |
| 4 | Review rules and history | Git workflow 10/15 (shared) | Completed |
| 5 | Safe release of a database change | Deployment 17/20 | Completed |
| 6 | Monitoring | Hosting stability 14/15 | Completed |
| 7 | Final report chapter and user guide | Final report, user guide | Chapter 7 Completed; user guide sections in review |
| 8 | Production release and smoke checks | CI/CD, hosting stability | Completed (slot swap path not exercised) |

## 1. Slots and autoscaling

- **Earlier feedback:** Hosting stability 14/15, "slots/autoscaling suggested".
- **Change made:** a costed tier decision recorded in operations.md section 8 (PR #52) and chapter 7. The production workflow can deploy each app to a slot, test it there and swap it into production, controlled by the repository variable `PRODUCTION_SLOT_NAME` (PR #51). With the variable empty it deploys directly, as before.
- **Verification:** the workflow change passed `ci` and ran in the v1.1.0 release with `PRODUCTION_SLOT_NAME` unset, so it deployed directly and skipped the swap steps. The slot path has not been run, because B1 has no slots.
- **Client impact:** once the team approves the extra cost, a release can be swapped in without restarting the live site.
- **Status:** pipeline support and decision **Completed**. Running slots and autoscale on S1 (about US$21 for seven days, above the US$20 budget alert) is an **Evaluated option**, not implemented.

## 2. Failing build from a vulnerable package

- **Earlier feedback:** CI/CD 19/20 (the pipeline was praised; the build must stay green).
- **Change made:** CI failed on restore because a transitive package, `System.Security.Cryptography.Pkcs`, had two published vulnerabilities (GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx). It is pinned to the patched 10.0.13 in `Exsensic.Data.csproj` (PR #49).
- **Verification:** `ci` green on the pull request; `cd-staging` green after the merge, including the end-to-end tests.
- **Client impact:** a known-vulnerable library is not shipped. The build treats these warnings as errors, so a vulnerable dependency stops the pipeline.
- **Status:** **Completed**.

## 3. Dependency updates

- **Earlier feedback:** CI/CD 19/20.
- **Change made:** the Dependabot backlog was merged and duplicate pull requests were closed. Newer patch updates (ASP.NET Core 10.0.14 and a GitHub Actions update) are raised by Dependabot and go through the same checks.
- **Verification:** each merged update passed `ci` and `pr-checks`; the last staging run was green.
- **Client impact:** security and bug-fix releases of the framework reach the system through the normal review path.
- **Status:** **Completed** for the merged updates. Any later Dependabot update is **Pending** until reviewed.

## 4. Review rules and history

- **Earlier feedback:** Git workflow 10/15 (shared), "uneven commit balance and compressed timeline weakened history".
- **Change made:** branch protection on `develop` and `main` requires a pull request and passing `build-test` and `pr-checks`, and blocks force pushes and deletion. A CODEOWNERS file sends review to the owner of each area (PR #64). The migration rules were written down (PR #50). History was not rewritten or back-dated; the report says plainly that the Task 2 timeline was compressed.
- **Verification:** the protection settings were read back from the GitHub API on 2026-10-29. Required approvals are not set, because the single merger would block themselves.
- **Client impact:** every change is reviewed and traceable to a pull request.
- **Status:** **Completed**. Required approvals are **Deferred**.

## 5. Safe release of a database change

- **Earlier feedback:** Deployment 17/20 (no major gap); the Task 3 audit asks for safe releases.
- **Change made:** a second migration, `AddBookingRelatedConstraintsAndIndexes`, adds a unique index on `BookingRequirements (BookingId, FieldKey)`. Before the release, a read-only data check was added to the release steps (PR #65, `docs/task3/pre-migration-checks.sql`). The one-off handover runbook with a local path was removed.
- **Verification:** the check was run on `sqldb-exsensic-prod` on 2026-10-31: no duplicate `(BookingId, FieldKey)` rows (PASS) and no `FieldKey` over 50 characters (PASS). Staging already runs the migration and its tests passed.
- **Client impact:** a database change cannot stop the live system because of existing data.
- **Status:** **Completed**.

## 6. Monitoring

- **Earlier feedback:** Hosting stability 14/15; Application Insights was praised as wired in.
- **Change made:** the code was already wired in, but monitoring was not switched on in Azure. On 2026-10-31 Application Insights (`appi-exsensic`, workspace `log-exsensic`, UAE North) was created and connected to all four apps. The portal's automatic setup failed because it tried to use a resource group and a region that do not exist or are blocked, so both resources were created by hand in the project resource group.
- **Verification:** the Azure CLI shows `APPLICATIONINSIGHTS_CONNECTION_STRING` on all four apps, and both websites answer `/health` with 200.
- **Client impact:** the team can see failures and slow requests in production.
- **Status:** **Completed**. A dated screenshot of the Application Insights overview is in the evidence file.

## 7. Final report chapter and user guide

- **Earlier feedback:** the Task 3 brief asks for the hosting chapter of the final report and the technical sections of the user guide.
- **Change made:** chapter 7, hosting, CI/CD and operations (PRs #66 and #84), with an evidence table, a status table and the failure answers; the technical user-guide sections (access, accounts, browsers, settings by name, backup, help, FAQ and glossary).
- **Verification:** every pull request number and workflow claim was checked against the repository; the guide's rules (24-hour cancellation cut-off, no password reset, `/health`) were checked against the code and the live site.
- **Client impact:** a marker or new team member can understand how the system runs and how to get help without asking.
- **Status:** chapter 7 **Completed**. User guide technical sections: submitted for review; the support contact and extra browser results are **Pending** edits.

## 8. Production release and smoke checks

- **Earlier feedback:** CI/CD 19/20; Hosting stability 14/15; the Task 3 audit asks for a clean pull request to `main` to production sequence.
- **Change made:** release v1.1.0 (PR #90) merged `develop` into `main` at commit `cf6171c`. It carries the Pkcs fix, the slot-aware pipeline, the new migration and the other members' Task 3 work. Dependabot updates #79 to #83 were left out on purpose, because #81 does not build without a coordinated EF Core bump and #79 changes the deploy login step.
- **Verification:** `cd-production` run 41297454238 succeeded (build and deploy). It paused at the approval gate; approval issue #91 was approved and closed on 2026-11-01. The deploy used the direct path, because `PRODUCTION_SLOT_NAME` is not set, so the slot swap steps were skipped. The API smoke test passed after five waiting attempts while the app restarted, the web smoke test passed after three, and the temporary runner access rule was removed. The production website `/health` answers `Healthy` (checked 2026-11-01).
- **Client impact:** the live system has the fixes and improvements.
- **Status:** **Completed**. The slot swap path was not exercised.

## Pull requests in this change log

| PR | Title |
|---|---|
| #49 | chore(deps): pin patched cryptography pkcs package |
| #50 | docs(data): add migration strategy |
| #51 | ci(deploy): deploy production through an optional slot swap |
| #52 | docs(deploy): add slots and autoscaling decision |
| #65 | docs(deploy): add release guards and remove handover runbook |
| #66 | docs(deploy): add hosting and ci/cd report chapter |
| #84 | docs(deploy): add evidence table and failure answers to chapter 7 |
