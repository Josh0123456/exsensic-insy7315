# Contributing to Exsensic — Team Rules (INSY7315 Task 2)

**Due: Monday 5 October 2026.** Feature freeze: **Saturday 3 October, 23:59.**
**Repo:** https://github.com/Josh0123456/exsensic-insy7315 · **Staging:** `<url>` · **Production:** `<url>`

Read sections 1–5 before your first commit. Then open your card in `Exsensic_Part2_Team_Plan.pdf`.

---

## 1. Who owns what

| Person | Role | Owns (folders) |
|---|---|---|
| **P1 Christopher Doyle** · ST10445478 | Frontend, UX & accessibility; demo director | `src/Exsensic.Web/**` except the Admin catalogue/users screens (P3) and the Notifications screen (P2) |
| **P2 Daniel Wilson** · ST10446445 | Backend API & business logic | `src/Exsensic.Core/**` (booking side), booking/admin/staff/notification controllers in Api, booking contracts, the Notifications screen, `tests/Exsensic.UnitTests` |
| **P3 Dean Sibley** · ST10440750 | Database, security & repo governance | `src/Exsensic.Data/**`, Core entities and catalogue logic, auth/catalogue/user controllers and `Security/` in Api, Admin catalogue/users screens, security tests, CODEOWNERS, Dependabot, `pr-checks.yml` |
| **P4 Josh Stockwell** · ST10440989 | DevOps, hosting, repo owner & release | `.github/workflows/**` (except `pr-checks.yml`), hosting config, `tests/Exsensic.E2ETests`, architecture tests, `README.md`, Azure |

The exact file-by-file list is in `docs/CONTRACTS.md` §6 and §8 and in each card.

`.github/CODEOWNERS` enforces this: your PR automatically requests a review from the owner of any file you touch.
**If you need to change someone else's file, say so in the group chat first.**

## 2. The contract

`docs/CONTRACTS.md` defines every shared name: roles, statuses, entities, DTOs, routes, error codes and config keys.
**Nobody renames anything in it on their own.** Change it only through the process at the top of that file.

## 3. Branches

```
main       production. Only release merges from develop. Protected.
develop    integration. Default branch. Protected. Every PR targets develop.
feat/pN-short-name      new feature        e.g. feat/p2-booking-create
fix/pN-short-name       bug fix            e.g. fix/p1-wizard-back-button
chore/pN-short-name     tooling/config     e.g. chore/p4-azure-config
docs/pN-short-name      documentation      e.g. docs/p3-security-summary
test/pN-short-name      tests only         e.g. test/p3-ownership-suite
ci/pN-short-name        workflows          e.g. ci/p4-staging-deploy
release/vX.Y.Z          release prep (Josh only)
```

- `pN` is your person number (p1–p4), so the history shows who built what.
- **Short-lived:** one branch per card step or small group of steps. Branch from an up-to-date `develop`, merge back **the same day it's green**.
- **Never commit directly to `develop` or `main`.** Branch protection blocks it anyway.

### The routine in GitHub Desktop — every time

1. **Fetch origin** → switch to `develop` → **Pull origin**.
2. **Branch → New branch** named as above, based on `develop`.
3. Do **one** commit-sized change (your card lists them).
4. Build and run the tests (`dotnet build` then `dotnet test`).
5. Commit with the exact message format below, under **your own** GitHub account.
6. Repeat 3–5 for the next step on the same branch if it belongs with it.
7. **Push origin** → **Create Pull Request** into `develop`. Fill in the template.
8. Wait for CI to go green. Ask one other person to review.
9. The **reviewer** merges with **"Create a merge commit"**. Never "Squash and merge", never "Rebase and merge" — they rewrite your commits and the rubric marks commit history.
10. Delete the branch. Back to step 1.

If you turn `develop` red, fixing it is your first job, immediately.

## 4. Commit messages — one format, no exceptions

```
type(scope): short description

- what changed and why (bullet 1)
- bullet 2
- bullet 3
```

**First line rules**

- **Lowercase** throughout. **Imperative mood** — `add`, `fix`, `remove`, `update`. Not `added`, `adds`, `fixed`.
- **Under 72 characters.** No full stop. No emojis.
- **Specific** — name the screen, endpoint, entity or workflow.

