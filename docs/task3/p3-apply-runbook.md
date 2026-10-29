# P3 Task 3: apply runbook

Handover for Dean (P3). Eleven patches were prepared with an AI assistant. Under CONTRIBUTING.md section 6, the assistant did not commit, push or open pull requests, so **you** review, apply and commit each one under your own account. Commit messages follow section 4 exactly: no `Co-authored-by` or tool lines. Declare the AI assistance in the report's AI declaration (section 6).

Commands are for PowerShell, run from the repository root.

## 1. Before you apply

- [ ] **Copy the patches out of `%TEMP%` now.** They were written to a temporary folder that Windows can clear:
  ```powershell
  Copy-Item -Recurse "C:\Users\emeris\AppData\Local\Temp\claude\C--Users-emeris-Documents-GitHub-exsensic-insy7315\f281a63a-16ce-415c-95ee-07b4733f3d8b\scratchpad\patches" "$HOME\Documents\p3-patches"
  $P = "$HOME\Documents\p3-patches"
  ```
- [ ] **Check the copy** against the sha256 list you were given: `Get-FileHash $P\*.patch -Algorithm SHA256`.
- [ ] **Update develop.** Your local `develop` is 2 commits behind `origin/develop` (b53472f):
  ```powershell
  git switch develop
  git pull origin develop
  ```
- [ ] **You don't need the `W:` drive mapping.** It was only used to work around Windows path length inside the temporary worktrees. Apply in your normal repo.
- [ ] **Local branches with these names may already exist** (no commits on them). `git switch -C` below resets them to `origin/develop`, which is safe because they hold nothing.
- [ ] **Test database:** if `dotnet test` can't reach `(localdb)\MSSQLLocalDB`, either run `sqllocaldb create MSSQLLocalDB -s` once, or set `$env:ConnectionStrings__Tests = 'Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True'` in the same window.
- [ ] **Staging migration check:** run section 2 **before PR 01 is merged**.

## 2. Staging migration check (before merging PR 01)

Merging PR 01 into `develop` triggers `cd-staging`, and the API applies the new migration on start-up. Check the data first:

1. Azure portal → the staging SQL database → **Query editor**. Paste and run `docs/task3/pre-migration-checks.sql` (added by patch 10). It only reads data.
2. Both rows must say **PASS**:
   - `Duplicate (BookingId, FieldKey) in BookingRequirements`
   - `FieldKey longer than 50 characters`
3. If either shows a count, **don't merge PR 01**. Tell the team; the data must be fixed first.
4. Run the same check against **production** before the release that carries PR 01 is merged to `main`.

## 3. Apply and commit, in merge order

Apply each patch on its **own branch from `origin/develop`**. Run the build and tests (CONTRIBUTING section 3 step 4), commit, push, open a PR into `develop`, and wait for CI and a reviewer before merging. Merge in this order:

**10 → 01 → 03 → 02 → 04 → 05 → 06 → 07 → 08 → 09.** The docs go last because they cite the new tests and migration. **11** (CODEOWNERS) can go at any time.

All of 01–09 were applied together in this order on b53472f: build 0 warnings, 110 unit and 175 integration tests passed. 02 and 04 add the same `using` line to the same file, and git merges that cleanly.

Before every commit:
```powershell
dotnet build Exsensic.sln -c Release
dotnet test tests/Exsensic.UnitTests -c Release
dotnet test tests/Exsensic.IntegrationTests -c Release
```

