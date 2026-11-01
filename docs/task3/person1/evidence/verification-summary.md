# Verification summary — commit-safe record

Person 1 review date: 28 October 2026. Baseline cd70da1. This sanitised summary deliberately separates successful earlier local execution from the subsequent restore failure and GitHub status. Raw outputs are retained locally outside the repository; they are not required in the commit.

| Verification | Recorded result | Scope / limit |
|---|---|---|
| Earlier local solution build | Passed; 0 warnings/errors | Historical local execution before fresh vulnerability refresh; not current clean-build clearance |
| Earlier local unit suite | 110 passed, 0 failed/skipped | Actual execution in the Task 3 pass; not a passing CI result |
| Earlier local integration suite | 104 passed, 0 failed/skipped | Actual local SQL fixtures; not a passing CI result |
| Earlier configured local existing E2E suite | 7 passed, 0 failed/skipped | After installing its required Chromium runtime; initial configured attempt failed startup 7/7 because runtime absent |
| Isolated real site.js browser assertions | 12 passed | Minimal test controls, no fake API/auth success |
| Public page/width inspections | 40 initial + 40 API-backed repeat | Structural/layout checks; selected public routes |
| Populated role page/width inspections | 80 passed | Local demo data, 16 page/state visits across five widths; not all possible content/edge cases |
| Populated interactions | 10 passed | Sign-ins; create/reschedule/cancel; dialog focus; selected keyboard filters |
| Actual catalogue filter/history checks | 4 passed | Keyboard category, real matching cards, Back/refresh unlocking |
| Later clean restore with --force --no-cache | FAILED, exit 1 | Pkcs 9.0.11 NU1902/NU1903 reproduced after refresh; current solution clean-restore blocker |
| Referenced GitHub CI run 40671793733 | FAILED at Restore packages | Build/tests skipped; still failed when rechecked; no current CI-green claim |
| Referenced staging run 37319256633 | FAILED on 5 October; 6 passed/1 failed | Historical EXS-TODAY completion fixture wait; latest deployment must be checked by Josh/Dean |
| Current Person 1 syntax/diff review | See final review record | No dependency fix or new full solution test pass claimed |
| Human NVDA/Narrator | PENDING HUMAN VERIFICATION | Checklist prepared, human execution not performed |
| Exact FR01–FR33 original wording | NOT VERIFIED | Original Part 1 source absent; secondary FR12/25 references only |

Tracked evidence: filter/public structural JSON and four public/catalogue screenshots have no credential fields or personal booking content. The source scripts are reviewable and retain no hard-coded credentials. No suspicious JWT/auth-cookie/password-literal patterns were found by the scoped text scan; this is not a repository-wide secret audit.

Excluded local evidence: raw build/test/CLI output, private role-results JSON and the machine-specific changed-files list were moved to a local-only outputs folder. The existing ignored TestResults/Playwright traces remain local and may contain session tokens, cookies, form values and demo data. Do not publish or attach traces without separate redaction. No secrets or trace contents are reproduced here.

No solution restore/build/E2E rerun was performed during the final documentation-only handover review. The latest fresh restore result remains FAILED, as reproduced during the preceding failure diagnosis. See ci-staging-handover.md for exact diagnostics and owner acceptance steps.

Final handover review: site.js and all four Web-owned browser helper scripts passed Node syntax checking. The Git whitespace check passed (only LF→CRLF normalisation advisories). Git reports no changes in Data, API, Core, Contracts, existing tests, workflows, global.json or Directory.Build.props. No backend/dependency/E2E/workflow/Azure edits or Git mutations performed. Person 1 remains on the correct feature branch. Exact commit scope is documented in commit-inventory.md; all raw/private evidence remains excluded.