**Body:** anything bigger than a one-line change gets a blank line, then **2–5 bullets** starting with a
capital letter: what changed and why. Optional last line `Refs #12` to link the issue.

**Nothing else** in the message: no `Co-authored-by`, no "generated with" lines, no tool signatures.
Every commit is made by the person, under the person's own account.

| type | use for |
|---|---|
| `feat` | new behaviour, screen or endpoint |
| `fix` | a bug, wrong behaviour, broken layout |
| `refactor` | restructure with no behaviour change |
| `style` | visual polish: CSS, spacing, colours (not code formatting) |
| `test` | adding or changing tests only |
| `docs` | README, docs folder, comments-only changes |
| `ci` | GitHub Actions workflows |
| `chore` | tooling, packages, config, repo settings files |
| `perf` | measurable performance improvement |

| scope | area |
|---|---|
| `web` | MVC plumbing: controllers, api clients, program setup |
| `ui` | layout, design system, partials, CSS |
| `a11y` | accessibility |
| `auth` | identity, login, registration, profile |
| `catalog` | services, time slots, availability |
| `booking` | booking lifecycle, State and Observer patterns |
| `admin` | admin dashboard and management |
| `staff` | staff schedule and completion |
| `api` | cross-cutting API: errors, conventions, health |
| `data` | entities, DbContext, migrations, seed |
| `security` | headers, rate limits, authorisation, hardening |
| `deploy` | Azure, environments, release |
| `repo` | repo files: CODEOWNERS, templates, gitignore |
| `deps` | dependency updates (Dependabot uses this) |
| `demo` | demo script and presentation material |
| `readme` / `contract` | README / `docs/CONTRACTS.md` |

**Good**
```
feat(booking): add booking creation with double-booking protection

- Save booking, requirements and history in one transaction
- Map the filtered unique index violation to 409 slot_unavailable
- Validate requirement keys against the category template
```
**Bad:** `Updated stuff` · `fix: bug` · `Feat(Booking): Added booking.` · `wip` · `final final`

Target: **15+ real commits per person** across the week. Four bulk dumps on Sunday night lose GitHub marks.

**CI checks this.** The `pr-checks` workflow fails a pull request whose branch name or any commit subject
breaks these rules, and lists the offenders. If it's your **last** commit, fix it with GitHub Desktop's
*Amend last commit* and push again. If it's an earlier commit, tell Dean: he adds the
`commit-format-exception` label, which is recorded on the PR. Aim never to need it.

## 5. Rules that carry marks

- **Everything real by Saturday:** no mock data, no hard-coded lists in views, no fake login in the final build.
- **Secrets never in Git:** use `dotnet user-secrets` locally. Never commit `appsettings.*.json` with real values, `.env`, publish profiles or connection strings. Secret scanning push protection is on.
- **Security is enforced by the API,** not by hiding buttons in the UI.
- **The State pattern is the only place transitions are decided.** Nobody writes `booking.Status = ...` outside Core.
- **Migrations: Dean creates them.** Need a schema change? Ask Dean. Never edit or delete a merged migration.
- **Tests:** never delete, skip or weaken a test to make CI green. Fix the code or raise it in the chat.
- **Comments and references:** XML doc comments on public members; numbered references `[n]` for adopted code, matching the README.
- **Logging:** `ILogger<T>` only, no personal data in logs.

## 6. AI tools

- AI assistants may help write and explain code. **AI tools never commit, push, merge, or open PRs.** You review every change and commit it yourself.
- Your agent's rules file (`AGENTS.md`, `CLAUDE.md`, `.claude/`, `.cursor/`, `.codex/`) stays **on your machine only**. Add it to `.git/info/exclude`, not `.gitignore`:
  ```powershell
  Add-Content .git\info\exclude "`nAGENTS.md`nCLAUDE.md`n.claude/`n.cursor/`n.codex/`n.mcp.json`n.github/copilot-instructions.md"
  ```
  Check with `git status`: none of those files should appear.
- This hides tool config, **not the fact we used AI**. The final report's AI declaration names the tools and what they did, honestly and specifically.

## 7. Before your first commit

Reply in the group chat: **"P_ — [your role] — confirmed."** Roles are confirmed in writing, not assumed.
