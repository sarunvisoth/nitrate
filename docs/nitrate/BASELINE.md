# Nitrate Baseline (Prismedia 627958a, before any Nitrate code change)

Recorded 2026-09-26 in a Linux (Ubuntu 24.04, x86_64) Claude cloud session, on the tree produced by
foundation-plan Tasks 1–2 (Prismedia v3.7.2 + Nitrate docs/hygiene only). This is **not** a Windows or
AMD result; see [WINDOWS_HOST.md](WINDOWS_HOST.md) for owner-verified Windows steps.

Tasks 4–7 compare against these numbers. A test that already fails here is not a regression.

## Toolchain

| Tool | Version | How obtained |
|------|---------|--------------|
| .NET SDK | 10.0.112 | `apt-get install dotnet-sdk-10.0` (Ubuntu `noble-updates`). The `dot.net/v1/dotnet-install.sh` script could not download from `builds.dotnet.microsoft.com` in the cloud sandbox (proxy 403). All projects target `net10.0`; there is no `global.json`. |
| Node.js | 22.22.2 | preinstalled |
| pnpm | 10.30.3 | preinstalled (matches `packageManager`) |
| FFmpeg | 6.1.1 (Ubuntu build) | `apt-get install ffmpeg`. Linux build only; it does **not** prove `h264_amf` support. |
| PostgreSQL | 16.13 | preinstalled; started with `service postgresql start` |

## Test baseline

`pnpm install --frozen-lockfile` succeeded (pnpm reports ignored build scripts for `@swc/core` and `core-js`; harmless for tests).

| Suite | Command | Result |
|-------|---------|--------|
| Root unit | `pnpm test:unit` | **7/7 passed** (3 files) |
| Web (Svelte) | `pnpm test:web-svelte` | **1352/1352 passed** (242 files), after `svelte-kit sync` — see note 1 |
| Frontend lint/typecheck | `pnpm check` | **pass** — 5/5 turbo tasks, svelte-check 0 errors / 0 warnings |
| Generated client parity | `pnpm api:check` (dev API running) | **pass** — "Generated client is in sync with the backend." |
| Backend | `pnpm test:backend` | **4469/4470 passed, 1 failed** — see below |

Backend breakdown (final clean run, Postgres up and dev DB migrated):

| Project | Passed | Failed | Total |
|---------|-------:|-------:|------:|
| Prismedia.Domain.Tests | 190 | 0 | 190 |
| Prismedia.Application.Tests | 1137 | 0 | 1137 |
| Prismedia.Infrastructure.Tests | 2823 | 1 | 2824 |
| Prismedia.Api.Tests | 319 | 0 | 319 |

### Pre-existing failures (not fixed — inherited from Prismedia)

1. **`AcquisitionDownloadRemovalTests.RecoveryMigrationReopensOnlyCompletedImportedUsenetTransfers`** — deterministic.
   `Npgsql.PostgresException 42703: column "recovery_of_acquisition_id" of relation "acquisitions" does not exist`.
   The test migrates to the migration *before* the recovery migration, then seeds rows through the *current*
   EF model, which already includes `recovery_of_acquisition_id` (added later by
   `20260908235145_AddHeldAcquisitionAlternatives`). Test bug, not a runtime bug.
2. **`AutoIdentifyRunnerTests.CancelsProviderWhenNoCascadeProgressArrivesBeforeInactivityTimeout`** — timing-sensitive.
   Failed once under full-suite load (`Assert.Empty() Failure: Collection was not empty`), passed in isolation and in
   the final full run. Treat an occasional failure of this test as pre-existing flakiness.

### Environment notes that turn into false failures

1. **`pnpm test:web-svelte` on a fresh checkout:** all 242 files fail with
   `Cannot find module './.svelte-kit/tsconfig.json'`. Run `pnpm --filter @prismedia/web-svelte sync`
   (or `pnpm check`, which runs it) once first.
2. **Postgres not running:** 171 backend tests report as *failed* (xUnit dynamic skip surfaced as failure):
   `PostgreSQL test requires PRISMEDIA_TEST_DATABASE_URL or the local dev database`. They need a role
   `prismedia`/`prismedia` able to create databases (the Docker image's `POSTGRES_USER` is a superuser; locally we
   used `CREATE ROLE prismedia LOGIN SUPERUSER PASSWORD 'prismedia'` plus `CREATE DATABASE prismedia OWNER prismedia`).
   Override with `PRISMEDIA_TEST_DATABASE_URL`.
3. **Dev DB not migrated:** 5 `Prismedia.Api.Tests` (`OptionalEntityStateEndpointTests` ×4,
   `EntityPatchEndpointTests.EntityConsumptionPatchStoresPositionAndActiveTime`) return HTTP 500 because
   `WebApplicationFactory<Program>` uses the real `prismedia` database. Start the API once (it applies EF migrations on
   startup) and they pass.

## Running locally

This worked end to end in the cloud sandbox (Linux):

```bash
# 1. Database (local Postgres 16; Docker alternative: docker compose -f infra/docker/docker-compose.yml up -d postgres)
sudo service postgresql start
sudo -u postgres psql -c "CREATE ROLE prismedia LOGIN SUPERUSER PASSWORD 'prismedia';" \
                      -c "CREATE DATABASE prismedia OWNER prismedia;"

# 2. Frontend deps + SvelteKit sync
pnpm install --frozen-lockfile
pnpm --filter @prismedia/web-svelte sync

# 3. API (applies all EF migrations on first start, listens on http://0.0.0.0:8008)
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/backend/src/Prismedia.Api/Prismedia.Api.csproj

# 4. Worker
DOTNET_ENVIRONMENT=Development dotnet run --project apps/backend/src/Prismedia.Worker/Prismedia.Worker.csproj

# 5. Vite (the API proxies the UI to it in Development; open http://localhost:8008, not :5173)
pnpm dev
```

Configuration used (all defaults, no env vars needed):

- Connection string: `Host=localhost;Port=5432;Database=prismedia;Username=prismedia;Password=prismedia`
  (`apps/backend/src/Prismedia.Api/appsettings.json`, `ConnectionStrings:Prismedia`).
- Data/cache dirs in Development: `Prismedia:DataDir = ../../data`, `Prismedia:CacheDir = ../../data/cache`
  (`appsettings.Development.json`; resolves to the git-ignored `apps/backend/data/`). Production defaults are
  `/data` and `/data/cache` and are overridden with `PRISMEDIA_DATA_DIR` / `PRISMEDIA_CACHE_DIR`.
- ffmpeg/ffprobe found on `PATH` (override: `PRISMEDIA_FFMPEG_PATH`, `PRISMEDIA_FFPROBE_PATH`).

Observed: `GET /api/health` → `200 {"status":"ok","runtime":"dotnet"}`; `GET /` served the Svelte app through the API
(returns a "Waiting for Vite…" page until `pnpm dev` is up); `GET /setup` → 200 (first-run wizard). The worker logged
"Job scheduler started", "Library filesystem change monitor started". The first API start logs a few
`fail: … __EFMigrationsHistory` lines before creating the history table; these are expected on an empty database.
No library was scanned and no media was played in the sandbox.
