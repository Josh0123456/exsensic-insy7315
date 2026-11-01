# Database migrations

How the schema changes over time. Owner: Dean (P3). Related: [operations.md](operations.md) section 4 (rolling back), [CONTRACTS.md](CONTRACTS.md) section 9 (settings).

## Current state

There is one migration, `20261002163643_InitialCreate`. It creates the whole first schema: the Identity tables, the catalogue, bookings, requirements, history and notifications, with the filtered unique index that blocks double bookings and the check constraints. The API applies pending migrations on start-up in Azure (`Database:MigrateOnStartup`), so staging and production both already record `InitialCreate` as applied.

## Decision: keep `InitialCreate`, change the schema in small steps from now on

The Task 2 feedback asked for the single migration to be split into incremental ones. We keep the existing migration and make every later change its own migration, for these reasons:

- **It is already applied.** Each database stores the migration id in `__EFMigrationsHistory`. Replacing `InitialCreate` with several new migrations gives EF ids it has never seen, so it would try to create tables that already exist and fail.
- **Re-baselining costs data.** Splitting it properly means dropping and recreating, or hand-editing the history table of, the staging and production databases, which hold the seeded accounts and demo bookings. That risk buys no new behaviour.
- **The rule already exists.** CONTRIBUTING.md says never edit or delete a merged migration, and [operations.md](operations.md) says to fix forward.

## Rules for every schema change

1. **One change, one migration.** A new column, index or constraint is its own migration, not added to an older one.
2. **Descriptive names** in PascalCase that say what changes, for example `AddBookingCancellationReason`.
3. **Never edit or delete a merged migration.** If it is wrong, fix forward with a new one.
4. **Review the generated SQL** before merging: `dotnet ef migrations script --idempotent --project src/Exsensic.Data --startup-project src/Exsensic.Api`.
5. **Include the model snapshot** (`ExsensicDbContextModelSnapshot.cs`) in the same commit.
6. **Migrations are created by Dean** (CONTRIBUTING.md) and use the commit scope `data`, for example `feat(data): add booking cancellation reason`.

## Commands

```bash
# create a migration (run from the repository root)
dotnet ef migrations add AddBookingCancellationReason --project src/Exsensic.Data --startup-project src/Exsensic.Api

# apply to the local database
dotnet ef database update --project src/Exsensic.Data --startup-project src/Exsensic.Api
```

## How a change reaches production

1. The migration is merged into `develop` through a pull request.
2. `ci` runs the integration tests against SQL Server, which applies every migration to a fresh database.
3. `cd-staging` deploys; the API applies the pending migration on start-up, and the smoke test and Playwright tests run against it.
4. The same migration reaches production in the next `release/vX.Y.Z` merge to `main`, after approval.

If a migration goes wrong, roll forward with a new migration. The database can also be restored from the 7-day point-in-time backup (operations.md section 4).
