# Person 1 commit allowlist and exclusions

Reviewed 28 October 2026. Repository: `C:\Users\Chris\AndroidStudioProjects\exsensic-insy7315`. Branch: `feat/p1-task3-feedback-improvements`. Paths below are relative to that repository for portable staging; no Git staging/commit was performed.

**Ready for a draft/review PR with declared external blockers; not merge-ready.** Current clean restore fails NU1902/NU1903, the referenced GitHub CI run is failed, and the October 5 staging fixture failure needs latest-deployment verification. Original FR wording, human reader acceptance and final guide/rehearsal publication details remain outstanding.

## Modified existing files — 11

- `docs/accessibility.md`
- `docs/demo-script.md`
- `src/Exsensic.Web/Views/Admin/Bookings.cshtml`
- `src/Exsensic.Web/Views/Book/Index.cshtml`
- `src/Exsensic.Web/Views/Bookings/Index.cshtml`
- `src/Exsensic.Web/Views/Bookings/Reschedule.cshtml`
- `src/Exsensic.Web/Views/Services/Index.cshtml`
- `src/Exsensic.Web/Views/Shared/_Layout.cshtml`
- `src/Exsensic.Web/Views/Staff/Index.cshtml`
- `src/Exsensic.Web/wwwroot/css/journeys.css`
- `src/Exsensic.Web/wwwroot/js/site.js`

## New reviewable files — 23

- `docs/task3/person1/accessibility-manual-test-checklist.md`
- `docs/task3/person1/ci-staging-handover.md`
- `docs/task3/person1/client-user-guide-draft.md`
- `docs/task3/person1/commit-inventory.md`
- `docs/task3/person1/completion-report.md`
- `docs/task3/person1/dean-josh-handover-message.md`
- `docs/task3/person1/evidence/filter-browser-results.json`
- `docs/task3/person1/evidence/home-390.png`
- `docs/task3/person1/evidence/public-browser-results.json`
- `docs/task3/person1/evidence/public-populated-browser-results.json`
- `docs/task3/person1/evidence/register-1280.png`
- `docs/task3/person1/evidence/services-1280.png`
- `docs/task3/person1/evidence/services-390.png`
- `docs/task3/person1/evidence/verification-summary.md`
- `docs/task3/person1/expo-speaking-evidence.md`
- `docs/task3/person1/feedback-improvement-log.md`
- `docs/task3/person1/person1-final-report-section.md`
- `docs/task3/person1/requirements-traceability.md`
- `docs/task3/person1/responsive-test-matrix.md`
- `src/Exsensic.Web/tests/catalog-filters.browser.js`
- `src/Exsensic.Web/tests/filter-feedback.browser.js`
- `src/Exsensic.Web/tests/private-pages.browser.cjs`
- `src/Exsensic.Web/tests/public-pages.browser.js`

## What not to commit

- Any files in the local-only outputs directory (`person1-task3-local-only`): raw build/test/CLI logs, private-browser-results.json and the former machine-specific changed-files.txt. Nine original raw files were moved there without deletion. Earlier outputs copies are local deliverables too, not commit input.
- Playwright traces/ZIPs, videos, screenshots of signed-in private data, browser storage-state JSON, cookies, JWTs, HAR/network dumps or raw role test outputs. Existing generated traces under tests/Exsensic.E2ETests/bin/Debug/net10.0/TestResults/playwright are ignored by Git; do not force-add them.
- bin/, obj/, .vs/, TestResults/, browser profiles/caches, .playwright-cli/, attachments or workspace scripts.
- .env files, user secrets, credentials, connection strings, certificates/private keys, publish profiles or Azure settings.
- Any newly generated raw evidence if you rerun the browser helpers: inspect the allowlist again before staging.
- Backend/Data/Contracts, existing tests/Exsensic.E2ETests, dependencies, CI/CD or Azure edits: none belong in this Person 1 commit.

The JSON allowlist contains public structural checks and isolated control assertions, not private role data. The four screenshots show public Home/Register/Services only. A scoped text scan found no suspicious JWT, auth-cookie or password-literal patterns in proposed text files. That is a scoped review, not a full historical secret scan.

## Review before manual commit

1. Stage only the listed paths after reading the diff; do not force-add ignored outputs.
2. Check staged paths and whitespace, then inspect staged content for accidental secrets or generated artifacts.
3. Use a draft PR description that explicitly names the external CI restore blocker and historical staging E2E issue. Do not describe required checks as green.
4. Retain historical local test results with their date and later failed-restore correction.
5. Add real commit/PR identifiers to the report only after Christopher performs Git operations.

Suggested subject: `feat(web): address Person 1 Task 3 feedback and prepare review evidence`

- Add accessible filter feedback with repeat guards and history recovery.
- Preserve Exsensic branding and existing API/security boundaries.
- Add Web-owned browser checks and sanitised local verification evidence.
- Prepare requirements, client guide, accessibility and EXPO drafts.
- Hand over external CI/staging failures without changing teammate-owned files.