### 10. docs/p3-apply-runbook (this runbook and the SQL check)
```powershell
git fetch origin
git switch -C docs/p3-apply-runbook origin/develop
git apply "$P\10-docs_p3-apply-runbook.patch"
git add docs/task3/p3-apply-runbook.md docs/task3/pre-migration-checks.sql
git commit -m "docs(data): add p3 apply runbook and pre-migration checks" -m "- Add the merge order and commit commands for the p3 task 3 patches`n- Add read-only staging checks for the booking requirements index migration"
git push -u origin docs/p3-apply-runbook
```

### 01. feat/p3-task1-configs
```powershell
git switch -C feat/p3-task1-configs origin/develop
git apply "$P\01-feat_p3-task1-configs.patch"
git add src/Exsensic.Data/Configurations/BookingRequirementConfiguration.cs src/Exsensic.Data/Configurations/BookingStatusHistoryConfiguration.cs src/Exsensic.Data/Configurations/NotificationConfiguration.cs src/Exsensic.Data/Migrations/20261029161643_AddBookingRelatedConstraintsAndIndexes.cs src/Exsensic.Data/Migrations/20261029161643_AddBookingRelatedConstraintsAndIndexes.Designer.cs src/Exsensic.Data/Migrations/ExsensicDbContextModelSnapshot.cs
git commit -m "feat(data): add booking requirement, history and notification indexes" -m "- Add a unique (BookingId, FieldKey) index so each field is answered once`n- Index BookingStatusHistory on (BookingId, ChangedAtUtc) for the timeline`n- Index Notifications on (UserId, IsRead) for the unread badge and list`n- Bound FieldKey to 50 characters because SQL Server cannot index nvarchar(max)`n- Generate migration AddBookingRelatedConstraintsAndIndexes; InitialCreate untouched"
git push -u origin feat/p3-task1-configs
```
Optional before pushing: review the SQL with `dotnet ef migrations script InitialCreate AddBookingRelatedConstraintsAndIndexes --idempotent --project src/Exsensic.Data --startup-project src/Exsensic.Api` (migrations.md rule 4).

### 03. fix/p3-block-booked-slot
```powershell
git switch -C fix/p3-block-booked-slot origin/develop
git apply "$P\03-fix_p3-block-booked-slot.patch"
git add src/Exsensic.Core/Catalog/ServiceCatalogService.cs tests/Exsensic.IntegrationTests/Catalog/AdminTimeSlotRulesTests.cs
git commit -m "fix(catalog): refuse to block a slot that has an active booking" -m "- Return 409 slot_has_booking as CONTRACTS.md section 7 already documents`n- Share the active-booking check between block and delete`n- Test block and delete of a booked slot through the api"
git push -u origin fix/p3-block-booked-slot
```

### 02. fix/p3-admin-self-lockout (two commits)
```powershell
git switch -C fix/p3-admin-self-lockout origin/develop
git apply "$P\02-fix_p3-admin-self-lockout.patch"
git add src/Exsensic.Api/Controllers/AdminUsersController.cs tests/Exsensic.IntegrationTests/Security/AdminSelfLockoutTests.cs
git commit -m "fix(admin): stop an admin deactivating their own account" -m "- Return 400 validation_failed when the target is the signed-in admin`n- The seeder creates the only admin, so self-deactivation could lock everyone out`n- Test that self-deactivation is refused and another admin can still be deactivated"
git add src/Exsensic.Web/Controllers/AdminUsersController.cs
git commit -m "fix(admin): show the api reason when a user toggle fails" -m "- The users list has no fields to highlight, so show the field message in the toast"
git push -u origin fix/p3-admin-self-lockout
```

### 04. fix/p3-create-staff-errors
```powershell
git switch -C fix/p3-create-staff-errors origin/develop
git apply "$P\04-fix_p3-create-staff-errors.patch"
git add src/Exsensic.Api/Controllers/AdminUsersController.cs tests/Exsensic.IntegrationTests/Security/AdminCreateStaffTests.cs
git commit -m "fix(admin): report a weak staff password as a validation error" -m "- Only duplicate email or user name is now 409 duplicate`n- Any other identity failure is 400 with the reasons on the Password field`n- Test both cases through the api"
git push -u origin fix/p3-create-staff-errors
```

### 05. test/p3-admin-tests (two commits)
```powershell
git switch -C test/p3-admin-tests origin/develop
git apply "$P\05-test_p3-admin-tests.patch"
git add tests/Exsensic.IntegrationTests/Security/AdminRouteTests.cs
git commit -m "test(security): add role enforcement tests for admin endpoints" -m "- Every admin services, time slots and users endpoint is 401 when anonymous`n- Clients and staff get 403 forbidden; admins get 200 on the list endpoints"
git add tests/Exsensic.IntegrationTests/Security/AdminCatalogueTests.cs
git commit -m "test(admin): add admin catalogue and user journey tests" -m "- Create, edit and archive a service, including duplicate and stale rowversion`n- Generate, block and unblock time slots, including unknown ids`n- Create a staff account and deactivate a user"
git push -u origin test/p3-admin-tests
```

### 06. test/p3-security-regression (five commits)
```powershell
git switch -C test/p3-security-regression origin/develop
git apply "$P\06-test_p3-security-regression.patch"
git add tests/Exsensic.IntegrationTests/Security/AuthApiTests.cs
git commit -m "test(auth): add login success and account lockout tests" -m "- Correct password returns a token for the account's role`n- Five wrong passwords lock the account even from different ips"
git add tests/Exsensic.IntegrationTests/Security/OwnershipTests.cs
git commit -m "test(security): add staff booking ownership test" -m "- Staff not assigned to a booking get 404 when reading it"
git add tests/Exsensic.IntegrationTests/Security/SecurityStampTests.cs
git commit -m "test(security): add security stamp invalidation tests" -m "- A deactivated user's existing token is rejected with 401`n- Reactivating does not revive a token issued before deactivation"
git add tests/Exsensic.IntegrationTests/Security/SecurityHeaderTests.cs
git commit -m "test(security): add api security header tests" -m "- Check csp, nosniff, frame, referrer, permissions and cache headers`n- Cover success, 404 and 401 responses so error paths are hardened too"
git add tests/Exsensic.IntegrationTests/Security/RateLimitTests.cs
git commit -m "test(security): add registration rate limit tests" -m "- Fourth registration in a minute from one ip is 429 with retry-after`n- Another ip is unaffected by the first ip's limit"
git push -u origin test/p3-security-regression
```

### 07. docs/p3-security
```powershell
git switch -C docs/p3-security origin/develop
git apply "$P\07-docs_p3-security.patch"
git add docs/security.md
git commit -m "docs(security): expand security controls with popia and threat model" -m "- Map each owasp top 10 item to the control and test in this codebase`n- Explain auth, security stamp, 404-not-403, rate limits and headers`n- Add popia data inventory, access table, threat model and known gaps"
git push -u origin docs/p3-security
```

### 08. docs/p3-chapter-06
```powershell
git switch -C docs/p3-chapter-06 origin/develop
git apply "$P\08-docs_p3-chapter-06.patch"
git add docs/task3/chapter-06-data-security-architecture.md
git commit -m "docs(data): add report chapter on data, security and architecture" -m "- Add mermaid erd, data dictionary, migration and seed strategy`n- Map admin requirements to routes and tests, marking fr32 as partial`n- Record design trade-offs, deferred endpoints and git governance"
git push -u origin docs/p3-chapter-06
```

### 09. docs/p3-user-guide-admin
```powershell
git switch -C docs/p3-user-guide-admin origin/develop
git apply "$P\09-docs_p3-user-guide-admin.patch"
git add docs/task3/user-guide-admin.md
git commit -m "docs(admin): add admin user guide" -m "- Cover dashboard, approval, staff assignment, services, slots and users`n- Add client data permissions and safe sign-out, with figure placeholders"
git push -u origin docs/p3-user-guide-admin
```

### 11. chore/p3-codeowners (any time)
CODEOWNERS is yours (CONTRIBUTING section 1), but the PR still needs Josh's review because `.github/` is his.
```powershell
git switch -C chore/p3-codeowners origin/develop
git apply "$P\11-chore_codeowners.patch"
git add .github/CODEOWNERS
git commit -m "chore(repo): add dean as code owner for security and admin api" -m "- Request dean's review on api security, admin and catalogue controllers`n- Cover the data project, security tests and docs/security.md`n- Keep daniel as owner of the admin bookings controller"
git push -u origin chore/p3-codeowners
```

## 4. Live evidence (after deployment to staging)

Capture each item as a screenshot or a copied result for the P1/02 evidence pack.

1. **Migration applied.** In Query editor on staging:
   ```sql
   SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;
   ```
   Expect `20261029161643_AddBookingRelatedConstraintsAndIndexes` after `20261002163643_InitialCreate`.
2. **Demo bookings, all four statuses, one rejected.**
   ```sql
   SELECT Status, COUNT(*) AS Bookings FROM Bookings GROUP BY Status;
   SELECT Reference, CancellationReason FROM Bookings WHERE CancellationReason LIKE 'Rejected:%';
   ```
   Expect Requested, Confirmed, Completed and Cancelled rows, and at least one `Rejected:` reason.
3. **Web CSP.** Open DevTools → Console, then load the catalogue (`/Services`), the booking wizard, `/Admin`, `/Admin/Services`, `/Admin/TimeSlots` and `/Admin/Users`. No "Refused to load" or "Refused to execute" messages. Screenshot the Network tab showing the `Content-Security-Policy` response header.
4. **Antiforgery (manual; the integration tests host only the API).** Signed in as admin on any page, run this in the DevTools console. It targets a user id that doesn't exist, so nothing can change even if the check failed:
   ```js
   fetch('/Admin/Users/00000000-0000-0000-0000-000000000000/Toggle', { method: 'POST', body: new URLSearchParams({ isActive: 'false' }) }).then(r => console.log(r.status))
   ```
   Expect `400` (no antiforgery token). Screenshot the console.
5. **Admin screenshots for the user guide** (`docs/task3/user-guide-admin.md`, figures 1.1–8.1): dashboard, booking results and details, staff selection, services list and form, time slots list and generate form, users list and add-staff form, navigation with Sign out. Capture these on the deployed site with demo data only.

## 5. Deferred (future work, not in this release)

These need a CONTRACTS.md change, which requires the team's sign-off (CONTRIBUTING section 2). There is no time before the EXPO, so FR32 stays PARTIAL. Chapter 6, section 6.10 records them.

- `GET /admin/users/{id}`: detail for one account, without the password hash, security stamp or lockout data.
- `PUT /admin/users/{id}/role`: change a user's role. Bump the security stamp, refuse self-demotion, and keep at least one active admin.
