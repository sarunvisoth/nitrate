# Nitrate Architecture Analysis (Prismedia base at `627958a`)

Evidence-based map of the Prismedia v3.7.2 codebase that Nitrate forked. Every claim cites a repo path and line, as
read on 2026-09-26 at the tree after foundation-plan Task 4 (which added the `Amf` transcoder profile). Anything asked
about that does not exist is marked **Not found**, with the searches that were run. Prismedia's own docs were used as
a map only; the code is the evidence. Line numbers go stale as Nitrate changes. Re-verify before relying on one.

Sections 1–18 answer the owner's questions, §19 covers native Windows readiness (used by
[WINDOWS_HOST.md](WINDOWS_HOST.md)), and §20 summarises the proposed annotation system (expanded in the M3 plan).

## Contents

- [1. Directory structure](#1-directory-structure)
- [2. Frontend technology](#2-frontend-technology)
- [3. Backend technology](#3-backend-technology)
- [4. Database](#4-database)
- [5. Authentication](#5-authentication)
- [6. API structure](#6-api-structure)
- [7. Media-library model](#7-media-library-model)
- [8. Scanning](#8-scanning)
- [9. FFmpeg/transcoding](#9-ffmpegtranscoding)
- [10. How books are represented](#10-how-books-are-represented)
- [11. EPUB rendering](#11-epub-rendering)
- [12. PDF rendering](#12-pdf-rendering)
- [13. Reading progress storage](#13-reading-progress-storage)
- [14. Existing annotations / bookmarks](#14-existing-annotations--bookmarks)
- [15. Apple-client/API architecture](#15-apple-clientapi-architecture)
- [16. Best extension points for our reader](#16-best-extension-points-for-our-reader)
- [17. Relevant licenses](#17-relevant-licenses)
- [18. Risks / limitations](#18-risks--limitations)
- [19. Native Windows readiness](#19-native-windows-readiness)
- [20. Proposed annotation system (summary)](#20-proposed-annotation-system-summary)

## 1. Directory structure

`AGENTS.md` "Project Structure" (`AGENTS.md:78-89`) lists the layout below. Every entry was checked on disk with `ls`.

| Path | What it is | Evidence |
|---|---|---|
| `apps/web-svelte/` | The Svelte frontend. It builds to static assets that the .NET API serves. | `AGENTS.md:81`, `apps/web-svelte/svelte.config.js:1,8-11` |
| `apps/backend/` | The .NET solution: `Prismedia.slnx`, `src/` (6 projects), `tests/` (4 test projects), `global.json` (SDK 10.0.201) | `apps/backend/Prismedia.slnx:2-15`, `apps/backend/global.json:3` |
| `packages/ui-svelte/` | The only workspace package: design tokens and UI primitives (`@prismedia/ui-svelte`) | `AGENTS.md:84`, `packages/ui-svelte/package.json:2`, `packages/ui-svelte/src/index.ts:1-15` |
| `infra/docker/` | `unified.Dockerfile`, `web.Dockerfile`, `docker-compose.yml`, `entrypoint.sh` | `AGENTS.md:86` |
| `scripts/` | `release/check-version.mjs`, `dev/kill-orphans.sh`, `dev/usenet-sim.mjs`, `docs-site.mjs` | `package.json:24,28-29` |
| `docs/` | Architecture and design docs, plus the Nitrate plans in `docs/nitrate/` (`VISION.md`, `ROADMAP.md`, `BASELINE.md`, `plans/`) | `AGENTS.md:8-9,88` |
| `documentation-site/` | Docusaurus 3.10 site (`@prismedia/documentation-site`). Developer guides live in `docs/developers/`. | `documentation-site/package.json:2,18`, `docs/app-codebase-flow.md:3-4` |
| `e2e/` + `playwright.config.ts` | Playwright smoke tests (`smoke.spec.ts`) that run against the API at `:8008` | `playwright.config.ts:3,9` |
| `tests/` | Root Vitest tests: `architecture/`, `fixtures/`, `support/` | `vitest.config.ts:6-9` |
| `nix/`, `flake.nix` | Nix dev environment and NixOS module | (directory listing) |

- **Tooling:** a pnpm workspace (`apps/*`, `documentation-site`, `packages/*`) driven by turbo (`pnpm-workspace.yaml:1-4`, `package.json:5,7,36`). The root scripts start the API and Worker with `dotnet watch` (`package.json:9-10`).
- **Doc mismatch:** `docs/architecture.md:35-38` describes a `packages/contracts` package. **Not found.** `ls packages` shows only `ui-svelte`.

## 2. Frontend technology

**Core stack** (`apps/web-svelte/package.json`):

| Item | Version / detail | Evidence |
|---|---|---|
| Svelte | `^5.55.9`, runes on | `apps/web-svelte/package.json:54`, `apps/web-svelte/svelte.config.js:16-18` |
| SvelteKit | `^2.61.0` | `apps/web-svelte/package.json:44` |
| Adapter | `@sveltejs/adapter-static` 3.0.10, SPA fallback to `index.html` | `apps/web-svelte/package.json:43`, `apps/web-svelte/svelte.config.js:8-11` |
| SSR | Off (`ssr = false` in the root layout) | `apps/web-svelte/src/routes/+layout.ts:10` |
| Build tooling | Vite `^6.4.2`, Tailwind CSS `^4.3.0` via `@tailwindcss/vite`, TypeScript `^5.9.3` | `apps/web-svelte/package.json:56-58`, `apps/web-svelte/vite.config.ts:6` |
| Dev proxy | `/api` and `/assets` go to `localhost:8008` | `apps/web-svelte/vite.config.ts:13-22` |

**Routing**
- Routing is SvelteKit file-based under `src/routes`. Routes include `books`, `movies`, `series`, `settings`, `setup`, `login`, `account`, `jobs`, `downloads` and others (directory listing).
- The root `+layout.ts` load does the auth and setup guard. It redirects to `/setup` when setup is needed, and to `/login?returnTo=…` when there is no user (`apps/web-svelte/src/routes/+layout.ts:44-54`).
- `apps/web-svelte/package.json:12` has a script `"start": "node build/index.js"`, which is adapter-node style. That script does not match the static adapter.

**State**
- State lives in rune-based `*.svelte.ts` stores in `src/lib/stores`, for example `session.svelte.ts` uses `$state` (`apps/web-svelte/src/lib/stores/session.svelte.ts:16-17`).
- **Not found:** svelte/store or any third-party state library. Searched the `package.json` dependencies.

**Tests**
- Unit: Vitest `^4.1.7` with jsdom, `@testing-library/svelte`, `@testing-library/jest-dom` and v8 coverage (`apps/web-svelte/package.json:47-48,50,52,59`, `vitest.config.ts:8-15`). `$app/*` modules are mocked (`vitest.config.ts:22-25`).
- End-to-end: Playwright at the repo root (`apps/web-svelte/package.json:20,32`).

**API client generation**
- orval 8.9.1 (`apps/web-svelte/package.json:53`) reads `http://127.0.0.1:8008/openapi/v1.json` (`apps/web-svelte/orval.config.ts:3-4`).
- It generates a split fetch client in `src/lib/api/generated/prismedia.ts` plus `model/` (`apps/web-svelte/orval.config.ts:12-15`), using a custom mutator `orvalFetch` (`apps/web-svelte/orval.config.ts:19-22`).
- `pnpm api:generate` then runs `scripts/gen-codes.mjs` (`apps/web-svelte/package.json:16`). `api:check` fails the build when the committed output is stale (`apps/web-svelte/scripts/check-generated.mjs:1-3`).
- The generated client is large: `prismedia.ts` is 12,766 lines and `model/` holds 527 files (measured with `wc` and `ls`).

**Notable libraries**

| Library | Use | Evidence |
|---|---|---|
| foliate-js | Vendored at `src/lib/vendor/foliate-js`, pinned commit `78914ae…`, MIT. Used only for reflowable EPUB; its `pdf.js` is stubbed. Loaded dynamically in `BookFileReader.svelte`. | `apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:7-9,13-21`, `apps/web-svelte/src/lib/components/BookFileReader.svelte:275` |
| pdfjs-dist `^6.0.227` | Dynamic import in `PdfReader.svelte` | `apps/web-svelte/package.json:33`, `apps/web-svelte/src/lib/components/PdfReader.svelte:473-474` |
| vidstack `^1.12.13` | Video player, including its HLS provider | `apps/web-svelte/package.json:36`, `apps/web-svelte/src/lib/components/VideoPlayer.svelte:33,42` |
| hls.js `^1.6.16` | Declared as a dependency | `apps/web-svelte/package.json:30` |
| jassub 2.4.2 | ASS subtitles; assets copied in a prebuild step | `apps/web-svelte/package.json:7,31`, `apps/web-svelte/src/lib/vendor/load-jassub.ts:2` |
| TipTap 3 + tiptap-markdown | Markdown editor | `apps/web-svelte/package.json:26-29,35`, `apps/web-svelte/src/lib/components/forms/MarkdownEditor.svelte:3-6` |
| svelte-dnd-action | Sidebar drag and drop | `apps/web-svelte/src/lib/components/Sidebar.svelte:16` |
| @pierre/trees | File tree | `apps/web-svelte/src/lib/components/files/FileTreePane.svelte:5` |
| marked | Markdown rendering | `apps/web-svelte/package.json:32` |

- hls.js: **Not found** as a direct import. Searched `grep "import.*hls"` in `src`. It appears to be consumed through vidstack.
- `packages/ui-svelte` depends on bits-ui `^2.16.3`, class-variance-authority, clsx, tailwind-merge and lucide (`packages/ui-svelte/package.json:37-42`). It exports its source directly (`packages/ui-svelte/package.json:6-26`).

## 3. Backend technology

The solution has 6 source projects and 4 test projects (`apps/backend/Prismedia.slnx:3-14`). All target `net10.0`.

**Project references**

| Project | SDK | References | Key packages |
|---|---|---|---|
| Domain | `Microsoft.NET.Sdk` | none | none (`apps/backend/src/Prismedia.Domain/Prismedia.Domain.csproj:1-9`) |
| Contracts | Sdk | Domain (`apps/backend/src/Prismedia.Contracts/Prismedia.Contracts.csproj:12`) | none |
| Application | Sdk | Domain, Contracts (`apps/backend/src/Prismedia.Application/Prismedia.Application.csproj:4-5`) | DI and Hosting abstractions (`:9-10`) |
| Infrastructure | Sdk | Application, Domain, Contracts (`apps/backend/src/Prismedia.Infrastructure/Prismedia.Infrastructure.csproj:4-6`) | EF Core 10.0.8, Npgsql.EFCore.PostgreSQL 10.0.1, Npgsql 10.0.2, Microsoft.Extensions.Identity.Core (password hashing), AngleSharp, PdfPig, PDFtoImage, SkiaSharp, VersOne.Epub, YamlDotNet (`:10-29`) |
| Api | `Microsoft.NET.Sdk.Web` | Application, Contracts, Domain, Infrastructure (`apps/backend/src/Prismedia.Api/Prismedia.Api.csproj:4-7`) | Microsoft.AspNetCore.OpenApi 10.0.9, Microsoft.OpenApi 2.7.5 (`:11-12`) |
| Worker | `Microsoft.NET.Sdk.Worker` | Application, Infrastructure (`apps/backend/src/Prismedia.Worker/Prismedia.Worker.csproj:15-16`) | Microsoft.Extensions.Hosting (`:11`) |

**Layering**
- The intended direction is Api/Worker → Infrastructure → Application → Domain (`docs/backend-architecture-contract.md:24-32`).
- The code matches, with two additions:
  - `Contracts` references `Domain`, so DTOs can carry `[Code]` enums (`apps/backend/src/Prismedia.Contracts/Prismedia.Contracts.csproj:10-12`).
  - `Application` also references `Contracts`.

**API hosting** (`apps/backend/src/Prismedia.Api/Program.cs`)
- Uses `WebApplication.CreateBuilder` (`:24`) with response compression, `AddOpenApi` and the app/infrastructure DI (`:47-75,95-96`).
- Runs a `TranscodeReaperService` hosted service inside the API (`:99`).
- Serves the built SPA from `PRISMEDIA_STATIC_WEB_ROOT` (`:26-33,115-126`) and `/assets` from the cache directory (`:128-145`).
- Startup order before `app.Run()`: database restore → migrations → user bootstrap (`:178-182`).
- The dev URL is `http://0.0.0.0:8008` (`Properties/launchSettings.json:8`).
- The database comes from `DATABASE_URL` or `ConnectionStrings:Prismedia` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:65-68`). The context is registered with pooling (`AddDbContextPool` + `UseNpgsql`, `:106-108`).

**Worker hosting** (`Prismedia.Worker/Program.cs`)
- Uses `Host.CreateApplicationBuilder` (`:7`).
- Runs as a system user context with no user identity (`:12-18`).
- Waits for any pending restore and for the migrated database before running (`:26-28`).
- Its hosted services are registered in `AddPrismediaWorkerApplication`: `QueueWorker`, `JobScheduler`, `JobHistoryPruner`, `MonitoredSearchWorker`, `LibraryFileChangeMonitor` and others (`apps/backend/src/Prismedia.Application/DependencyInjection.cs:105,128-136`).

**Docker**
- One image builds the web app with pnpm (`infra/docker/unified.Dockerfile:26`) and runs on `aspnet:10.0-noble` (`:64`).
- `entrypoint.sh` does three things:
  - initialises an embedded PostgreSQL (`infra/docker/entrypoint.sh:44`);
  - runs the Worker in a restart loop (`:100-109`);
  - `exec`s the API (`:120-129`).

## 4. Database

**DbContext**
- There is one: `PrismediaDbContext` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaDbContext.cs:9`).
- **Not found:** any other `DbContext`. Searched `grep ": DbContext"` over `apps/backend/src`.
- Model configuration is split into partial configuration files applied in `OnModelCreating` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaDbContext.cs:195-199`).
- `xmin` is used as the optimistic concurrency token on `JobRunRow`, `JobGraphRow`, `JobResourceStateRow`, `EntityRow`, `UserEntityStateRow` and `EntityConsumptionDayRow` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaDbContext.cs:205-225`).

**Entity sets** (grouped; all at `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaDbContext.cs:18-193`)

| Group | Tables |
|---|---|
| Core Entity model | `EntityKinds`, `Entities`, projections (`EntityAvailability`, `EntityRollups`, descendant/reference/collection counts), `EntityRelationshipLinks`, `EntityUrls`, `EntityExternalIds`, `EntityProviderIdentities`, `EntityAlternativeTitles`, `EntityMarkers`, `EntityDescriptions`, `EntityDates`, `EntityStats`, `EntityClassifications`, `EntityLifetimes`, `EntityPositions`, `EntitySources` (`:18-85`) |
| Files and media | `EntityFiles`, `EntityFileFingerprints`, `EntityLibraryRoots`, `EntityPageManifests`/`EntityPageEntries`, `EntitySubtitles`/`EntitySubtitleStates`, `EntityTechnical`, `MediaSources`, `MediaStreams`, `TrickplayInfos` (`:47-77`) |
| Kind details | Video series, gallery, book, book chapter, comic series and installment, `BookChapterAudioMappings`, `BookReadingChapters`, `BookContentStates`, audio track, person, tag, collection and collection item (`:87-113`) |
| Per-user engagement | `UserEntityStates`, `EntityConsumptionEvents`, `EntityConsumptionDays` (`:61-65`) |
| Library and scan | `LibraryRoots`, `MediaFileIgnores`, `ScannedFiles`, `LibraryFileChangeIntents` (`:115-121`) |
| System and security | `AppSettings`, `Users`, `UserSessions`, `UserLibraryAccess`, `UiPreferences`, `BrowserSessions`, `BrowserSessionSettings`, `ProviderConfigs`, `ProviderCredentials`, `DatabaseBackups` (`:123-147`) |
| Identify | `IdentifyResults`, `IdentifyQueueItems`, `FingerprintSubmissions` (`:141-145`) |
| Jobs and queue | `JobRuns`, `JobGraphs`, `JobDependencies`, `JobGraphSignals`, `JobResourceStates`, `JobResourceLeases`, `MediaProcessLeases` (`:149-161`) |
| Acquisition | Indexers, download clients, remote path mappings, credentials, `BookAcquisitionProfiles`, `CustomFormats`, `Acquisitions`, `ReleaseCandidates`, `DownloadTransfers`, cleanups, import hints, blocklist, history, `WantedSuppressions`, `Monitors` (`:163-193`) |

**Migrations**
- Location: `apps/backend/src/Prismedia.Infrastructure/Persistence/Migrations`.
- Count: 110 migration files (excluding `*.Designer.cs`), 107 `*.Designer.cs` files, and `PrismediaDbContextModelSnapshot.cs`.
- The first is `20260528134807_InitialPrismediaSchema.cs`; the last is `20260908235145_AddHeldAcquisitionAlternatives.cs` (directory listing).

**How migrations are applied at startup**
- The API calls `PrismediaMigrationRunner.ApplyPrismediaMigrationsAsync` (`apps/backend/src/Prismedia.Api/Program.cs:179`).
- The runner:
  - retries with backoff (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaMigrationRunner.cs:20-22,42-58`);
  - sets a 600-second command timeout (`:23,50`);
  - holds a PostgreSQL advisory lock (`pg_advisory_lock`) (`:92-122`);
  - validates migration history, then calls `MigrateAsync` (`:69-87`).
- It is skipped when `Prismedia:ApplyMigrations=false` or when running under the MVC test host (`:237-245`).
- The Worker never migrates. It only waits (`apps/backend/src/Prismedia.Worker/Program.cs:22-27`).

**Queue and job state in Postgres**
- There is no separate queue service (`docs/architecture.md:12`).
- Workers claim jobs with a SQL `FOR UPDATE OF candidate SKIP LOCKED` over `job_runs` and `job_graphs`, ordered by importance and fairness (`apps/backend/src/Prismedia.Infrastructure/Queue/JobQueueService.cs:555-577`).
- Tables: `job_graphs`, `job_dependencies`, `job_graph_signals`, `job_resource_states`, `job_resource_leases`, `media_process_leases` and `job_runs` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs:290,322,343,366,380,397,414`).

## 5. Authentication

**Mechanism: custom middleware, not the ASP.NET Core auth stack**
- **Not found:** `AddAuthentication`, `AddAuthorization`, `JwtBearer`, `CookieAuthentication`, `[Authorize]`, `RequireAuthorization`, `AllowAnonymous`. Searched `grep -rnE` for those terms over `apps/backend/src/**/*.cs`, excluding `bin` and `obj`.
- Instead, `app.UsePrismediaApiAuthentication()` (`apps/backend/src/Prismedia.Api/Program.cs:147`) adds inline middleware (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:30-79`).

**What requires a login**
- Everything under `/api` and `/opds` (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:130-136`).
- Exceptions: `/api/health`, the dev-only `/api/_codegen`, `GET /api/auth/setup-status`, `POST /api/auth/setup` and `POST /api/auth/login` (`:114-148`).

**How the token is read**
The token is looked for in this order:
1. `Authorization: Bearer` header (`:153-157,173-182`).
2. The `prismedia-session` HttpOnly cookie, ignored on `Sec-Fetch-Site: cross-site` requests (`:24,159-167`).
3. The `?access_token=` query parameter, for media renderers (`:169-170`, `apps/backend/src/Prismedia.Contracts/Security/AuthContracts.cs:8,11`).

OPDS requests also accept HTTP Basic credentials, which are checked on every request (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:40-59`).

**Tokens and sessions**
- Tokens are opaque 32-byte random values (`apps/backend/src/Prismedia.Application/Security/UserAuthService.cs:307-311`).
- They are stored SHA-256 hashed in `user_sessions.token_hash` (`:303-304`, `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs:145-157`).
- Sessions slide over a 90-day window (`apps/backend/src/Prismedia.Application/Security/UserAuthService.cs:21`). The cookie `MaxAge` matches (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:89-97`).
- Login returns `LoginResponse(AccessToken, User)`, described as "the bearer token for native clients" (`apps/backend/src/Prismedia.Contracts/Security/AuthContracts.cs:30-31`), and also sets the cookie (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:210-211`).
- Login can record optional `Client`, `DeviceName` and `DeviceId` (`apps/backend/src/Prismedia.Contracts/Security/AuthContracts.cs:23-28`).
- Passwords use ASP.NET Identity PBKDF2-HMAC-SHA512 with 210,000 iterations (`apps/backend/src/Prismedia.Infrastructure/Security/IdentityPasswordHasher.cs:13-20`).
- Failed logins are throttled per IP and username bucket, returning 429 (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:103-106,231-234`).

**Users and roles: multi-user**
- `UserRole` is `Admin` (full management, all libraries) or `Member` (only explicitly granted libraries) (`apps/backend/src/Prismedia.Domain/Entities/Enums/Core/UserRole.cs:8-15`).
- The `users` table has `role`, `allow_nsfw`, `can_create_libraries`, `can_request_content` and `enabled` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs:117-141`).
- `RequireAdmin()` and `RequireRequestPermission()` are endpoint filters (`apps/backend/src/Prismedia.Api/Security/AuthorizationEndpointFilters.cs:13-50`). For example, `/api/users` is admin-only (`apps/backend/src/Prismedia.Api/Endpoints/Users/UserEndpoints.cs:12-14`).

**Setup wizard / first user**
- `GetSetupStatusAsync` reports `NeedsSetup` while no enabled admin exists (`apps/backend/src/Prismedia.Application/Security/UserAuthService.cs:154-158`).
- `CreateFirstAdminAsync` creates the admin, or promotes an existing user with the same name, and signs them in (`:165-218`).
- The frontend redirects to `/setup` while setup is needed (`apps/web-svelte/src/routes/+layout.ts:44-46`).

**Recovery**
- On every API boot, while `PRISMEDIA_RECOVERY_PASSWORD` is set, the account named by `PRISMEDIA_RECOVERY_USERNAME` (default `admin`) is created or reset as an enabled admin with that password. Its active sessions are invalidated (`apps/backend/src/Prismedia.Infrastructure/Security/UserBootstrapRunner.cs:19-24,47-98`).
- A warning is logged telling the operator to unset the variable (`:100-104`).

**API keys**
- **Not found** for authentication. Searched `grep -rniE "api_?key|ApiKey"` in `Prismedia.Api`, `Application/Security`, `Infrastructure/Security` and `Contracts/Security`.
- The only hit is the OpenSubtitles provider key (`apps/backend/src/Prismedia.Api/Endpoints/Videos/VideoEndpoints.cs:131-138`).
- Native clients use the bearer session token that login returns.

**Per-user data scoping**
- `user_entity_states` is keyed on `(user_id, entity_id)` and holds favorite, rating, resume and progress fields (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaCapabilityModelConfiguration.cs:19-40`).
- `entity_consumption_events` and `entity_consumption_days` carry `user_id` (`:56-60,83-87`).
- Library visibility per user is stored in `user_library_access` `(user_id, library_root_id)` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs:32-34`). It is read through `ILibraryAccessReader` inside `CurrentUserContextHolder` (`apps/backend/src/Prismedia.Application/Security/CurrentUserContextHolder.cs:11,16`).
- `ICurrentUserContext` exposes `UserId`, `IsAdmin`, `AllowNsfw` and `IsSystem`. The Worker runs as the system context (`apps/backend/src/Prismedia.Application/Security/ICurrentUserContext.cs:11-37`).
- NSFW visibility has a per-user server ceiling (`apps/backend/src/Prismedia.Api/Endpoints/EndpointRouteBuilderExtensions.cs:55-80`).

## 6. API structure

**Registration pattern: minimal APIs with route groups**
- `MapPrismediaEndpoints` calls 36 per-area `Map*Endpoints` extension methods (`apps/backend/src/Prismedia.Api/Endpoints/EndpointRouteBuilderExtensions.cs:6-45`). Program.cs calls it at `apps/backend/src/Prismedia.Api/Program.cs:157`.
- Each area file uses `MapGroup("/api/<area>").WithTags(...)`, for example `apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:17-18`.
- Endpoints declare `.WithName(...)`, which becomes the OpenAPI operationId, and `.Produces<T>()` (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:26-28,86-90`).
- Kind-specific catalog routes such as `/api/movies` are generated by the shared helper `MapEntityKindRoutes` (`apps/backend/src/Prismedia.Api/Endpoints/Movies/MovieEndpoints.cs:9-15`, `apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityKindRouteEndpoints.cs:12`).
- OPDS routes are mapped at `/opds` (`apps/backend/src/Prismedia.Api/Endpoints/Opds/OpdsEndpoints.cs:21-47`, `apps/backend/src/Prismedia.Contracts/Opds/OpdsProtocol.cs:8`).
- There are about 247 `.MapGet/Post/Put/Patch/Delete/Methods(` calls under `Endpoints/` (grep count).
- Unmatched routes return an `ApiProblem` 404, or the SPA shell for page routes (`apps/backend/src/Prismedia.Api/Program.cs:159-176`).

**OpenAPI generation**
- Uses built-in `Microsoft.AspNetCore.OpenApi` (`AddOpenApi`) with nested-type schema ids and `CodecEnumSchemaTransformer`, so `[Code]` enums appear as typed string enums (`apps/backend/src/Prismedia.Api/Program.cs:56-75`).
- `MapOpenApi()` and the codegen manifest (`/api/_codegen/...`) are mapped **only in Development** (`apps/backend/src/Prismedia.Api/Program.cs:106-108`, `apps/backend/src/Prismedia.Api/Codegen/CodegenEndpoints.cs:11,18`).
- orval reads `/openapi/v1.json` (`apps/web-svelte/orval.config.ts:3-4`).
- **Not found:** a committed OpenAPI spec file or a production OpenAPI endpoint. Searched the `MapOpenApi` call sites; there is one, and it is inside the Development guard.

**Frontend generation**
- orval writes `src/lib/api/generated/prismedia.ts` (fetch functions such as `getSetupStatus` and `login`, `apps/web-svelte/src/lib/api/generated/prismedia.ts:1618,1715`) and `model/`.
- `scripts/gen-codes.mjs` fetches `/api/_codegen/codes.json` and writes `generated/codes.ts` with `as const` objects for `[Code]` enums, capability discriminators and setting keys (`apps/web-svelte/scripts/gen-codes.mjs:1-7,16-17,28-38`).
- Both steps need a running dev API.

**How the frontend calls the API**
- Hand-written wrappers in `src/lib/api/*.ts` import the generated functions and models, for example `apps/web-svelte/src/lib/api/auth.ts:16,25-26` and `apps/web-svelte/src/lib/api/entities.ts:9,24-25`.
- All calls go through `orvalFetch`/`fetchApi`, which:
  - prefix `API_BASE` (`/api`, or `PUBLIC_API_URL`) (`apps/web-svelte/src/lib/api/orval-fetch.ts:4,55-58`);
  - send the same-origin cookie (`:88,140`);
  - handle a 401 once globally by redirecting to `/login?expired=1` (`:40-47`).

**Versioning**
- **No `/api/v1` prefix exists.** Routes are unversioned `/api/<area>`, as the `MapGroup` list shows (for example `/api/auth`, `/api/entities`, `/api/videos`).
- Searched `grep -rn '"/api/v\|/v1/\|v1"'` in `Prismedia.Api`. The only "v1" is the OpenAPI document name `openapi/v1.json` (`apps/web-svelte/orval.config.ts:4`).
- Compatibility is kept by never changing existing endpoints (`AGENTS.md:20-21`) and by kind-prefixed alias routes over `GET /api/entities/{id}` (`docs/architecture.md:89-91`).

## 7. Media-library model

**EntityKind (a closed enum with 22 values).** The values are `Audio, AudioLibrary, AudioTrack, Book, BookVolume, BookChapter, ComicInstallment, ComicSeries, ComicVolume, Collection, Gallery, Image, MusicArtist, BookAuthor, Person, Movie, Studio, Tag, Video, VideoEpisode, VideoSeries, VideoSeason` (`apps/backend/src/Prismedia.Domain/Entities/Kinds/EntityKind.cs:8-82`). According to its doc comment, codes, metadata and construction are kept in discovered definitions rather than in enum attributes (`apps/backend/src/Prismedia.Domain/Entities/Kinds/EntityKind.cs:3-7`).

**Definitions.**
- `EntityKindDefinition` is abstract. Its constructor takes kind, code, display name, category, `EntityStorageShape`, presentation, navigation, search and behavior, plus an optional CLR type and a default-capability factory (`apps/backend/src/Prismedia.Domain/Entities/Kinds/EntityKindDefinition.cs:12-37`).
- There are three typed bases:
  - `EntityKindDefinition<TEntity>` (`apps/backend/src/Prismedia.Domain/Entities/Kinds/EntityKindDefinition.cs:332`)
  - `RootEntityKindDefinition<TEntity>` (`:531`)
  - `PlayableVideoEntityKindDefinition<TEntity>`, which implements `IPlayableVideoKindDefinition` (`apps/backend/src/Prismedia.Domain/Entities/Kinds/PlayableVideoEntityKindDefinition.cs:45`)
- `EntityStorageShape` has five values: `None`, `Folder`, `File`, `Archive` and `ArchiveEntry` (`apps/backend/src/Prismedia.Domain/Entities/Enums/Core/EntityStorageShape.cs:6-26`).
- `EntityKindRegistry.Discover()` finds definitions by reflection over the Domain assembly. It rejects duplicate kinds, codes and CLR types, and it throws at startup if any enum value has no definition (`apps/backend/src/Prismedia.Domain/Entities/Kinds/EntityKindRegistry.cs:129-155`).

The concrete definitions found are listed below. All are under `apps/backend/src/Prismedia.Domain/`.

| Kind | Definition (file:line) | Base |
|---|---|---|
| Audio | `apps/backend/src/Prismedia.Domain/Entities/Kinds/AudioEntityKindDefinition.cs:7` | non-generic `EntityKindDefinition`; "protocol-level audio root" (`:3-6`) |
| Person / Tag | `apps/backend/src/Prismedia.Domain/Taxonomy/Person.cs:9`, `apps/backend/src/Prismedia.Domain/Taxonomy/Tag.cs:9` | `EntityKindDefinition<T>` |
| Studio | `apps/backend/src/Prismedia.Domain/Taxonomy/Studio.cs:7` | `RootEntityKindDefinition<T>` |
| Movie, Video, VideoEpisode | `apps/backend/src/Prismedia.Domain/Media/Movie.cs:7`, `apps/backend/src/Prismedia.Domain/Media/Video.cs:7`, `apps/backend/src/Prismedia.Domain/Media/Video.cs:51` | `PlayableVideoEntityKindDefinition<T>` |
| VideoSeries / VideoSeason | `apps/backend/src/Prismedia.Domain/Media/VideoSeries.cs:10` / `:100` | generic / root |
| Book, BookChapter | `apps/backend/src/Prismedia.Domain/Media/Book.cs:10`, `apps/backend/src/Prismedia.Domain/Media/BookChapter.cs:7` | generic |
| BookVolume, BookAuthor | `apps/backend/src/Prismedia.Domain/Media/BookVolume.cs:8`, `apps/backend/src/Prismedia.Domain/Media/BookAuthor.cs:8` | root |
| ComicSeries, ComicInstallment | `apps/backend/src/Prismedia.Domain/Media/ComicSeries.cs:10`, `apps/backend/src/Prismedia.Domain/Media/ComicInstallment.cs:9` | generic |
| ComicVolume | `apps/backend/src/Prismedia.Domain/Media/ComicVolume.cs:8` | root |
| Gallery / Image | `apps/backend/src/Prismedia.Domain/Media/Gallery.cs:11` / `apps/backend/src/Prismedia.Domain/Media/Image.cs:7` | generic / root |
| AudioTrack / AudioLibrary / MusicArtist | `apps/backend/src/Prismedia.Domain/Media/AudioTrack.cs:9` / `apps/backend/src/Prismedia.Domain/Media/AudioLibrary.cs:8` / `apps/backend/src/Prismedia.Domain/Media/MusicArtist.cs:8` | generic / root / root |
| Collection | `apps/backend/src/Prismedia.Domain/Media/Collection.cs:10-17` (StorageShape `None`) | generic |

**Entity base types.**
- `Entity` is abstract. It holds the definition, `Id` (Guid), `Title`, `ParentEntityId`, `SortOrder`, capabilities, structural children, non-structural relationships, URLs, external IDs and files (`apps/backend/src/Prismedia.Domain/Entities/Entity.cs:8-94`).
- The constructor checks that the CLR type matches `definition.ClrType` (`apps/backend/src/Prismedia.Domain/Entities/Entity.cs:36-40`).
- Concrete entities inherit `Entity<TDefinition>`, which resolves its definition through `EntityKindRegistry.Get<TDefinition>()` (`apps/backend/src/Prismedia.Domain/Entities/Entity.cs:477-497`). Examples are `Movie : Entity<MovieEntityKindDefinition>` (`apps/backend/src/Prismedia.Domain/Media/Movie.cs:77`), `Video` (`apps/backend/src/Prismedia.Domain/Media/Video.cs:41`) and `VideoEpisode` (`apps/backend/src/Prismedia.Domain/Media/Video.cs:110`).
- The architecture doc describes the public read ladder `IEntityRef → IEntitySummary → IEntityDocument` (`docs/architecture.md:74-81`) and says `ParentEntityId` is the one structural hierarchy (`docs/architecture.md:106-111`).

**Shared identity: one table, but not EF TPH.**
- Every kind is stored in one `entities` table as a single non-polymorphic row class, `EntityRow`, with a `kind_code` string column (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityRow.cs:3-8`; `apps/backend/src/Prismedia.Infrastructure/Persistence/BaseEntityModelConfiguration.cs:24-37`).
- `kind_code` is a foreign key to an `entity_kinds` lookup table. That table is seeded from `EntityKindRegistry.All` (`apps/backend/src/Prismedia.Infrastructure/Persistence/BaseEntityModelConfiguration.cs:9-21`, FK at `:71-74`).
- `parent_entity_id` is a self-FK with `SetNull` on delete (`apps/backend/src/Prismedia.Infrastructure/Persistence/BaseEntityModelConfiguration.cs:75-78`).
- The persistence layer does not use EF inheritance mapping. A search for `HasDiscriminator`, `UseTphMappingStrategy` and `UseTptMappingStrategy` in `Prismedia.Infrastructure` returned **Not found**. The row-to-domain mapping is therefore code-driven by `kind_code`.
- Kind-specific data lives in 1:1 detail tables keyed by `EntityId`:
  - `video_series_details`, `gallery_details`, `comic_series_details`, `comic_installment_details`, `book_details`, `book_chapter_details` and `audio_track_details` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaMediaModelConfiguration.cs:10,18,36,47,70,92,177`)
  - the row classes are in `apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/MediaDetailRows.cs:5-60`
- Shared capability tables include `entity_descriptions`, `entity_technical`, `entity_sources`, `entity_positions` and `entity_file_fingerprints` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaCapabilityModelConfiguration.cs:10,131,150,163,201`).

**Files and roles.**
- The domain `EntityFile(Role, Path, MimeType)` is defined at `apps/backend/src/Prismedia.Domain/Entities/EntityFile.cs:9`.
- `EntityFileRole` has 13 roles: `source, thumbnail, grid-thumbnail, grid-thumbnail-2x, poster, backdrop, logo, preview, sprite, trickplay, waveform, cover, hls` (`apps/backend/src/Prismedia.Domain/Entities/Enums/Media/EntityFileRole.cs:6-65`).
- These are persisted in `entity_files`, and a unique index on `(entity_id, role)` allows only one file per role per entity (`apps/backend/src/Prismedia.Infrastructure/Persistence/EntityAttachmentModelConfiguration.cs:86-102`).
- There is a second, non-unique `(role, path)` index for exact-path owner lookups (`:106`).
- The `source` column (`scan` or `custom`) stops scans from overwriting custom assets (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityFileRow.cs:24-29`).
- Whether an entity counts as "source-backed" is derived from `EntityFileRole.Source` on the entity or any descendant (`docs/architecture.md:108-111`).

**Library roots.**
- `library_roots` has columns `path` (unique), `label`, `enabled`, `recursive`, `scan_videos`, `scan_images`, `scan_audio`, `scan_books`, `is_nsfw`, `auto_identify` and `last_scanned_at` (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/LibraryRootRow.cs:3-38`; `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs:9-29`).
- Direct root ownership is recorded in `entity_library_roots(entity_id PK, library_root_id)` (`apps/backend/src/Prismedia.Infrastructure/Persistence/EntityAttachmentModelConfiguration.cs:70-83`). The docs say the most specific root wins when roots overlap (`docs/architecture.md:64-66`).
- Related tables are `user_library_access` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs:31-40`), `media_file_ignores` (`:42-54`) and the scan snapshot `scanned_files(library_root_id, scan_kind, path)` with size and mtime (`:56-67`).

**Relationships.**
- Structural hierarchy is modeled only by `parent_entity_id`.
- Non-structural links are stored in `entity_relationship_links`. The primary key is `(entity_id, relationship_code, target_entity_id)`, and the table also holds `target_kind_code`, `sort_order` and a `metadata_json` jsonb column. There are cascade FKs to both ends (`apps/backend/src/Prismedia.Infrastructure/Persistence/EntityRelationshipModelConfiguration.cs:9-37`).
- The docs say these links are "persistence structures, not a global `EntityGraph` runtime" (`docs/architecture.md:68`).

## 8. Scanning

**Worker host.**
- `Prismedia.Worker/Program.cs` is a generic host. It registers the application, the worker application and the infrastructure, and it runs as the system user context (`apps/backend/src/Prismedia.Worker/Program.cs:7-18`).
- On startup it waits for any pending restore and for the database to be migrated, because the API owns migrations (`:22-28`).
- `AddPrismediaWorkerApplication` registers every discovered handler type and these hosted services (`apps/backend/src/Prismedia.Application/DependencyInjection.cs:105-138`):
  - `QueueWorker`
  - `JobScheduler`
  - `JobHistoryPruner`
  - `LibraryFileChangeMonitor`
  - `WorkerHeartbeatPublisher`
  - `EntityLifecycleRecoveryWorker`
  - `MonitoredSearchWorker`
  - `EntityAvailabilityReconciliationWorker`
  - `EntityAssetRowSweepWorker`

**Job types and definitions.**
- `JobType` has 42 codes (`apps/backend/src/Prismedia.Domain/Entities/Enums/Operations/JobType.cs:9-197`). They include:
  - scans: `scan-library/gallery/book/comic/audio`
  - `reconcile-entity`
  - probes: `probe-video/audio`
  - fingerprints: `fingerprint-video/image/audio`
  - generation: `generate-preview/trickplay/image-thumbnail/grid-thumbnail/book-cover-thumbnail/audio-waveform`
  - subtitles, identify and acquisition jobs
  - `database-backup`
- Each handler declares its type with `[JobDefinition]`. The attribute carries `ResourceClass` (Light by default), `Importance`, `SingletonBehavior` and `BlocksAutoIdentify` (`apps/backend/src/Prismedia.Application/Jobs/JobDefinitionAttribute.cs:9-27`).
- `JobDefinitionRegistry` discovers handlers by reflection and fails when a handler has no definition (`apps/backend/src/Prismedia.Application/Jobs/JobDefinitionRegistry.cs:11-54`).
- Examples:
  - `ProbeVideo` is `StandardCpu` (`apps/backend/src/Prismedia.Application/Jobs/Handlers/Probe/ProbeVideoJobHandler.cs:12`)
  - `GeneratePreview` is `HeavyCpu, BestEffort` (`apps/backend/src/Prismedia.Application/Jobs/Handlers/Generate/GeneratePreviewJobHandler.cs:13`)
  - `GenerateTrickplay` is `HeavyCpu, Deferred` (`apps/backend/src/Prismedia.Application/Jobs/Handlers/Generate/GenerateTrickplayJobHandler.cs:12`)
  - `GenerateImageThumbnail` is `StandardCpu, BestEffort` (`apps/backend/src/Prismedia.Application/Jobs/Handlers/Generate/GenerateImageThumbnailJobHandler.cs:13`)
  - every scan type is `QueueWideWhenUntargeted` (`apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanLibraryJobHandler.cs:18`)

**Queue in PostgreSQL.**
- The tables are `job_graphs`, `job_dependencies`, `job_graph_signals`, `job_resource_states`, `job_resource_leases` and `job_runs` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs:290,322,343,366,380,414`).
- A node is claimed by a single SQL statement with `FOR UPDATE OF candidate SKIP LOCKED`. Ordering is by importance, then graph `last_dispatched_at`, then sequence (`apps/backend/src/Prismedia.Infrastructure/Queue/JobQueueService.cs:490-578`).
- There is no push notification. A search for `LISTEN`, `NOTIFY` and `pg_notify` under `Prismedia.Infrastructure` returned **Not found**. `QueueWorker` polls with `IdleDelay = 2s`, and it recovers stale leases after 2 minutes (`apps/backend/src/Prismedia.Application/Jobs/QueueWorker.cs:22-26`).
- Interactive graphs get the first claim (`apps/backend/src/Prismedia.Application/Jobs/QueueWorker.cs:109-122`).
- Capacity:
  - Interactive lanes are `clamp((cpus+1)/2, 1, 4)`.
  - CPU permits are `max(1, cpus-1)`.
  - Heavy work costs 2 permits and light work costs 0 (`apps/backend/src/Prismedia.Application/Jobs/AdaptiveJobCapacity.cs:8-31`).
  - This matches `docs/jobs.md:27,41`.

**Scheduler.**
- `JobScheduler` ticks every 60 seconds (`apps/backend/src/Prismedia.Application/Jobs/JobScheduler.cs:20,37`).
- It queues routine per-root scans when `now - LastScannedAt >= IntervalMinutes`, and deep integrity scans on `IntegrityIntervalHours` (`apps/backend/src/Prismedia.Application/Jobs/JobScheduler.cs:92-152`).
- `LibraryScanJobs` maps each root's flags to scan job types (`apps/backend/src/Prismedia.Application/Jobs/LibraryScanJobs.cs:22-28`).
- Every root scan shares one durable resource, `LibraryScan`, with `maxConcurrency: 1`, so scans run one at a time across the server (`apps/backend/src/Prismedia.Application/Jobs/LibraryScanJobs.cs:144-164`).

**Scan pipeline.**
- `ScanJobHandler` is the base class. It first builds a cheap size+mtime signature list for the root, then diffs it against the stored snapshot, and skips the detailed pass when nothing changed (`apps/backend/src/Prismedia.Application/Jobs/Handlers/ScanJobHandler.cs:9-28`).
- A `FileSignature` is `(Path, SizeBytes, ModifiedTicks)` with no hashing (`apps/backend/src/Prismedia.Application/Jobs/Scanning/FileSignature.cs:3-12`).
- Change-only runs load path intents from the ledger (`apps/backend/src/Prismedia.Application/Jobs/Handlers/ScanJobHandler.cs:198-232`).
- Discovery is a recursive walk (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/FileDiscoveryService.cs:132-188`). It uses `Directory.EnumerateFiles/EnumerateDirectories`, filters by extension, skips directories whose names start with `.` (`:171`), and silently skips `UnauthorizedAccessException` and `DirectoryNotFoundException` (`:183-187`).
- Extension sets are case-insensitive (`apps/backend/src/Prismedia.Application/Files/SupportedExtensions.cs:15-47`).
- After materialization, `ScanLibraryJobHandler` batches downstream needs and enqueues follow-up work through `VideoDownstreamJobPlanner` (`apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanLibraryJobHandler.cs:375-404`). The resulting order is probe, fingerprint, subtitle extraction, preview, trickplay, and grid thumbnail when there is no preview (`apps/backend/src/Prismedia.Application/Jobs/Handlers/EntityProcessingPlanRequests.cs:31-49`).

**File watching.**
- The mechanism is `FileSystemWatcher` combined with polling and durable coalescing, in `LibraryFileChangeMonitor` (`apps/backend/src/Prismedia.Application/Jobs/LibraryFileChangeMonitor.cs`).
- One watcher is created per enabled root that exists. It sets `IncludeSubdirectories = root.Recursive`, uses the `FileName|DirectoryName|LastWrite|Size` filters, and `InternalBufferSize = 64 KiB` (`:83-93`).
- Created, Changed, Deleted and Renamed events record `(rootId, fullPath)` with a timestamp (`:94-100,119-121`).
- The loop polls every 2 seconds. It flushes observations after a 5-second quiet period, rescans the list of roots every minute, and re-queues outstanding intents every 30 seconds (`:18-21,30-43`).
- Flushed paths go into the `library_file_change_intents` ledger through an `INSERT ... ON CONFLICT DO UPDATE` upsert (`apps/backend/src/Prismedia.Infrastructure/Jobs/EfLibraryFileChangeIntake.cs:28-35`; table at `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs:69-78`). Change-only scans are then queued per routed kind (`apps/backend/src/Prismedia.Application/Jobs/LibraryScanJobs.cs:77-108`).
- On an overflow or error, the handler logs and records the root path itself, which forces a whole-root reconciliation (`apps/backend/src/Prismedia.Application/Jobs/LibraryFileChangeMonitor.cs:101-107`).
- A root that is missing or cannot be watched is skipped. Scheduled integrity scans remain the fallback (`:79-81,110-115`; `docs/jobs.md:47`).
- The PollingFileWatcher option is **Not found**. A search for `UsePollingFileWatcher`, `DOTNET_USE_POLLING` and `PhysicalFileProvider` found only static-file providers in `apps/backend/src/Prismedia.Api/Program.cs:32,130`.

**Windows-relevant observations.**
- **Case sensitivity.** `FileSystemPathComparison` uses `OrdinalIgnoreCase` on Windows and `Ordinal` elsewhere (`apps/backend/src/Prismedia.Application/Files/FileSystemPathComparison.cs:9-14`). It is used for exclusions, deltas and descendant checks (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/FileDiscoveryService.cs:190-208`; `apps/backend/src/Prismedia.Application/Jobs/Handlers/ScanJobHandler.cs:451,468-470`).
- **Case-sensitive database lookups.** PostgreSQL text keys are case-sensitive, for example `scanned_files` PK `(root, kind, path)` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs:58`), the intents PK (`:71`) and `entity_files (role, path)` (`apps/backend/src/Prismedia.Infrastructure/Persistence/EntityAttachmentModelConfiguration.cs:106`). If the same file is reported with different casing on Windows, these lookups may not match. This is an inference from the schema and was not tested.
- **Case-sensitive in-memory key.** The watcher's observation key is `ConcurrentDictionary<(Guid, string), ...>` with the default ordinal comparer (`apps/backend/src/Prismedia.Application/Jobs/LibraryFileChangeMonitor.cs:22`). It is de-duplicated later with the platform comparer (`:152`).
- **Path separators.** Most code uses `Path.*` APIs. `LibraryScanPathRules` normalises `\` to `/` before prefix checks (`apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPathRules.cs:14,27,41-42`).
- **Hidden directories.** Only dot-prefixed directories are skipped. Windows `Hidden` or `System` attributes are not checked (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/FileDiscoveryService.cs:170-172`). Inaccessible folders such as `System Volume Information` are swallowed by the catch at `:183`.
- **Network drives.** There is no SMB- or UNC-specific handling. A case-insensitive search for `\bUNC\b|IsUnc|network drive|\\\\` in `Prismedia.Application/Jobs`, `Prismedia.Application/Files`, `Prismedia.Infrastructure/Jobs` and `Prismedia.Infrastructure/Media/Processing` returned **Not found**. Reliability relies on the overflow handler plus periodic scans.
- **Default data directory.** It is `/data` when unset (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:72-74`). On Windows that resolves to the current drive root, which is .NET path semantics and was not tested.

**Probe and thumbnail jobs.**
- `MediaProbeService` runs ffprobe through `ProcessExecutor` and parses its JSON output (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/MediaProbeService.cs:275-293`).
- Thumbnail, preview and waveform generation use `ThumbnailService` with `lowPriority: true` and `-threads = AssetGenerationThreads` (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/ThumbnailService.cs:169,425-445`).

## 9. FFmpeg/transcoding

**Deciding between direct play, remux and transcode.** The decision is made in `apps/backend/src/Prismedia.Application/Videos/VideoDirectPlayPolicy.cs` and called from `VideoPlaybackPlanService.CreatePlanAsync` (`apps/backend/src/Prismedia.Application/Videos/VideoPlaybackPlanService.cs:49-80`). The steps are:
1. **Invalid bit depth.** If the stream signals HDR but has fewer than 10 bits, the result is Transcode, unless the client supports local tone mapping (`apps/backend/src/Prismedia.Application/Videos/VideoDirectPlayPolicy.cs:64-68`).
2. **No device profile.** If the client sent no `DirectPlayProfiles`, the result is Direct only when `source.DirectPlayable`, direct play is allowed and the range is allowed; otherwise it is Transcode (`:72-77`). `DirectPlayable` depends only on the file extension: it is true for browser-native extensions such as `.mp4` and `.webm`, or for anything not in the transcode list such as `.mkv` and `.avi` (`apps/backend/src/Prismedia.Infrastructure/Videos/VideoSourceService.cs:14-28,154-157`).
3. **Device profile present.** Direct play requires a container, video codec and audio codec match, plus an allowed dynamic range and a bitrate at or below `MaxStreamingBitrate` (`:79-93`). Otherwise Remux is chosen when the client accepts the video codec in `mp4` or `ts`. Dolby Vision P5 or BL-0 can only be remuxed to clients that support DV (`:103-116`). Anything else is Transcode (`:118`).
4. **Other client inputs.** The client also sends `EnableDirectPlay`, `EnableDirectStream`, `EnableTranscoding`, `SupportedVideoRangeTypes` and `EnableClientToneMapping`. Choosing an explicit audio stream disables direct play (`apps/backend/src/Prismedia.Application/Videos/VideoPlaybackPlanService.cs:71-80`).
5. **URLs.** Remux is served at `v/remux/<stream playlist>` as stream-copy fMP4 HLS. Transcode is served from the HLS master playlist as `ts / h264 / aac` (`apps/backend/src/Prismedia.Application/Videos/VideoPlaybackPlanService.cs:111-137,153-172`). If remux timeline preparation fails, the plan falls back to Transcode (`:98-103`).

**Virtual HLS segments.**
- Segments are 6 seconds long, and the cache format is version 14 (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:21-22`).
- The variant playlist is a full VOD list generated from the probed duration before any segment exists, with entries `seg_%05d.ts` (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:714-740`).
- When a segment is requested:
  1. A cached file is returned if it exists.
  2. Otherwise, under a per-stream `SemaphoreSlim`, the service reuses an active generation within 8 segments of its start or frontier, or it cancels other generations and starts a new one at `segment-1` (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:335-383,403-425`, window constant at `:33`).
  3. It then polls the staging directory every 100 ms (`:34`; `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Generation.cs:218-260`).
- Each generation runs one ffmpeg process with `-ss <start>` and `-f hls -hls_flags temp_file -start_number N` (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:31-120`). Segments are copied to a canonical path through a temp file and `File.Move` (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Generation.cs:263-281`).
- A cache is invalidated by `metadata.json`, which records source path, size, mtime, renditions, profile and format version (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:284-323`; `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:398-408`).
- The master playlist advertises one rung unless `EnableAdaptiveBitrate` is on (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetServiceOptions.cs:55-59`; `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:170-183`).

**Transcoder profiles.**
- The enum is `Auto, Software, VideoToolbox, Vaapi, Nvenc, Qsv, Amf`, and `Amf` is documented as "AMD's h264_amf encoder on Windows" (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetServiceOptions.cs:6-27`). Parsing is case-insensitive with a fallback (`:39-44`).
- The UI setting lists all seven values, and `Amf` is labelled "AMD on Windows" (`apps/backend/src/Prismedia.Application/Settings/AppSettings.cs:295-306`).
- Encoder mapping: `h264_videotoolbox`, `h264_vaapi`, `h264_nvenc`, `h264_qsv`, `h264_amf`, and `libx264` as the default (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:171-178`).
- Only Software sets `-threads`, `-preset veryfast`, `-crf` and `-profile:v main` (`:186-203`). Hardware profiles get `-profile:v main` and a `-pix_fmt`: `nv12` for QSV, `yuv420p` for the others, and none for VAAPI. VideoToolbox also adds `-allow_sw 1` (`:204-218`). All profiles set `-b:v`, `-maxrate` and `-bufsize` (`:220-228`).
- **Auto resolution** (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:410-424`): macOS gets VideoToolbox, Linux gets Vaapi when the configured device file exists, and everything else gets Software. **On Windows, Auto never selects Nvenc, Qsv or Amf.** Users must choose those explicitly.
- **Fallbacks.** Any non-software failure is retried once with software x264. A "No such filter" error during tone mapping is retried without tone mapping (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Generation.cs:149-199`).

**Hardware acceleration flags.**
- For VAAPI: `-vaapi_device <device>` before the input (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:44-46`), plus the filter `format=nv12,hwupload,scale_vaapi=...` (`:138-145`).
- For VideoToolbox, `-hwaccel videotoolbox` is added only when tone mapping is needed (`:51-53`).
- No decode-side `-hwaccel` or `-init_hw_device` exists for NVENC, QSV or AMF. A search for `hwaccel|init_hw_device|qsv_device|cuda` across `apps/backend/src` found only `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:52`. **Not found** for those three profiles: decode and scaling run on the CPU, and only the encode is offloaded.

**Tone mapping.**
- `NeedsToneMapping` is true when the range classification is anything other than SDR (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:341-344`).
- `ResolveEffectiveTranscoderProfile` forces Software for HDR sources on every profile except VideoToolbox (`:326-339`). **This means Amf, Nvenc, Qsv and Vaapi all fall back to software for HDR.**
- The filter chain comes from `FfmpegToneMapping.BuildFilter` (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/FfmpegToneMapping.cs:26-48`):
  - Dolby Vision uses `tonemapx` (DV P5 path, `:34-44`). `tonemapx` is not a stock ffmpeg filter, so this assumes a Jellyfin-style ffmpeg build. That is an inference, and the fallback path covers it.
  - HDR10 and HLG use `zscale` plus `tonemap=hable` (`:46`).

**Resolving the ffmpeg and ffprobe paths.**
- The environment variable `PRISMEDIA_FFMPEG_PATH` takes precedence over `Prismedia:Hls:FfmpegPath`, and the same pattern applies to ffprobe (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:78-80`).
- If nothing is configured, the code scans `PATH` for `jellyfin-ffmpeg` and otherwise uses `ffmpeg` (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/MediaToolOptions.cs:37-49`). The PATH check is `File.Exists(dir/jellyfin-ffmpeg)` with no `.exe` probing (`:82-95`). **On Windows it therefore will not find `jellyfin-ffmpeg.exe`** and falls back to `ffmpeg`, which is an inference from that code.
- ffprobe comes from explicit configuration. Otherwise it is derived by replacing `ffmpeg` in the filename within the same directory, or by using `ffprobe.exe` when the name ends in `.exe` (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/MediaToolOptions.cs:58-80`).
- The runtime setting `hls.ffmpegPath` overrides the HLS ffmpeg path, but ffprobe still uses the startup value (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:426-441`).
- Background video tooling re-reads the same setting (`apps/backend/src/Prismedia.Infrastructure/Media/Adapters/MediaAssetGeneratorAdapter.cs:140-152`). It rebuilds `MediaToolOptions` with two arguments, so `AssetGenerationThreads` resets to its default of 1 (`:151`; default at `apps/backend/src/Prismedia.Infrastructure/Media/Processing/MediaToolOptions.cs:20`). As a result, `PRISMEDIA_ASSETGEN_THREADS` may be ignored on that path. This is likely a bug and was not verified at runtime.

**Thread caps.**
- For HLS software encoding, `hls.encodingThreadCount = 0` becomes `ProcessorCount-1`, and larger values are capped at `ProcessorCount` (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:447-450`; setting at `apps/backend/src/Prismedia.Application/Settings/AppSettings.cs:319-323`).
- Background generation uses `PRISMEDIA_ASSETGEN_THREADS`, default 1 (`DependencyInjection.cs:81-85`; `apps/backend/src/Prismedia.Infrastructure/Media/Processing/ThumbnailService.cs:425-426`).

**How ffmpeg is spawned.**
- All external processes go through `ProcessExecutor`, which uses `ProcessStartInfo(fileName)` with `UseShellExecute = false` and `ArgumentList.Add` for each argument, so no shell string is built (`apps/backend/src/Prismedia.Infrastructure/Processes/ProcessExecutor.cs:28-36,80-92,144-151,200-207`).
- On cancellation it calls `Kill(entireProcessTree: true)` (`:55-58`).
- Low-priority jobs get `PriorityClass = BelowNormal` (`:240-250`).
- A search for `new ProcessStartInfo` and `Process.Start` outside this file returned **Not found**. A search for `/bin/sh`, `bash`, `cmd.exe` and `UseShellExecute = true` also returned **Not found**.

**Concurrency limits and leases.**
- `ProcessExecutor` takes an admission lease only when the executable's filename, without extension, equals `ffmpeg` (`apps/backend/src/Prismedia.Infrastructure/Processes/ProcessExecutor.cs:252-267`).
  - Low-priority work acquires a background lease; everything else registers a playback lease.
  - **ffprobe is not gated.**
  - **A binary named `jellyfin-ffmpeg` is also not gated**, because the name does not equal `ffmpeg`.
- `MediaProcessAdmission` never blocks playback (`apps/backend/src/Prismedia.Infrastructure/Processes/MediaProcessAdmission.cs:54-66`). Background work loops until three conditions hold: the host has headroom, a local semaphore is free, and the shared Postgres lease is available (`:71-121`).
- The background cap is `clamp((cpus+7)/8, 1, 2)` (`apps/backend/src/Prismedia.Application/Jobs/AdaptiveJobCapacity.cs:21-22`; wired at `DependencyInjection.cs:119-124`).
- Leases are rows in `media_process_leases`, serialized with `pg_advisory_xact_lock`. They last 45 seconds with a 10-second heartbeat (`apps/backend/src/Prismedia.Infrastructure/Processes/PostgresMediaProcessLeaseStore.cs:13-15,47,104-134`).
- **Host load probing on Windows.** The probe reads `/proc/stat`, `/proc/loadavg` and `/proc/meminfo`, and returns `null` when they are absent. A `null` sample is treated as having headroom (`apps/backend/src/Prismedia.Infrastructure/Processes/HostLoadProbe.cs:58-71,93-103`). **On Windows, CPU, load and memory gating is effectively disabled**, leaving only the count cap and the playback check.
- There is no cap on concurrent playback transcodes. A search for `MaxConcurrentTranscode|TranscodeLimit|concurrentStreams` returned **Not found**; this matches `docs/jobs.md:43`.
- Abandoned jobs are cleaned up by `TranscodeReaperService`. It sweeps every 30 seconds, with a 120-second session TTL, a 60-second grace period and a 6-hour maximum lifetime (`apps/backend/src/Prismedia.Infrastructure/Videos/TranscodeReaperService.cs:26-39`). It is hosted in the API process (`apps/backend/src/Prismedia.Api/Program.cs:99`).

**Summary of Windows-relevant points.**
- The VAAPI device defaults to `/dev/dri/renderD128` in the options, the DI registration and the setting (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetServiceOptions.cs:68`; `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:353`; `apps/backend/src/Prismedia.Application/Settings/AppSettings.cs:310-313`). This is harmless on Windows because Auto only checks it under `OperatingSystem.IsLinux()` (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:419`), but explicitly choosing `Vaapi` on Windows would pass a Linux path to `-vaapi_device`.
- Auto never picks Amf, Nvenc or Qsv (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:410-424`).
- The `/proc`-based load probe does nothing on Windows (`apps/backend/src/Prismedia.Infrastructure/Processes/HostLoadProbe.cs:101-103`).
- The PATH lookup for `jellyfin-ffmpeg` ignores `.exe` (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/MediaToolOptions.cs:88-91`).
- `EnsureVirtualCacheUnderLockAsync` calls `Directory.Delete(root, recursive: true)` when the cache is stale (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:315`). On Windows this would throw if ffmpeg still holds a file open inside that directory. This is an inference from Windows file-locking semantics and was not tested.
- The only explicit `OperatingSystem.IsWindows()` branches in the backend are path comparison (`apps/backend/src/Prismedia.Application/Files/FileSystemPathComparison.cs:10,14`), hard links (`apps/backend/src/Prismedia.Infrastructure/Acquisition/HardLink.cs:18`), comic folder normalisation (`apps/backend/src/Prismedia.Infrastructure/Media/Comics/ComicFolderNormalizer.cs:424`) and subtitle sidecars (`apps/backend/src/Prismedia.Infrastructure/Media/Sidecars/SubtitleSidecarDiscovery.cs:211`).

## 10. How books are represented

**Formats and kinds (docs, verified in code)**
- Docs: the Books scanner handles comic archives `.cbz`/`.zip`, single-file books `.epub`/`.pdf`, and audiobooks `.m4b`/`.m4a`/`.mp3` (`documentation-site/docs/library/books.md:11-19`). Each `.epub`/`.pdf` becomes **one Book entity**, with no chapter or page entities (`documentation-site/docs/library/books.md:81`).
- Domain `BookFormat` has only `epub`, `pdf` and `audio` (`apps/backend/src/Prismedia.Domain/Entities/Enums/Books/BookFormat.cs:9-20`). `BookFormatTier` has `fixed` (PDF), `reflowable` (EPUB) and `archive` (CBZ/ZIP) (`apps/backend/src/Prismedia.Domain/Entities/Enums/Books/BookFormatTier.cs:11-26`).
- Entity kinds include `Book`, `BookVolume`, `BookChapter`, `ComicInstallment`, `ComicSeries`, `ComicVolume` and `BookAuthor` (`apps/backend/src/Prismedia.Domain/Entities/Kinds/EntityKind.cs:19-57`).
- MOBI/AZW3/CBR: there is **no reader or scanner support**. They show up only in acquisition import classification (`apps/backend/src/Prismedia.Application/Acquisition/AcquisitionImportFileLedger.cs:257`) and in MIME/OPDS maps (`.cbr` in `apps/backend/src/Prismedia.Infrastructure/Entities/EfEntityFileContentService.cs:74` and `apps/backend/src/Prismedia.Infrastructure/Opds/EfOpdsCatalogService.cs:799`). The vendored foliate-js still contains `mobi.js`/`fb2.js` (see §11), but Prismedia only ever opens EPUB with it.

**Book kind definition**
- `BookEntityKindDefinition` uses `EntityStorageShape.Archive`, `engagement: Reading`, `upgradeMode: AtomicBookFile`, and default capabilities `CapabilityStats`, `CapabilityProgress` and `CapabilityConsumption` (`apps/backend/src/Prismedia.Domain/Media/Book.cs:10-40`). Progress topology is `Work(EntityKind.Book)` (`apps/backend/src/Prismedia.Domain/Media/Book.cs:49`). A Book may be a root or a child of `BookAuthor` (`apps/backend/src/Prismedia.Domain/Media/Book.cs:52-53`).

**Files**
- `EntityFileRow` has these fields: `Id`, `EntityId`, `Role` (default `Source`), `Path`, `MimeType`, `SizeBytes`, `Source`, `CreatedAt` and `UpdatedAt` (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityFileRow.cs:5-33`). There is **no content-hash column** on this row. A separate `EntityFileFingerprintRow` exists (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityCapabilityRows.cs:151-158`), but I found no evidence that book scanning writes to it.
- A scanned EPUB/PDF gets one `EntityFileRow` with `Role = Source` (`apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPersistenceService.ScanUpserts.cs:509-518`).

**Serving to the reader**
- The route is `GET` / `HEAD /api/entities/{id:guid}/files/{role}` (`apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityFileEndpoint.cs:8-17`, group prefix `/api/entities` at `apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityEndpoints.cs:5`). It declares 200 and 206 responses (`apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityFileEndpoint.cs:11-12`).
- The file is resolved by `(EntityId, Role)` with `FirstOrDefault`, after a visibility check (`apps/backend/src/Prismedia.Infrastructure/Entities/EfEntityFileContentService.cs:24-36`).
- **Range requests are supported server-side**: `Results.File(..., enableRangeProcessing: true)` is used both for plain files and for archive members (`apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityFileResults.cs:54-60`, `:30-37`). The cache header is `private, max-age=3600`, and file URLs are "stable across file replacement (same id + role)" (`apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityFileResults.cs:12-18`).
- **The web client does not use ranges.** The EPUB reader `fetch`es the whole file into a `Blob` and wraps it in a `File` (`apps/web-svelte/src/lib/components/BookFileReader.svelte:278-285`). The PDF reader does the same with an `arrayBuffer` (`apps/web-svelte/src/lib/components/PdfReader.svelte:477-483`). The route builds `sourceUrl = /entities/${id}/files/source` (`apps/web-svelte/src/routes/books/[id]/reader/+page.svelte:137`), and `apiAssetUrl` prefixes it with `API_BASE` (`apps/web-svelte/src/lib/api/orval-fetch.ts:66-76`).

**`apps/web-svelte/src/lib/api/books.ts`**
- It contains only three wrappers: `fetchBookContents` (server-projected EPUB TOC, `apps/web-svelte/src/lib/api/books.ts:13-21`), `fetchBookChapterMappings` (`:23-31`) and `saveBookChapterMappings` (`:33-46`). Each calls generated Orval functions and `unwrapGenerated` (`apps/web-svelte/src/lib/api/books.ts:1-11`). The backend routes are `GET /api/books/{id}/contents` and `GET`/`PUT /api/books/{id}/chapter-mappings` (`apps/backend/src/Prismedia.Api/Endpoints/Books/BookEndpoints.cs:18-71`).

**Comics vs books**
- Comics are `ComicSeries`, then an optional `ComicVolume`, then a readable `ComicInstallment` whose pages are an ordered manifest inside the archive (`documentation-site/docs/library/books.md:21,59`).
- They open in `routes/entities/[id]/reader/+page.svelte`, which uses `ComicReader` and the `reader-manifest` / `reader-pages` API (`apps/web-svelte/src/routes/entities/[id]/reader/+page.svelte:5,13`). The backend routes are `GET /{id}/reader-manifest` and `GET /{id}/reader-pages/{ordinal}` (`apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityReaderEndpoints.cs:10-17`).
- Prose books (EPUB/PDF) open in `routes/books/[id]/reader/+page.svelte`. That page refuses anything without a readable EPUB/PDF (`+page.svelte:129-131`, and `hasReadableBookFile` at `apps/web-svelte/src/lib/api/capabilities.ts:177-180`). It then picks `PdfReader` or `BookFileReader` by `BookMetadata.format` (`+page.svelte:128,138-139,306-330`).

## 11. EPUB rendering

**Components**
- The route is `apps/web-svelte/src/routes/books/[id]/reader/+page.svelte`. It mounts `BookFileReader` with `contentType="application/epub+zip"`, `initialLocation`, `initialFraction`, `initialFlow`, `onLocationChange` and `onFlowChange` (`+page.svelte:317-330`).
- `apps/web-svelte/src/lib/components/BookFileReader.svelte` (542 lines) wraps `ReaderShell` (`apps/web-svelte/src/lib/components/reader/ReaderShell.svelte`) and `ReaderContentsSheet` (`apps/web-svelte/src/lib/components/BookFileReader.svelte:3,18,467-469`). `ReaderShell` renders everything inside a fullscreen `Dialog` from `@prismedia/ui-svelte` (`apps/web-svelte/src/lib/components/reader/ReaderShell.svelte:5,90-141`).

**foliate-js version and provenance**
- `apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:7-9` gives the source as `https://github.com/johnfactotum/foliate-js`, the pinned commit as `78914aef4466eb960965702401634c2cb348e9b1`, and the license as MIT. A `LICENSE` file is present. There is no `package.json`, `README` or `VERSION` in that folder; the listing contains only `LICENSE`, `PROVENANCE.md`, `*.js`, `ui/` and `vendor/`.
- One local modification is recorded: `pdf.js` is stubbed and `vendor/pdfjs/` was removed (`apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:16-21`). The stub throws `"PDF rendering is handled server-side, not by foliate-js"` (`apps/web-svelte/src/lib/vendor/foliate-js/pdf.js:1-6`).
- **Stale doc warning:** `apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:13-14` and the `apps/web-svelte/src/lib/vendor/foliate-js/pdf.js:1-2` comment say PDFs are "rendered to page images server-side and shown in the comic reader". Current code actually uses a pdfjs-dist client reader (§12).

**Vendored files** (from a directory listing)
- Root: `comic-book.js`, `dict.js`, `epub.js`, `epubcfi.js`, `fb2.js`, `fixed-layout.js`, `footnotes.js`, `mobi.js`, `opds.js`, `overlayer.js`, `paginator.js`, `pdf.js` (stub), `progress.js`, `quote-image.js`, `search.js`, `text-walker.js`, `tts.js`, `uri-template.js`, `view.js`
- `ui/`: `menu.js`, `tree.js`
- `vendor/`: `fflate.js`, `zip.js`

**What is actually imported**
- The app imports only `view.js`, dynamically (`apps/web-svelte/src/lib/components/BookFileReader.svelte:275`). No other non-vendor file imports foliate: a grep for `foliate` outside `vendor/` hits only `BookFileReader.svelte` and `foliate-vendor.d.ts`.
- `view.js` statically imports `epubcfi.js`, `progress.js`, `overlayer.js` and `text-walker.js` (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:1-4`). It dynamically imports:
  - `vendor/zip.js` (`:32`) and `epub.js` (`:84`, `:102`)
  - `paginator.js` or `fixed-layout.js` (`:255-259`)
  - `search.js` only from `search()` (`:546`) and `tts.js` only from `initTTS()` (`:587`); the app never calls either
  - `comic-book.js`, `fb2.js`, `pdf.js`, `mobi.js` and `fflate.js` on format-detection branches (`:91-117`)
- `apps/web-svelte/src/lib/foliate-vendor.d.ts:1-4` declares only `module "$lib/vendor/foliate-js/view.js"`, and says the vendor tree is excluded from type-checking. The view is held as `any` (`apps/web-svelte/src/lib/components/BookFileReader.svelte:71-73`).

**How the view is created**
1. Import `view.js`, which registers the `<foliate-view>` custom element (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:597`).
2. Fetch the book into a `File` (`apps/web-svelte/src/lib/components/BookFileReader.svelte:278-285`).
3. `document.createElement("foliate-view")` and append it to `stageEl` (`:288-291`).
4. `await view.open(file)` (`:293`).
5. Read `view.book.toc` and `view.isFixedLayout` (`:296-297`).
6. Add the `relocate` listener (`:298`).
7. `applyFlow` (`renderer.setAttribute("flow", ...)`, `:104-106`) and `applyStyles` (`renderer.setStyles(...)`, `:108-110`).
8. `view.init({ lastLocation: initialLocation ?? {fraction}, showTextStart })` (`:301-310`).

On teardown the component removes the listener and calls `view.close()` and `view.remove()` (`:319-329`).

**Events**
- The component listens **only to `relocate`** (`apps/web-svelte/src/lib/components/BookFileReader.svelte:298`). `handleRelocate` reads `detail.fraction`, `detail.cfi` and `detail.tocItem.label`, then calls `onLocationChange({cfi, fraction, label})` (`:159-168`).
- `view.js` also emits several events that the app **does not listen to**:
  - `load` with `{doc, index}` (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:339-349`)
  - `external-link` and `link` (`:360-365`)
  - `draw-annotation` with `{draw, annotation, doc, range}` (`:393`)
  - `show-annotation` with `{value, index, range}` (`:411`, `:428`)
  - `create-overlay` with `{index}` (`:418`)
- The relocate detail is built as `{...progress, tocItem, pageItem, cfi, range}`, where `cfi = getCFI(index, range)` (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:329-338`).

**Selection handling today: Not found**
- `BookFileReader.svelte` contains no `selectionchange`, `getSelection` or `load` handlers (full file read, lines 1-542).
- Only foliate's own internals touch selection. `paginator.js` has a `selectionchange` listener that turns the page during pointer or keyboard selection in paginated mode (`apps/web-svelte/src/lib/vendor/foliate-js/paginator.js:595-616`). `view.deselect()` also exists (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:486-489`).
- **Obstacle:** in paginated mode the component renders a full-stage gesture overlay, `.book-reader-gestures`, with `position:absolute; inset:0; z-index:5; touch-action:none` (`apps/web-svelte/src/lib/components/BookFileReader.svelte:417-426,517-522`). It captures pointer events above the iframe. My inference is that this prevents mouse or touch text selection in paged mode. The overlay is not rendered in scrolled mode (`:417`).
- The comment at `apps/web-svelte/src/lib/components/BookFileReader.svelte:194-197` notes that foliate's scroller sits in a closed shadow DOM. Both `apps/web-svelte/src/lib/vendor/foliate-js/view.js:214` and `apps/web-svelte/src/lib/vendor/foliate-js/paginator.js:429` use `attachShadow({ mode: 'closed' })`.

**Theming and settings**
- `contentStyles(percent)` injects a dark scheme, `font-size: {percent}%`, a transparent background, text color `#e7e7ea` and neutral link colors, all with `!important` (`apps/web-svelte/src/lib/components/BookFileReader.svelte:88-102`).
- Font size ranges from 80 to 200 in steps of 10 and is **not persisted** (`:66-67,77,119-124`).
- Flow is `paginated` or `scrolled` and is persisted through `progress.mode` (`+page.svelte:162,200`).
- Fixed-layout EPUBs hide the flow and font controls (`apps/web-svelte/src/lib/components/BookFileReader.svelte:82-83,362,389`).
- A tap/swipe zone model is shared with the comic reader via `comicTapZone` (`:19,258-266`).

## 12. PDF rendering

- `pdfjs-dist` is declared as `"^6.0.227"` (`apps/web-svelte/package.json:33`) and locked at `pdfjs-dist@6.0.227` (`pnpm-lock.yaml:6631`).
- The component is `apps/web-svelte/src/lib/components/PdfReader.svelte` (941 lines). It is a **separate PDF.js viewer, not foliate's adapter**:
  - It imports `pdfjs-dist/build/pdf.mjs` and the worker URL (`apps/web-svelte/src/lib/components/PdfReader.svelte:473-475`) and calls `getDocument({ data })` (`:483`).
  - foliate's `pdf.js` is a throwing stub (§11).
  - Type shims live in `apps/web-svelte/src/lib/pdfjs-dist.d.ts:1-4`.
- **A text layer is present.** `new pdfjsLib.TextLayer({... container: textLayerDiv})` is rendered into `.pdf-text-layer` (`apps/web-svelte/src/lib/components/PdfReader.svelte:319-329`). The wrapper is `canvas + textLayerDiv + linkLayer` (`:333`).
- In **paged** mode the text and link layers get `pointer-events: none`, so there is no selection there (`:842-847`). In scroll mode text is selectable; the docs describe "Continuous scroll with selectable text" (`documentation-site/docs/library/books.md:164`).
- PDF link annotations are read with `page.getAnnotations()` to build a custom link overlay (`apps/web-svelte/src/lib/components/PdfReader.svelte:398-436`). These are PDF-native link annotations, not user annotations.
- Page changes are reported through `onPageChange(page, pageCount)` (`apps/web-svelte/src/lib/components/PdfReader.svelte:40,360`).

## 13. Reading progress storage

**Backend**
- Endpoint: `PATCH /api/entities/{id}/progress`, body `EntityProgressUpdateRequest` (`apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityProgressEndpoint.cs:9-54`). With `Prefer: return=minimal` it returns 204; otherwise it returns the full `EntityCard` (`:15-35,59-63`).
- Request fields: `CurrentEntityId`, `Unit`, `Index`, `Total`, `Mode`, `Completed`, `Reset`, `Location` ("format-specific resume locator (e.g. an EPUB CFI)"), `ActivitySeconds`, `ActivityKind`, `UtcOffsetMinutes` (`apps/backend/src/Prismedia.Contracts/Entities/EntityUpdateRequests.cs:59-74`).
- Use case: `EntityCapabilityService.UpdateProgressAsync` and `UpdateProgressWithoutProjectionAsync` (`apps/backend/src/Prismedia.Application/Entities/EntityCapabilityService.Progress.cs:41-105`). They call `UpdateProgressOwnerAttemptAsync`, which:
  - checks visibility and resolves the progress owner (`apps/backend/src/Prismedia.Application/Entities/EntityCapabilityService.cs:486-500`)
  - trims `location` (`:558`)
  - calls `progress.TryMoveTo(...)` (`:586-595`)
- Domain: `CapabilityProgress` holds `CurrentEntityId`, `Unit`, `Index`, `Total`, `Mode`, `CompletedAt`, `UpdatedAt`, `ConsumedCount` and an opaque `Location` (`apps/backend/src/Prismedia.Domain/Capabilities/UserState/CapabilityProgress.cs:8-59`).
- `ProgressUnit` values are `item`, `page`, `chapter`, `track`, `cfi` ("index is approximate and the locator lives in Location") and `second` (`apps/backend/src/Prismedia.Domain/Entities/Enums/Media/ProgressUnit.cs:8-31`).
- **Per-user persistence.** `ProgressCapabilityMapper` reads and writes the current user's `user_entity_states` row, keyed by `currentUser.UserId`. Without a user it persists nothing (`apps/backend/src/Prismedia.Infrastructure/Entities/Mappers/Capabilities/ProgressCapabilityMapper.cs:8-63`).
- The `UserEntityStateRow` columns are `progress_*`, including `progress_location` (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityCapabilityRows.cs:17-45`). The composite PK is `(user_id, entity_id)` with cascade FKs to users and entities (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaCapabilityModelConfiguration.cs:18-53`).
- Reads go through `GET /api/entities/{id}` via the `progress` and `consumption` capabilities (`docs/consumption-tracking.md:59-61`).

**Locators per format (web)**

| Format | Unit | Index / total | Location | Mode | Evidence |
| --- | --- | --- | --- | --- | --- |
| EPUB | `cfi` | `round(fraction*10000)` / `10000` | foliate relocate CFI string | `scrolled` or `paged` | `apps/web-svelte/src/routes/books/[id]/reader/+page.svelte:189-206` |
| PDF | `page` | zero-based page / page count | none | always `scrolled` | `+page.svelte:216-233` |
| Comics | `page` | page / page count | none | reader mode; `reset` supported | `apps/web-svelte/src/routes/entities/[id]/reader/+page.svelte:142-159` |

- EPUB resume: the stored `location` is used only if it starts with `epubcfi(` (`apps/web-svelte/src/lib/entities/epub-contents.ts:66-70`). Otherwise it falls back to `index/total` as a fraction (`+page.svelte:148-167`).
- Launch locations that start with `{` or `[`, or contain `#prismedia-progress=`, are rejected as "native-only saved locator formats" (`apps/web-svelte/src/lib/entities/epub-contents.ts:72-79`). This strongly suggests the Apple apps write **non-CFI locators into the same `location` field**. That is an inference; their source is not in the repo.

**Sending cadence**
- There is **no debounce**. Every `relocate` triggers `queueEpubSave()` (`+page.svelte:178-182`), and every PDF page change triggers `queuePdfSave` (`:248-253`).
- Saves are serialized through a promise queue, `progressSaveQueue` (`:60,208-214,235-246`).
- Other triggers:
  - a heartbeat every 15 s carrying `activitySeconds` (`:87,106-114`)
  - `visibilitychange` (`:88-92`)
  - unmount (`:95-99`)
  - explicit close, which sends `completed` when the fraction is at least 0.995 (`:255-266`)
- The web wrapper sends `Prefer: return=minimal` and accepts 204 (`apps/web-svelte/src/lib/api/consumption.ts:46-88`).
- An `accessed` consumption event is recorded on open (`+page.svelte:77-82`).

**Apple apps**
- They are closed-source and not in this repo. "Existing HTTP endpoints they use must stay backward compatible" (`docs/nitrate/VISION.md:21-22`).
- Documented progress routes kept "stable" for existing clients are `/api/entities/{id}/playback`, `/playback/events`, `/progress` and `/api/playback/statistics` (`docs/consumption-tracking.md:57-70`). Generated Swift models are called the client source of truth (`docs/consumption-tracking.md:70`).
- Exactly which endpoints the apps call: **Not found** in the repo. I searched for `*.swift` across the repo (none), and `documentation-site/docs/using/native-apps.md` lists no endpoints.
- **Implication:** changing the semantics of `progress.location` or the `/progress` contract is risky for native clients. New annotation routes are additive and safe.

## 14. Existing annotations / bookmarks

Searches were case-insensitive `grep -rni` for `annotation`, `bookmark`, `highlight` and `\bnotes?\b` over `apps/backend/src` (`*.cs`, excluding `bin/`, `obj/`, `Migrations/`) and `apps/web-svelte/src` (excluding `/vendor/`).

**Backend: Not found (no feature).**
- `annotation` hits are unrelated comments: `apps/backend/src/Prismedia.Infrastructure/Acquisition/OwnedVideoEvidence.cs:12` ("no-tracking annotation") and `apps/backend/src/Prismedia.Domain/Entities/Enums/Codec/CodeFamilyAttribute.cs:5`.
- `bookmark` and `highlight`: zero hits.
- `notes?`: four unrelated comment hits, for example `apps/backend/src/Prismedia.Domain/Capabilities/Metadata/CapabilityDescription.cs:4` and `apps/backend/src/Prismedia.Application/Settings/SettingDefinition.cs:114`.
- In `Migrations/`, the only matches are EF's `HasAnnotation` (214 occurrences). No annotation, highlight or bookmark tables exist.

**Web (non-vendor): Not found (no feature).**
- `annotation` hits are a Select-option `annotation` label prop (`apps/web-svelte/src/lib/components/Select.test.ts:10`, `apps/web-svelte/src/lib/components/books/BookChapterMappingEditor.svelte:50`, `apps/web-svelte/src/lib/components/acquisitions/ManualImportReview.svelte:37`, `apps/web-svelte/src/lib/components/UiFoundationPreview.svelte:7`) and PDF link annotations (`apps/web-svelte/src/lib/components/PdfReader.svelte:398-436`).
- `bookmark` hits are only lucide icons: `apps/web-svelte/src/lib/components/entities/EntityGridPresetDropdown.svelte:2,52`, `apps/web-svelte/src/lib/requests/acquisition-status-display.ts:3,46` ("Wanted"), `apps/web-svelte/src/routes/ebooks/+page.svelte:2,10`, and `apps/web-svelte/src/lib/components/app-shell-nav-icon-map.ts:6,42`.
- `highlight` hits are UI states or CSS, for example `apps/web-svelte/src/lib/components/SearchResultCard.svelte:12` and `apps/web-svelte/src/lib/components/VideoPlayer.svelte:1804`.
- `notes` hits are unrelated, for example `apps/web-svelte/src/lib/components/ChangelogDialog.svelte:208` ("Release notes").

**Vendored foliate-js: annotation APIs present but unused**
- `view.addAnnotation(annotation, remove)` resolves `annotation.value` (a CFI) and asks the section's overlayer to draw by emitting `draw-annotation` with a `draw(func, opts)` callback (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:368-398`).
- `deleteAnnotation` is at `:399-401`.
- `showAnnotation` navigates to the annotation and emits `show-annotation` (`:421-430`).
- `#createOverlayer` wires a click hit-test that emits `show-annotation` and then `create-overlay` (`:406-420`).
- Supporting methods: `getCFI(index, range)` (`:431-435`), `resolveCFI` (`:436-445`), `goTo` returns `resolved` (`:460-470`), `deselect` (`:486-489`).
- `Overlayer` provides `add`, `remove`, `redraw` and `hitTest` (`apps/web-svelte/src/lib/vendor/foliate-js/overlayer.js:17-50`). Static draw styles are `underline`, `strikethrough`, `squiggly`, `highlight`, `outline` and `copyImage` (`apps/web-svelte/src/lib/vendor/foliate-js/overlayer.js:52,75,98,126,142,163`). `highlight` fills rects with `color` at opacity `var(--overlayer-highlight-opacity, .3)` (`apps/web-svelte/src/lib/vendor/foliate-js/overlayer.js:126-141`).
- The paginator dispatches `create-overlayer` after each section loads (`apps/web-svelte/src/lib/vendor/foliate-js/paginator.js:989-994`) and exposes the overlayer via `getContents()` (`apps/web-svelte/src/lib/vendor/foliate-js/paginator.js:1092-1098`).
- `fixed-layout.js` has **no** `create-overlayer` dispatch; a grep for `create-overlayer` / `overlayer` found no hits there. Annotations therefore only work for reflowable EPUB.
- `epubcfi.js` exports `isCFI`, `joinIndir`, `parse`, `collapse`, `compare`, `fromRange`, `toRange`, `fromElements`, `toElement`, `fake`, `fromCalibrePos` and `fromCalibreHighlight` (`apps/web-svelte/src/lib/vendor/foliate-js/epubcfi.js:9-346`).
- None of the above is referenced by app code. `BookFileReader.svelte` only uses `open`, `init`, `goTo`, `goLeft`/`goRight`, `close`, `book`, `renderer` and `isFixedLayout` (`apps/web-svelte/src/lib/components/BookFileReader.svelte:104-110,127-131,153,174-178,288-310,322-324`).

## 15. Apple-client/API architecture

**Context.** The iPhone/iPad/Apple TV apps are closed-source and live outside this repo. The Nitrate docs make this explicit: "Prismedia's iPhone/iPad/Apple TV apps are closed-source App Store apps, not in this repo. Existing HTTP endpoints they use must stay backward compatible" (`docs/nitrate/VISION.md:21-22`; also `AGENTS.md:20-21`, `docs/nitrate/ROADMAP.md:28`). The upstream developer docs name the native source as `Prismedia-SwiftUI/PrismediaShared` "in the sibling native repository" (`documentation-site/docs/developers/codebase-flow.md:59`).

### API surface a native client uses

- **No versioned API.** No `/api/v1` or `/api/v2` route prefix and no `ApiVersion` construct exist in Prismedia's own endpoints. Searched `apps/backend` and `apps/web-svelte/src` for `api/v[0-9]|ApiVersion|X-Prismedia|X-Client`. The only `/api/v1` hits are outbound URLs to third-party services, for example `apps/backend/src/Prismedia.Infrastructure/Subtitles/OpenSubtitlesProtocol.cs:5` and `apps/backend/src/Prismedia.Infrastructure/Acquisition/AcquisitionProtocol.cs:156`. Every Prismedia route is a flat `/api/...` path, such as `/api/auth` (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:17`). Stability is a policy, not a mechanism: "Do not change or remove them; add new endpoints instead" (`AGENTS.md:20-21`).
- **Contracts described as stable.** Some contract classes call themselves stable wire vocabulary:
  - `ApiAuthenticationProtocol`: "Stable wire vocabulary shared by Prismedia authentication clients" (`apps/backend/src/Prismedia.Contracts/Security/AuthContracts.cs:5-12`).
  - `VideoPlaybackProtocol`: "Stable route and query vocabulary", with `RoutePrefix = "/api/playback"` commented "Native playback API route prefix" (`apps/backend/src/Prismedia.Contracts/Playback/VideoPlayback.cs:5-8`).
- **Client capability negotiation (the real one) is for video playback.** `VideoPlaybackPlanRequest` is documented as "Client capabilities used to select a video delivery plan". It carries `EnableDirectPlay`, `EnableDirectStream`, `EnableTranscoding`, `EnableClientToneMapping`, `SupportedVideoRangeTypes`, and a `Profile` with `MaxStreamingBitrate` and `DirectPlayProfiles` (container/codec combinations) (`apps/backend/src/Prismedia.Contracts/Playback/VideoPlayback.cs:44-80`).
- **`apps/web-svelte/src/lib/api/capabilities.ts` is not client/device negotiation.** It holds typed accessors over the **entity capability** documents the server returns, for example `getCapability(capabilities, kind)` and `getRatingCapability` (`apps/web-svelte/src/lib/api/capabilities.ts:31-42`). Its backend counterpart is the polymorphic `EntityCapability` family. `CapabilityPolymorphism` discovers the subtypes by reflection and publishes stable discriminator kinds "so the code-generation surface can publish the same discriminators" (`apps/backend/src/Prismedia.Contracts/Entities/Capabilities/Core/CapabilityPolymorphism.cs:7-21`). Mutations go through `EntityCapabilityService` (`apps/backend/src/Prismedia.Application/Entities/EntityCapabilityService.cs:8-18`).
- **Code generation shared with the native app.** Upstream docs state "The native client generates closed codes, complete Entity-kind definitions, and request definitions from that same backend manifest" (`documentation-site/docs/developers/codebase-flow.md:368-369`). The OpenAPI document and the codegen manifest are mapped **only in Development** (`apps/backend/src/Prismedia.Api/Program.cs:106-108`). So the native app's generated models are a build-time snapshot, and any change to the shape of a contract or code can break the App Store build.
- **Other protocol surfaces.**
  - OPDS under `/opds`, using HTTP Basic (`apps/backend/src/Prismedia.Contracts/Opds/OpdsProtocol.cs:8,14`; handled at `apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:40-59`).
  - An unauthenticated health probe, `GET /api/health`, returns `("ok","dotnet")` (`apps/backend/src/Prismedia.Api/Endpoints/Health/HealthEndpoints.cs:12-16`; exempt from auth at `apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:116-118`).
- **Name search.** No endpoint names contain `apple`, `tvos`, `ios`, `native`, or `device`. Searched `"/api...` route strings in `apps/backend/src/Prismedia.Api` for `v1|client|native|apple|tvos|ios|device|auth|login|token|session|pair|qr`. The only hits were auth routes and `/api/browser-session` (`apps/backend/src/Prismedia.Api/Endpoints/Playback/BrowserSessionEndpoints.cs:8-18`, "Ensures a browser-scoped persistence session").

### Auth flow for a native client

1. **Setup check and login.**
   - `GET /api/auth/setup-status` is public (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:20-28`).
   - `POST /api/auth/login` is public (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:141-148`). It takes `LoginRequest(Username, Password, Client?, DeviceName?, DeviceId?)`, and the optional fields "describe the device for the sessions list" (`apps/backend/src/Prismedia.Contracts/Security/AuthContracts.cs:22-28`).
2. **Token.**
   - A successful login returns `LoginResponse(AccessToken, User)`, described as "the bearer token for native clients" (`apps/backend/src/Prismedia.Contracts/Security/AuthContracts.cs:30-31`).
   - The server also sets the `prismedia-session` HttpOnly cookie (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:209-212`; cookie options at `apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:23-24, 89-97`).
   - The endpoint summary says it issues "a session cookie and bearer token" (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:87`). One session-token type serves web, native, and OPDS (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:8-12`).
3. **Presenting the token.** The server checks three places, in this order:
   - `Authorization: Bearer <token>`
   - the session cookie, ignored when `Sec-Fetch-Site: cross-site`
   - the `?access_token=` query parameter, "used by media renderers that cannot attach an Authorization header" (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:153-182`; `apps/backend/src/Prismedia.Contracts/Security/AuthContracts.cs:10-11`)

   Every `/api` path except health, codegen, and the public auth routes requires authentication (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:114-139`).
4. **Long-lived tokens.** Sessions are sliding: `SessionSlidingWindow = TimeSpan.FromDays(90)`, meaning "a session dies after this long without activity". Last-seen is refreshed at most every 5 minutes (`apps/backend/src/Prismedia.Application/Security/UserAuthService.cs:20-24`). There are no separate refresh tokens or API keys. **Not found**: searched for `refresh.?token`, `api.?key`, and `pairing|qrcode|\bqr\b|device[-_ ]?code` in backend and web source.
5. **Device registration.** There is no separate registration endpoint. Device metadata is captured at login:
   - `ClientIdentity(Client ?? "Prismedia Web", DeviceName, DeviceId, User-Agent)` (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:14, 219-226`)
   - stored as `UserSession.Client/DeviceName/DeviceId/ApplicationVersion` (`apps/backend/src/Prismedia.Application/Security/SecurityModels.cs:20-31, 40-45`)
   - listed and revoked through `GET /api/auth/sessions` and `DELETE /api/auth/sessions/{id}` (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:163-204`)
6. **QR codes and pairing.** **Not found** (same search as above). User docs describe a manual flow: enter the server address, choose Continue, and sign in with username and password (`documentation-site/docs/using/native-apps.md:12-15`).

### Version/compat headers and CORS

- **Version/compat headers.** **Not found**: searched for `ApiVersion|X-Prismedia|X-Client|api/v[0-9]`. The client version is not negotiated. `ApplicationVersion` is filled from the raw `User-Agent` header (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:220-225`). User docs only say to "Check the installed native app and server versions" (`documentation-site/docs/using/native-apps.md:43`).
- **CORS.** The only policy is `PrismediaDevCors`. It allows only localhost, 127.0.0.1, and ::1 origins, and it is applied **only in Development** (`apps/backend/src/Prismedia.Api/Program.cs:83-94, 106-109`). Native apps do not need CORS. A third-party browser client on another origin would be blocked in production.

### Docs mentions of the Apple apps

- **Marketing site.** App Store links for "iPhone, iPad & Apple TV" (`documentation-site/src/pages/index.tsx:83, 156-159`; `documentation-site/src/components/marketing/MarketingFooter.tsx:26`), plus `operatingSystem: 'Web, iOS, iPadOS, tvOS'` (`documentation-site/src/pages/index.tsx:36`).
- **Install docs.** An App Store link, id6792944211 (`documentation-site/docs/getting-started/before-you-install.md:43`).
- **Native-only features.** "Continue Combined" exists only in the native app (`documentation-site/docs/using/read-and-listen.md:8, 32`).
- **Nitrate plan.** Acceptance items include "Prismedia iPhone/iPad App Store app can connect to the server" (`docs/nitrate/plans/2026-09-26-foundation.md:515`).

## 16. Best extension points for our reader

### (a) Backend: Annotation entity, EF mapping, migration

The closest per-user analogs are:
- **`user_entity_states`** (per user and entity, one row)
- **`entity_consumption_events`** (per user and entity, many rows, `Guid` id). This is the best shape match for annotations.

Patterns to follow:

- **Domain.** Mutable capability classes live in `apps/backend/src/Prismedia.Domain/Capabilities/UserState/`; `CapabilityProgress.cs` is at `:8-59`. The contract says the domain should expose intent methods and no EF (`docs/backend-architecture-contract.md:40-44`). Annotations are many rows and not a capability projection, so a dedicated domain type plus an Application port is the better fit, rather than an `EntityCapability`.
- **EF row.** Rows live in `apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/`, for example `EntityConsumptionEventRow` (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityCapabilityRows.cs:47-60`) or `BookChapterAudioMappingRow` (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/BookChapterAudioMappingRow.cs:3-19`).
- **EF configuration.** Configuration goes in a `PrismediaModelConfiguration` partial. Examples:
  - `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaCapabilityModelConfiguration.cs:55-80` (events): snake_case columns, `ValueGeneratedNever` id, `(user_id, …)` indexes, FKs to `EntityRow` and `UserRow` with cascade
  - `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaMediaModelConfiguration.cs:99-125` (book chapter mappings)
- **DbSet.** Add it in `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaDbContext.cs`; for example `UserEntityStates` is at `:61` and `EntityConsumptionEvents` at `:63`.
- **Migrations.** They go in `apps/backend/src/Prismedia.Infrastructure/Persistence/Migrations/` (`docs/backend-architecture-contract.md:72`). The latest one is `20260908235145_AddHeldAcquisitionAlternatives.cs`. Follow `docs/database-migrations.md` (`docs/backend-architecture-contract.md:78-79`).
- **Application port and use case.**
  - Port example: `IConsumptionEventStore` (`apps/backend/src/Prismedia.Application/Playback/IConsumptionEventStore.cs:6-16`).
  - Service example: `IBookChapterMappingService` with a status-result record (`apps/backend/src/Prismedia.Application/Books/IBookChapterMappingService.cs:8-46`). A new `Prismedia.Application/Books/IBookAnnotationService.cs` would follow this.
- **Infrastructure implementation.**
  - `EfConsumptionEventStore(PrismediaDbContext db, ICurrentUserContext currentUser)` stamps `UserId` from `currentUser.UserId` (`apps/backend/src/Prismedia.Infrastructure/Playback/EfConsumptionEventStore.cs:11-45`).
  - `EfBookChapterMappingService(db, IEntityVisibilityChecker visibility, …)` checks book visibility first (`apps/backend/src/Prismedia.Infrastructure/Media/Books/EfBookChapterMappingService.cs:15-39`).
- **DI registration.** Register in `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:314-317` next to the book services, or at `:362` next to the event store.
- **Contracts.** Request and response DTOs go in `Prismedia.Contracts/Books/` (namespace used at `apps/backend/src/Prismedia.Api/Endpoints/Books/BookEndpoints.cs:2`) or `Prismedia.Contracts/Entities/` (`EntityUpdateRequests.cs`). Endpoints must never return domain or EF types (`docs/backend-architecture-contract.md:103-111`).

### (b) API route group registration

There are two options:
- Add routes to the existing `/api/books` group returned by `MapBookEndpoints` (`apps/backend/src/Prismedia.Api/Endpoints/Books/BookEndpoints.cs:10-16`, which uses `MapEntityKindRoutes`). An example would be `group.MapGet("/{id:guid}/annotations", …)`, following the `.WithName`/`.WithSummary`/`.Produces` style at `:18-71`.
- Create a new `Map…Endpoints` extension, called from `MapPrismediaEndpoints` (`apps/backend/src/Prismedia.Api/Endpoints/EndpointRouteBuilderExtensions.cs:6-45`; `MapBookEndpoints` is at `:16`). Per-entity sub-routes can instead be attached through `EntityEndpoints` (`apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityEndpoints.cs:5-22`).

Other notes:
- Authentication is global for `/api` through middleware, so no per-endpoint attribute is needed. Admin-only routes use `.RequireAdmin()` (`apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityMarkerEndpoints.cs:18`), which annotations must **not** use.
- **API tests** use `WebApplicationFactory<Program>`, replace the service with a fake, call `.WithTestAuth()` and then `CreateAuthenticatedClient()` (`apps/backend/tests/Prismedia.Api.Tests/BookChapterMappingEndpointTests.cs:13-100`; helper at `apps/backend/tests/Prismedia.Api.Tests/TestAuth.cs:45`).
- **Infrastructure tests** use EF InMemory (`apps/backend/tests/Prismedia.Infrastructure.Tests/EpubBookContentsServiceTests.cs:76-79`). Postgres-specific suites also exist, for example `UserEntityStateConcurrencyPostgresTests.cs`.
- **Frontend codegen.** After adding endpoints, run `pnpm api:generate` (Orval followed by `gen-codes.mjs`) (`package.json:14`, `apps/web-svelte/package.json:16`). The generated client lives in `apps/web-svelte/src/lib/api/generated/prismedia.ts`; for example `getBookContents` is at `:4442`.

### (c) Frontend

- **Capturing selection.**
  - Listen for foliate's `load` event (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:339-349`, detail `{doc, index}`) inside `BookFileReader.svelte`'s `onMount`, next to the `relocate` listener (`apps/web-svelte/src/lib/components/BookFileReader.svelte:298`), and remove it in cleanup (`:322`).
  - On that `doc`, attach `selectionchange` and/or `pointerup` handlers, read `doc.getSelection().getRangeAt(0)`, and compute the CFI with `view.getCFI(index, range)` (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:431-435`).
  - The paged-mode gesture overlay (`apps/web-svelte/src/lib/components/BookFileReader.svelte:417-426,517-522`) must be changed, for example with long-press handoff or pass-through while selecting. Otherwise selection cannot start in paginated mode.
  - Coordinates from the iframe need translating to the top window, for example via `range.getBoundingClientRect()` plus the iframe's `frameElement` rect. This last point is design guidance, not existing code.
- **Selection menu popover.**
  - `@prismedia/ui-svelte` exports `Popover` (bits-ui) (`packages/ui-svelte/src/index.ts:44`; content at `packages/ui-svelte/src/components/ui/popover/popover-content.svelte:1-51`, which portals and uses `z-50`). It also exports `DropdownMenu` (`index.ts:43`), `Sheet` (`:27`), `Textarea` (`:41`), `ToggleGroup` (`:47`) and `Dialog` (`:56`).
  - `Popover` is used in the app, for example in `apps/web-svelte/src/lib/components/entities/AddToCollectionMenu.svelte`.
  - The reader is inside a fullscreen `Dialog` (`apps/web-svelte/src/lib/components/reader/ReaderShell.svelte:90`), so the popover should mount inside the `book-reader-stage` div (`apps/web-svelte/src/lib/components/BookFileReader.svelte:416-464`) or be portalled with a z-index above the reader chrome. `ReaderContentsSheet` is the precedent for a layered overlay over the reader (`apps/web-svelte/src/lib/components/BookFileReader.svelte:467-469`; tests at `apps/web-svelte/src/lib/components/reader/ReaderContentsSheet.test.ts:5-24`).
- **Drawing highlights.**
  - Listen for `draw-annotation` and call `e.detail.draw(Overlayer.highlight, { color })` (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:392-393`; `apps/web-svelte/src/lib/vendor/foliate-js/overlayer.js:126-141`). `Overlayer` is exported from `apps/web-svelte/src/lib/vendor/foliate-js/overlayer.js:4`, so it needs its own import, and `apps/web-svelte/src/lib/foliate-vendor.d.ts:4` would need a matching `declare module`.
  - Listen for `create-overlay` (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:418`) and call `view.addAnnotation({ value: cfi, … })` for each saved annotation in that section index.
  - Listen for `show-annotation` (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:411`) to open the edit or note popover.
- **API wrapper.** Add a new `apps/web-svelte/src/lib/api/annotations.ts`, or extend `books.ts`, following the pattern `generated fn → requestInit(options) → unwrapGenerated(response, msg)` (`apps/web-svelte/src/lib/api/books.ts:1-46`). Unit tests mock `fetch` (`apps/web-svelte/src/lib/api/files.test.ts:13-25`).
- **Tests.**
  - Vitest uses jsdom with the `$app/*` mocks (`apps/web-svelte/vitest.config.ts:5-30`) and `@testing-library/svelte` component tests (`apps/web-svelte/src/lib/components/reader/ReaderContentsSheet.test.ts:1-24`).
  - No `BookFileReader` or `PdfReader` component test exists; a grep for those names in `*.test.ts` found none.
  - Pure EPUB helpers are tested in `apps/web-svelte/src/lib/entities/epub-contents.test.ts`, for example `:120,127`.
  - Playwright: `playwright.config.ts:9` sets `testDir: "./e2e"` with a chromium-only project (`:23-30`). The only spec is `e2e/smoke.spec.ts`, which covers video, jobs and similar (`:25`), plus `e2e/fixture-library.ts`. **No reader or book e2e test exists.**
  - **No EPUB fixture files:** `find -iname "*.epub"` excluding `node_modules` returned nothing, and `tests/fixtures/media` contains only `videos/`. Backend tests generate EPUBs in code with `ZipArchive` (`apps/backend/tests/Prismedia.Infrastructure.Tests/EpubBookContentsServiceTests.cs:86-88`).

### 16.1 Notes for annotation design

- **CFI generation and parsing already exist, client-side only.**
  - foliate emits a CFI on every relocate (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:333`). `getCFI` and `resolveCFI` are available (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:431-445`), and `epubcfi.js` has `fromRange`, `toRange`, `compare` and `parse` (`apps/web-svelte/src/lib/vendor/foliate-js/epubcfi.js:107,163,288,296`).
  - App code only prefix-checks `epubcfi(` (`apps/web-svelte/src/lib/entities/epub-contents.ts:66-70,129-131`).
  - The backend treats CFI as an opaque string (`apps/backend/src/Prismedia.Domain/Capabilities/UserState/CapabilityProgress.cs:55-59`; `apps/backend/src/Prismedia.Domain/Entities/Enums/Media/ProgressUnit.cs:25-27`). There is no server-side CFI parser: a backend grep for `epubcfi` / `\bcfi\b` returned only comments and the enum.
  - To sort annotations server-side, either store a spine index and fraction alongside the CFI, or sort client-side with `CFI.compare`.
- **Book identity.** The reader identifies a book by the **Entity id** (`page.params.id`, `+page.svelte:64`). The file is fetched by `(entityId, role="source")`, not by file id (`+page.svelte:137`; `apps/backend/src/Prismedia.Infrastructure/Entities/EfEntityFileContentService.cs:28-36`). Progress is keyed by `(user_id, entity_id)` (`apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaCapabilityModelConfiguration.cs:20`). Annotations should key on `(user_id, book entity_id)`.
- **Multiple files.** A readable Book has one `source` file; the lookup uses `FirstOrDefault` per role (`apps/backend/src/Prismedia.Infrastructure/Entities/EfEntityFileContentService.cs:28-36`). Audiobook parts are separate `AudioTrack` child entities with their own source files (`apps/backend/src/Prismedia.Infrastructure/Media/Books/EfBookChapterMappingService.cs:50-59`). A Book can therefore carry an EPUB/PDF and also have audio children (`documentation-site/docs/library/books.md:112-150`).
- **Rescan and file changes.**
  - The Book upsert is **path-keyed**: `FindEntityBySourcePath(Book, sourcePath)` updates the existing entity and keeps its id (`apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPersistenceService.ScanUpserts.cs:478-500`). A new path creates a new `Guid` (`:503-518`).
  - Replacing file content at the same path therefore keeps the entity id, but CFIs may silently point to different text.
  - There is no content hash on `EntityFileRow`. The closest signal is `BookContentStateRow.SourceSignature` (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/BookReadingChapterRow.cs:37-47`), computed as `epub:{length}:{lastWriteTicks}:{hash(path)}` (`apps/backend/src/Prismedia.Infrastructure/Media/Books/EfBookChapterMapService.cs:210-211`).
  - Acquisition upgrades swap the book file in place (`AtomicBookFile`, `apps/backend/src/Prismedia.Application/Jobs/Handlers/Acquisition/AcquisitionUpgradeReplaceJobHandler.cs:222-240`).
  - Recommendation: store the highlighted text (and some context) with each annotation so it can be re-anchored or flagged as stale. Optionally store the source signature too.
- **Current user on the backend.**
  - The authentication middleware resolves the session token, then calls `CurrentUserContextHolder.Set(user, sessionId)` (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:61-78,108-112`).
  - Services inject `ICurrentUserContext` and read `UserId`, which is `Guid.Empty` when unauthenticated (`apps/backend/src/Prismedia.Application/Security/ICurrentUserContext.cs:11-22`; `apps/backend/src/Prismedia.Application/Security/CurrentUserContextHolder.cs:21-38`). Examples: `apps/backend/src/Prismedia.Infrastructure/Entities/Mappers/Capabilities/ProgressCapabilityMapper.cs:13,17` and `apps/backend/src/Prismedia.Infrastructure/Playback/EfConsumptionEventStore.cs:11,35`.
  - Endpoints can also use `HttpContext.GetCurrentUser()` (`apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs:85-86`).
  - The worker runs as `IsSystem` with no user id (`apps/backend/src/Prismedia.Application/Security/ICurrentUserContext.cs:15-19`). An annotation store should reject `Guid.Empty`.
- **Visibility.** Use `IEntityVisibilityChecker.IsVisibleAsync` (`apps/backend/src/Prismedia.Application/Entities/IEntityVisibilityChecker.cs:8-10`), as the file and chapter-mapping services do. This hides NSFW or library-restricted books per user.
- **Scope limits.**
  - foliate overlays exist only in the reflowable paginator, not in fixed-layout (§14), so fixed-layout EPUBs cannot be highlighted with this mechanism.
  - PDFs use a separate pdf.js text layer, so PDF highlights would need their own anchoring, for example page index plus text-layer offsets.
- **Don't break native clients.** Keep `/api/entities/{id}/progress` and the meaning of `location` unchanged (`docs/nitrate/VISION.md:21-22`), and add annotations as new routes. The product intent for annotations (colors, notes, bookmarks separate from highlights, `/annotations` dashboard) is in `docs/nitrate/VISION.md:162-229`.

## 17. Relevant licenses

- **Prismedia / Nitrate.** `LICENSE` starts with "Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International" (`LICENSE:1`) and "Copyright (c) 2025 Paul Davis" (`LICENSE:3`). It includes the NonCommercial term (`LICENSE:20`). The README credits Prismedia by Paul Davis (`README.md:4`). AGENTS.md describes Nitrate as a "private, personal, non-commercial snapshot fork" (`AGENTS.md:3-4`).
- **foliate-js (vendored).**
  - Location: `apps/web-svelte/src/lib/vendor/foliate-js/`.
  - `LICENSE` reads "MIT License" and "Copyright (c) 2022 John Factotum" (`apps/web-svelte/src/lib/vendor/foliate-js/LICENSE:1,3`).
  - Provenance: source `https://github.com/johnfactotum/foliate-js`, pinned commit `78914aef4466eb960965702401634c2cb348e9b1`, license MIT (`apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:7-9`).
- **Third-party code bundled inside foliate-js.** `vendor/` "holds foliate's own bundled dependencies (zip.js, fflate)" (`apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:11-12`).
  - Files present: `vendor/zip.js` and `vendor/fflate.js`.
  - Neither minified file has a license or copyright string (a grep for `licen|copyright` returned 0 matches in each).
  - Their licenses are therefore **not stated in this repo**. Per their public repos, zip.js is BSD-3-Clause and fflate is MIT (not verified in this repo).
  - foliate's own bundled pdfjs build (`vendor/pdfjs/`) was **removed**, and `pdf.js` was replaced by a stub (`apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:16-21`; `apps/web-svelte/src/lib/vendor/foliate-js/pdf.js:1-6`).
  - `ui/menu.js` and `ui/tree.js` are foliate's own files.
- **pdfjs-dist.**
  - Declared as `"pdfjs-dist": "^6.0.227"` (`apps/web-svelte/package.json:33`).
  - The installed package reports `"version": "6.0.227"` and `"license": "Apache-2.0"` (`apps/web-svelte/node_modules/pdfjs-dist/package.json:3,14`).
  - It is used directly by `apps/web-svelte/src/lib/components/PdfReader.svelte:473-474`.
  - VISION confirms "foliate-js (MIT) and PDF.js (Apache-2.0) are fine" (`docs/nitrate/VISION.md:13`).
- **Readest and BookOrbit (reference only, no code).** Readest is AGPL-3.0 per its public repo (not verified in this repo). BookOrbit's license is also not verified in this repo. Both are forbidden as code sources: "never copy code from Readest (AGPL) or BookOrbit" (`AGENTS.md:22-23`). They are "UX/architecture references only" (`docs/nitrate/VISION.md:12-13`).
- **Full inventory.** `docs/THIRD_PARTY_LICENSES.md` holds the full inventory. It is required by `AGENTS.md:22-23` and already exists on this branch (commit `7bdcd4b8`). Its foliate-js row is at `docs/THIRD_PARTY_LICENSES.md:38`, its zip.js and fflate rows at `:39-40`, and its Readest/BookOrbit rows at `:456-457`.

## 18. Risks / limitations

### Vendored foliate-js
- **Why it is vendored.** foliate-js is not published to npm (`apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:4-5`).
- **How it is updated.** Manually: "Update by re-copying the runtime modules from a newer pinned commit". Local modifications must be re-applied by hand, currently the `pdf.js` stub (`apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:16-24`).
- **Build integration.** It is loaded only by dynamic import (`apps/web-svelte/src/lib/components/BookFileReader.svelte:275`) and typed through a shim (`apps/web-svelte/src/lib/foliate-vendor.d.ts:4`).
- **No drift check.** **Not found**: there is no automated upstream diff or version check. I searched non-vendor source and package manifests for `vendor/foliate-js`; the only hits were the two references above.
- **Divergence from upstream.** Git history shows fork-side edits to the vendored tree, for example "drop unused vendored pdfjs" (`git log -- apps/web-svelte/src/lib/vendor/foliate-js`: `dc0c3219`, `17e55aca`, `e6119513`, `3d9738c6`).
- **Stale documentation.** `apps/web-svelte/src/lib/vendor/foliate-js/PROVENANCE.md:13-14` says PDFs are rendered server-side and "shown in the comic reader". Yet the reader route mounts `PdfReader` (`apps/web-svelte/src/routes/books/[id]/reader/+page.svelte:307`), which runs pdfjs-dist in the client (`apps/web-svelte/src/lib/components/PdfReader.svelte:473-475`). The provenance text should be re-checked.

### EPUB CFI stability across re-scans and file changes
- **How progress is stored.** The CFI is an "Opaque format-specific resume locator (e.g. an EPUB CFI)" (`apps/backend/src/Prismedia.Domain/Capabilities/UserState/CapabilityProgress.cs:55-59`). It is saved per user in `UserEntityStateRow.ProgressLocation`, next to `ProgressIndex/Total` (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityCapabilityRows.cs:17-35`). The progress unit code is `cfi` (`apps/backend/src/Prismedia.Domain/Entities/Enums/Media/ProgressUnit.cs:26-27`).
- **How a book file is identified: by source path.**
  - `UpsertSingleFileBookAsync` looks up `FindEntityBySourcePath(Book, sourcePath)`. On a match it updates the existing entity in place and keeps its Id (`apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPersistenceService.ScanUpserts.cs:478-500`). Otherwise it creates a new Guid (`:503-518`).
  - `EntityFileRow` stores `Path`, `SizeBytes`, and timestamps, but no content hash (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityFileRow.cs:5-33`).
- **Consequence 1: replacing a file at the same path keeps the entity Id and the saved CFI.** Nothing detects that the content changed, so the old CFI may point into a different spine or DOM.
  - Content hashing exists (`EntityFileFingerprintRow` with `md5` and `oshash`, at `apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityCapabilityRows.cs:151-158` and `apps/backend/src/Prismedia.Domain/Entities/Enums/Media/FingerprintAlgorithm.cs:8-13`). But it is used only to decide whether fingerprints are missing (`apps/backend/src/Prismedia.Application/Jobs/Handlers/FingerprintGating.cs:12-41`), not to match or invalidate books.
- **Consequence 2: moving or renaming a file loses the book.** Stale cleanup removes books whose source path is no longer valid (`apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPersistenceService.StaleCleanup.cs:140-156`), so a moved file becomes a new entity. **Not found**: no fingerprint-based relinking for moved files; I searched StaleCleanup, Lifecycle, and ReplacementReservations for `fingerprint|oshash|moved|relink|rename`.
- **Consequence 3: a stale CFI has no fallback on the client.** The reader passes `initialLocation`, which takes precedence over the fraction, straight to `view.init`. On failure it only shows an error message and does not retry with the fraction (`apps/web-svelte/src/lib/components/BookFileReader.svelte:301-316`).
- **Annotations.** Future highlights and annotations anchored by CFI would inherit all of the above. **Not found**: no annotation or highlight model exists today; I searched backend `*.cs` for `annotation|highlight`, and the hits were unrelated.

### Multi-user assumptions
- **Product framing.** Prismedia is "optimized for a single trusted user or household on a private LAN and ships as a Docker image" (`AGENTS.md:74`). VISION adds "one server, one account, one library" (`docs/nitrate/VISION.md:14`).
- **What is per user.** Reading state is keyed by `UserId + EntityId` (`apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/EntityCapabilityRows.cs:17-19`). Library access is per user (`UserResponse.LibraryRootIds`, where "Admins implicitly access every library": `apps/backend/src/Prismedia.Contracts/Security/AuthContracts.cs:53-57`). Sessions are per user (`apps/backend/src/Prismedia.Api/Endpoints/Auth/AuthEndpoints.cs:163-204`).
- **Takeaway.** Accounts exist, but the threat model is a trusted LAN. For example, the OPDS Basic-auth path verifies the password on every request, and sessions are LAN-oriented.

### Blocks for native Windows (top items only; detailed checklist is elsewhere)
- **Backups shell out to Docker by default.**
  - `DatabaseBackupService` runs `docker exec pg_dump` and `docker cp` (`apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:355-375, 432-447`).
  - The default compose file is `infra/docker/docker-compose.yml`, overridable with `PRISMEDIA_BACKUP_DOCKER_COMPOSE_FILE` and `PRISMEDIA_BACKUP_DOCKER_COMPOSE_POSTGRES_SERVICE` (default `postgres`).
  - `pg_dump` and `pg_restore` default to whatever is on PATH (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:396-419`).
- **VA-API default device.** The default device is `/dev/dri/renderD128` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:353`; `apps/backend/src/Prismedia.Application/Settings/AppSettings.cs:312`).
- **AMF hardware encoding.** An `Amf` profile (`h264_amf`) was added in Nitrate commit `85cb5003` (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetServiceOptions.cs:25-26`; `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:176`). It still needs an owner-verified test on the AMD host (`docs/nitrate/VISION.md:16-18`).
- **Docker-only runtime assembly.** PostgreSQL, ffmpeg, and audiowaveform are provided by the Docker image rather than the app (next subsection). A native install must supply all three.

### Docker-first assumptions
- **Bundled PostgreSQL 16.** The single runtime image installs "PostgreSQL 16, and Jellyfin FFmpeg" (`infra/docker/unified.Dockerfile:64-66, 91-92`). audiowaveform is built from source in a separate stage (`infra/docker/unified.Dockerfile:28-45`).
- **Entrypoint manages the database.** `/bin/sh` runs `initdb` under `gosu` in `/data/postgres` and edits `postgresql.conf` with `sed` (`infra/docker/entrypoint.sh:1, 4, 41-44, 66-72`).
- **Docker named as the product shape.** "ships as a Docker image" (`AGENTS.md:74`). Nitrate overrides this: "Docker remains supported but is not the Windows path" (`AGENTS.md:18-19`).

### Upstream no longer synced
- Nitrate is a snapshot fork at `627958a` (v3.7.2) and is "never synced with upstream" (`AGENTS.md:3-5`). The only git remote is the Nitrate repo (`git remote -v`: `origin https://github.com/sarunvisoth/nitrate`).
- Security fixes, bug fixes, and changes to the native-app contract made upstream will not arrive automatically. Nitrate owns every fix.
- This interacts with the Apple apps. App Store updates track **upstream** Prismedia's API, so a future app version could expect endpoints that this frozen fork lacks. **Not found**: there is no compatibility or version handshake (see §15).

## 19. Native Windows readiness

Target: a native Windows PC (AMD CPU/GPU) running the API and the worker directly with `dotnet`, with no Docker. All paths are repo-relative. `bin/`, `obj/`, `node_modules/` and `Migrations/` were excluded.

**Bottom line.** The backend is mostly platform-neutral already:
- Filesystem paths are compared case-insensitively on Windows through a shared helper (`apps/backend/src/Prismedia.Application/Files/FileSystemPathComparison.cs:9-14`).
- Hard links use `CreateHardLinkW` on Windows (`apps/backend/src/Prismedia.Infrastructure/Acquisition/HardLink.cs:18-20`).
- There is an AMD AMF encoder profile (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetServiceOptions.cs:25-26`).
- Processes are started with `UseShellExecute = false` and never through `bash`/`sh` (`apps/backend/src/Prismedia.Infrastructure/Processes/ProcessExecutor.cs:28-36`).

What remains falls into two groups:
- **Runtime environment:** the port, data dir, web root, the PostgreSQL client tools on PATH, and restarting the processes, which the Docker entrypoint does today.
- **A handful of small Windows behaviour gaps:** file locking, `/proc`-only load sampling, and restore-by-restart.

### 19.1 Grep audit: every hit

Command 1 (Linux-path/shell patterns) hits, plus the extra patterns requested (`/proc`, `nice`, `ProcessPriorityClass`, `FileShare`, `FileSystemWatcher`, symlinks, `pg_dump`, `docker`, `Split('/')`, Ordinal path compares):

| # | Location (path:line) | What it does | Breaks on Windows? | Minimal fix |
|---|---|---|---|---|
| 1 | `apps/backend/src/Prismedia.Api/Program.cs:35-37` | API data dir = `PRISMEDIA_DATA_DIR` → `Prismedia:DataDir` → `"/data"`, resolved by `ResolvePath` (`apps/backend/src/Prismedia.Api/Program.cs:200-203`: `Path.GetFullPath` when rooted) | **Degrades**: on Windows `/data` is rooted but has no drive, so `GetFullPath` makes it `<drive of CWD>:\data` (e.g. `C:\data`) | Set `PRISMEDIA_DATA_DIR`. Optionally (small & local), default to `%ProgramData%\Prismedia` when `OperatingSystem.IsWindows()` |
| 2 | `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:72-77` | Same data/cache resolution for the Infrastructure layer (API and worker), via `NormalizePath` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:578-581`) | **Degrades** (same as #1) | Same as #1 |
| 3 | `apps/backend/src/Prismedia.Api/appsettings.json:6-7`, `apps/backend/src/Prismedia.Worker/appsettings.json:6-7` | Production `Prismedia:DataDir="/data"`, `CacheDir="/data/cache"` | **Degrades**: becomes `C:\data`, `C:\data\cache` | Override with env vars, or ship an `appsettings.Production.json` with Windows paths |
| 4 | `apps/backend/src/Prismedia.Application/Settings/AppSettings.cs:310-313` | Default for the `hls.vaapiDevice` setting is `/dev/dri/renderD128` | **No**: only used by the VA-API profile | none |
| 5 | `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:353`, `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetServiceOptions.cs:68` | `PRISMEDIA_VAAPI_DEVICE` default `/dev/dri/renderD128` | **No**: VA-API is only auto-selected under `OperatingSystem.IsLinux()` (#6) | none |
| 6 | `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:410-424` | The `Auto` profile picks VideoToolbox on macOS, VA-API on Linux if the device exists, otherwise Software | **Degrades**: on Windows `Auto` always means software libx264 and the AMD GPU goes unused | Set `PRISMEDIA_HLS_TRANSCODER=Amf` (or the `hls.transcoderProfile` setting, which offers `Amf` at `apps/backend/src/Prismedia.Application/Settings/AppSettings.cs:305`). A code change to auto-pick AMF would need encoder probing, so it is **larger** |
| 7 | `apps/backend/src/Prismedia.Infrastructure/Media/Books/BookCoverImageExtractor.cs:23` | PDF cover rendering is allowed on Linux, macOS and Windows | **No** | none |
| 8 | `apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:365` | `/tmp/prismedia-restore-….dump` temp path | **No**: this path is *inside* the Postgres container, used only by the Docker Compose fallback (`:363-384`) | none |
| 9 | `apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:294-306`, `:323-335`; defaults at `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:408-413` | Runs `pg_dump`/`pg_restore` by bare name, with `PG*` env vars built from the connection string (`apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:625-642`) | **Degrades**: the Windows PostgreSQL installer does not add `bin` to PATH, so you get a "not found" error (`:577-583`) | Set `PRISMEDIA_PG_DUMP_PATH` and `PRISMEDIA_PG_RESTORE_PATH` to `C:\Program Files\PostgreSQL\<ver>\bin\pg_dump.exe` and `pg_restore.exe`, or add that dir to PATH |
| 10 | `apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:308-317`, `:422-448`, `:546-550`; `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:396-399` | Docker Compose fallback (`docker exec pg_dump`) when the client tools are missing | **No** (not used): it only activates if the compose file exists and the DB host is local. Without Docker it fails with a clear message (`apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:585-588`). Missing-exe detection covers Win32 error 2 (`:571-575`) | none |
| 11 | `apps/backend/src/Prismedia.Api/Endpoints/Settings/SettingsEndpoints.cs:90-101`, `:222-229` (**fixed in Nitrate**: now defaults to in-process on Windows) | Outside Development, a restore request calls `lifetime.StopApplication()` and relies on an external supervisor to restart the API. The API then applies the restore at boot (`apps/backend/src/Prismedia.Api/Program.cs:178`) | **Yes**: a native process exits cleanly (code 0) and nothing restarts it. Windows Service recovery actions do not fire on a clean stop, so the restore never runs and the API stays down | **Small & local**: set `PRISMEDIA_BACKUP_RESTORE_IN_PROCESS=true`. Or change the default at `apps/backend/src/Prismedia.Api/Endpoints/Settings/SettingsEndpoints.cs:229` to `environment.IsDevelopment() \|\| OperatingSystem.IsWindows()`. Unit-testable |
| 12 | `infra/docker/entrypoint.sh:96-112` (worker loop), `:120-129` (API) | The container entrypoint supervises the worker (restarts it after 3 s) and injects `DATABASE_URL`, data/cache dirs and `ASPNETCORE_URLS` | **Yes** (no equivalent natively): nothing restarts a crashed worker, and none of these env vars are set | **Larger** (packaging): run the API and worker as Windows Services (`UseWindowsService()` or NSSM/WinSW) with restart-on-failure and the env vars from §19.2 |
| 13 | `apps/backend/src/Prismedia.Infrastructure/Processes/HostLoadProbe.cs:94-118` | `ProcHostLoadSampleReader` reads `/proc/stat`, `/proc/loadavg`, `/proc/meminfo` | **Degrades**: the files don't exist, so it returns `null` (`:101-103`), and the probe then always reports headroom (`:58-61`, `:68-71`). Background media admission no longer backs off under host CPU or memory pressure | **Small & local**: add a `WindowsHostLoadSampleReader` (P/Invoke `GetSystemTimes` + `GlobalMemoryStatusEx`; load average is not available, so pass 0) behind `IHostLoadSampleReader` (`:19-22`) and pick it with `OperatingSystem.IsWindows()` in the constructor at `:40-42`. Injectable, so unit-testable |
| 14 | `apps/backend/src/Prismedia.Infrastructure/Processes/ProcessExecutor.cs:240-249` | Low-priority jobs set `ProcessPriorityClass.BelowNormal` (best effort) | **No**: this maps to Windows `BELOW_NORMAL_PRIORITY_CLASS` | none |
| 15 | `apps/backend/src/Prismedia.Infrastructure/Processes/ProcessExecutor.cs:265-267` | Recognizes ffmpeg with `GetFileNameWithoutExtension`, ignoring case | **No**: `ffmpeg.exe` matches | none |
| 16 | `apps/backend/src/Prismedia.Infrastructure/Media/Processing/MediaToolOptions.cs:44-46`, `:82-95` | Looks for `jellyfin-ffmpeg` on PATH (with no `.exe`), otherwise uses `ffmpeg`. ffprobe name is derived with `.exe` awareness (`:70-80`) | **No**: the lookup misses on Windows and falls back to `ffmpeg`, which process start resolves to `ffmpeg.exe` on PATH | Put an AMF-capable ffmpeg build on PATH, or set `PRISMEDIA_FFMPEG_PATH` and `PRISMEDIA_FFPROBE_PATH` |
| 17 | `apps/backend/src/Prismedia.Application/Jobs/LibraryFileChangeMonitor.cs:85-107` | `FileSystemWatcher` with `InternalBufferSize = 64 KB`; `Error`/overflow schedules a reconciliation | **No**: 64 KB is the Windows maximum and overflow is already handled (`:101-107`) | none (SMB/UNC roots may drop events; the reconciliation covers this) |
| 18 | `apps/backend/src/Prismedia.Infrastructure/Health/FileWorkerHeartbeatStore.cs:28`, `:58-67` (**fixed in Nitrate**: shared read + temp cleanup) | The API reads the heartbeat with `File.OpenRead`. The worker writes a temp file, then `File.Move(temp, path, overwrite: true)` | **Degrades**: on Windows the replace fails with a sharing violation while the API has the file open (`OpenRead` does not grant `FileShare.Delete`). The failure is caught and logged (`apps/backend/src/Prismedia.Application/Health/WorkerHeartbeatPublisher.cs:25-27`), but the `.tmp` file is left behind | **Small & local**: open the reader with `new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite \| FileShare.Delete)`, and delete `tempPath` in a `catch` around the move |
| 19 | `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:304-316` | Stale virtual-HLS cache: `Directory.Delete(root, recursive: true)`, not guarded by any try/catch | **Degrades**: if a segment is still open (ffmpeg writing, or a client being served), Windows throws `IOException` and the playback request fails. Linux unlinks open files without error | **Small & local**: wrap it in try/catch with a short retry, or rename the directory aside and delete it best-effort (as `apps/backend/src/Prismedia.Infrastructure/Videos/TranscodeCacheService.cs:184-191` already does) |
| 20 | `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:452-458` | `ResetStagingDirectory` does an unguarded `Directory.Delete` | **Degrades** (same cause as #19) | Same pattern as #19 |
| 21 | `apps/backend/src/Prismedia.Infrastructure/Videos/TranscodeCacheService.cs:184-191` | Cache eviction deletes directories best-effort (catch + warn) | **No** (eviction of a locked dir is skipped and logged) | none |
| 22 | `apps/backend/src/Prismedia.Infrastructure/Acquisition/ImportFileMover.cs:59-67`, `:92-95` | Move-mode import: `File.Move`; on `IOException`, copy (source opened with `FileShare.Read`) then `File.Delete(source)` | **Degrades**: if a torrent client still has the payload open for write, the Move, the copy's source open and the delete all fail on Windows | Use Hardlink or Copy import mode on Windows. Code fix (**small & local**): open the source with `FileShare.ReadWrite` at `:93`. Making Move robust while seeding is **larger** |
| 23 | `apps/backend/src/Prismedia.Infrastructure/Acquisition/HardLink.cs:16-24` | Hard link via `CreateHardLinkW` on Windows, `link(2)` elsewhere; falls back to copy on failure | **No** (NTFS, same volume) | none |
| 24 | `apps/backend/src/Prismedia.Infrastructure/Acquisition/CompletedPayloadFileSystem.cs:74-75`, `:116` | Detects and resolves symlinks via `LinkTarget`/`ResolveLinkTarget` | **No** (the BCL supports NTFS reparse points) | none |
| 25 | `apps/backend/src/Prismedia.Application/Acquisition/ImportPlanBuilder.cs:59-60`, `:197-202` | `SanitizeSegment` replaces `<>:"/\|?*` and control characters, trims spaces, and neutralizes `.`/`..` | **Degrades**: trailing dots (e.g. an album `"Vol."`) and reserved names (`CON`, `NUL`, `AUX`, `COM1`…) are not handled. Win32 silently strips trailing dots, so the stored path can differ from the file on disk | **Small & local**: `TrimEnd('.', ' ')` and prefix `_` on reserved device names. Unit-testable |
| 26 | `apps/backend/src/Prismedia.Application/Files/FileSystemPathComparison.cs:9-31` | Case-insensitive path comparer and comparison on Windows; `IsSameOrDescendant` uses `Path.DirectorySeparatorChar` | **No** (already Windows-aware) | none |
| 27 | `apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPathRules.cs:6-43`, `apps/backend/src/Prismedia.Infrastructure/Acquisition/AcquisitionHintApplier.cs:728-734` | Normalize `\` to `/`, then compare with `FileSystemPathComparison.Comparison` | **No** | none |
| 28 | `apps/backend/src/Prismedia.Infrastructure/Media/Processing/AssetPathService.cs:40-54`, `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:595-606`, `apps/backend/src/Prismedia.Infrastructure/Videos/TrickplayService.cs:183-191`, `apps/backend/src/Prismedia.Infrastructure/StashCompat/StashScraperInstaller.cs:203-209` | Containment checks with `StringComparison.Ordinal` | **No**: root and candidate both come from `Path.GetFullPath` of the same root string (e.g. `apps/backend/src/Prismedia.Infrastructure/Media/Processing/AssetPathService.cs:16-19`, `:47`), so casing is consistent. At worst the check fails closed | none |
| 29 | `apps/backend/src/Prismedia.Application/Acquisition/MediaNamingTemplates.cs:80,187,202`, `apps/backend/src/Prismedia.Application/Acquisition/ImportPlanBuilder.cs:143`, `apps/backend/src/Prismedia.Application/Acquisition/MediaImportMerge.cs:245`, `apps/backend/src/Prismedia.Application/Acquisition/MediaImportPlanning.cs:38,766`, `apps/backend/src/Prismedia.Application/Acquisition/AcquisitionImportFileLedger.cs:266-272`, `apps/backend/src/Prismedia.Application/Jobs/Handlers/Acquisition/AcquisitionImportEngines.cs:877`, `apps/backend/src/Prismedia.Application/Jobs/Handlers/Acquisition/MusicAcquisitionReplacement.cs:49-52` | `Split('/')` on *template/relative* paths, which use `/` by design | **No**: relative paths are normalized (`\`→`/` at `apps/backend/src/Prismedia.Application/Acquisition/AcquisitionService.Files.cs:88-94`, `apps/backend/src/Prismedia.Application/Acquisition/AcquisitionImportFileLedger.cs:267`), and absolute targets go through `Path.GetFullPath(Path.Combine(...))` (`apps/backend/src/Prismedia.Application/Jobs/Handlers/Acquisition/AcquisitionImportEngines.cs:886`, `apps/backend/src/Prismedia.Application/Jobs/Handlers/Acquisition/MusicAcquisitionImportEngine.cs:269`) | none |
| 30 | `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:199`, `apps/backend/src/Prismedia.Infrastructure/Persistence/DirectPlayableMigrationAssetPreparer.cs:630-631` | `Split('/')` on URL/virtual asset paths | **No** (URLs, not filesystem paths) | none |
| 31 | `apps/backend/src/Prismedia.Infrastructure/StashCompat/StashScriptExecutor.cs:240-258`, `:277-281` | Runs Stash python scrapers using the command from the scraper definition (`action.Script[0]`). `PYTHONPATH` is joined with `Path.PathSeparator` | **Degrades**: needs Python and the scraper's pip deps (the Docker image installs them, `infra/docker/unified.Dockerfile:103-109`). `python3` may not exist on Windows. Launch failure gives `StashPythonUnavailableException` (`:262-266`) | Install Python and the packages listed in the Dockerfile; make sure the command named by the scraper resolves |
| 32 | `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:595-600` | Plugin dev path under the user profile, via `Environment.SpecialFolder.UserProfile` (not `$HOME`) | **No** | none |
| 33 | `apps/backend/src/Prismedia.Api/Endpoints/Entities/EntityFileResults.cs:118-141`, `apps/backend/src/Prismedia.Infrastructure/Media/Books/BookCoverImageExtractor.cs:29` | Temp files via `Path.GetTempPath()`; the file is closed before it is deleted | **No** | none |
| 34 | `apps/backend/src/Prismedia.Api/Prismedia.Api.csproj:1-25` | No app manifest or `longPathAware` settings | **Degrades (unverified)**: .NET itself handles long paths, but external tools given long paths (ffmpeg, pg_dump) may not | Keep library roots short, or enable `LongPathsEnabled` in the OS |

Not found anywhere in `apps/backend/src/**/*.cs`:
- `Mono.Unix`, `inotify`, `chmod`, `UnixFileMode`/`SetUnixFileMode`, `"bash"`/`"sh"`, `Path.Combine("/…`, `GetEnvironmentVariable("HOME")`.
- `/proc` outside #13, `/sys`, `cgroup`, `nice`, PosixSignal/SIGTERM.
- `/usr/`, `/var/`, `/etc/`, `/run/`, `/mnt/`, `/media`.

### 19.2 Environment variables

The Prismedia config keys can also be set in the standard .NET env form (e.g. `Prismedia__DataDir`). This is ASP.NET configuration behaviour, not repo code.

| Var | Default (cite) | Meaning | Needed on Windows? |
|---|---|---|---|
| `DATABASE_URL` | none; falls back to `ConnectionStrings:Prismedia`, otherwise it throws (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:65-68`) | DB connection. A `postgres://` or `postgresql://` URL is converted to key/value form; either form gets pool defaults Max 40 / idle 60 s (`apps/backend/src/Prismedia.Infrastructure/Database/PostgresConnectionString.cs:17-50`) | Optional. The appsettings default `Host=localhost;Port=5432;Database=prismedia;Username=prismedia;Password=prismedia` (`apps/backend/src/Prismedia.Api/appsettings.json:2-4`, `apps/backend/src/Prismedia.Worker/appsettings.json:2-4`) works if you create that role/DB on a local PostgreSQL. `DATABASE_URL` takes precedence over it |
| `ConnectionStrings:Prismedia` (env `ConnectionStrings__Prismedia`) | as above | Npgsql connection string | Same as above |
| `ASPNETCORE_URLS` | not set in code; Docker sets `http://0.0.0.0:8008` (`infra/docker/unified.Dockerfile:132`, `infra/docker/entrypoint.sh:128`) | Kestrel bind address | **Yes** (see §19.4) |
| `PRISMEDIA_STATIC_WEB_ROOT` | none; falls back to `<contentRoot>/wwwroot` (`apps/backend/src/Prismedia.Api/Program.cs:26-27`, `:121-126`, `:151`) | Built SPA directory | **Yes**, unless the build is copied into `wwwroot` (§19.3) |
| `PRISMEDIA_DATA_DIR` | `/data` (`apps/backend/src/Prismedia.Api/Program.cs:35-37`; `DependencyInjection.cs:72-74`; appsettings `apps/backend/src/Prismedia.Api/appsettings.json:6`) | Data root (backups, heartbeat, generated sources) | **Yes**: otherwise `C:\data` |
| `PRISMEDIA_CACHE_DIR` | `<dataDir>/cache` (`apps/backend/src/Prismedia.Api/Program.cs:38-40`; `DependencyInjection.cs:75-77`; appsettings `:7` sets `/data/cache`) | Asset/HLS cache, served at `/assets` (`apps/backend/src/Prismedia.Api/Program.cs:128-131`) | **Yes** (or follows DATA_DIR if appsettings is overridden) |
| `PRISMEDIA_FFMPEG_PATH` | `jellyfin-ffmpeg` if found on PATH, else `ffmpeg` (`DependencyInjection.cs:79`; `apps/backend/src/Prismedia.Infrastructure/Media/Processing/MediaToolOptions.cs:44-46`) | ffmpeg executable | Recommended (absolute path to an AMF-capable `ffmpeg.exe`) |
| `PRISMEDIA_FFPROBE_PATH` | derived from the ffmpeg path, keeping `.exe` (`DependencyInjection.cs:80`; `apps/backend/src/Prismedia.Infrastructure/Media/Processing/MediaToolOptions.cs:58-80`) | ffprobe executable | Optional |
| `PRISMEDIA_ASSETGEN_THREADS` | 1 (`apps/backend/src/Prismedia.Infrastructure/Media/Processing/MediaToolOptions.cs:17-20`; override at `DependencyInjection.cs:81-85`) | ffmpeg threads for background asset generation | No |
| `PRISMEDIA_HLS_TRANSCODER` | `Auto` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:351`; `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetServiceOptions.cs:39-44`) | HLS encoder profile | Not sufficient on its own: the persisted `hls.transcoderProfile` setting (default `Auto`) is parsed first and wins (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:431-436`), so this env var only applies when no database settings are available. **Select `AMD AMF` in Settings → HLS Transcoding instead** |
| `PRISMEDIA_VAAPI_DEVICE` | `/dev/dri/renderD128` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:353`) | VA-API device | No |
| `PRISMEDIA_BACKUP_DIR` | `<dataDir>/backups/database` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:387-389`) | pg_dump output dir | No (follows DATA_DIR) |
| `PRISMEDIA_BACKUP_RETENTION_DAYS` | 7 (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:390-395`) | Auto-backup retention | No |
| `PRISMEDIA_PG_DUMP_PATH` | `pg_dump` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:408-410`) | pg_dump executable | **Yes**, unless PostgreSQL `bin` is on PATH |
| `PRISMEDIA_PG_RESTORE_PATH` | `pg_restore` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:411-413`) | pg_restore executable | **Yes**, unless PostgreSQL `bin` is on PATH |
| `PRISMEDIA_BACKUP_DOCKER_COMPOSE_FILE` | `<repoRoot>/infra/docker/docker-compose.yml` when running inside the repo (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:396-402`; repo root = dir with `pnpm-workspace.yaml` + `package.json`, `:674-686`) | Compose file for the docker fallback | No |
| `PRISMEDIA_BACKUP_DOCKER_COMPOSE_POSTGRES_SERVICE` | `postgres` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:415-417`) | Compose service name | No |
| `PRISMEDIA_BACKUP_RESTORE_IN_PROCESS` | `IsDevelopment()` (`apps/backend/src/Prismedia.Api/Endpoints/Settings/SettingsEndpoints.cs:222-229`) | Restore inside the running API instead of stop-and-restart | **Yes: set `true`** (#11) |
| `PRISMEDIA_PLUGIN_DEV_PATHS` | none; in a repo checkout it adds `~/Dev/Prismedia-Plugins` (if present) and `<repo>/packages/plugins` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:583-606`) | Extra plugin dirs (`,`/`;` separated) | No |
| `PRISMEDIA_VERSION` | `package.json` version from the repo root, else `1.0.0` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:609-635`); update check at `apps/backend/src/Prismedia.Infrastructure/Updates/GhcrUpdateCheckService.cs:412-416` | Reported version | No |
| `PRISMEDIA_PLUGIN_INDEX_URL` / `PRISMEDIA_PLUGIN_INDEX_DISABLED` | GitHub raw `index.yml` / not disabled (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:638-654`) | Plugin catalog | No |
| `PRISMEDIA_STASH_SCRAPER_INDEX_URL` / `PRISMEDIA_STASH_SCRAPERS_DISABLED` | CommunityScrapers stable index / not disabled (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:656-671`) | Stash scraper catalog | No |
| `PRISMEDIA_PLUGIN_<PROVIDER>_<KEY>` | none (`apps/backend/src/Prismedia.Infrastructure/Plugins/PluginCredentialResolver.cs:22-28`) | Env-supplied plugin credentials | Optional |
| `PRISMEDIA_RECOVERY_PASSWORD` / `PRISMEDIA_RECOVERY_USERNAME` | unset / `admin` (`apps/backend/src/Prismedia.Infrastructure/Security/UserBootstrapRunner.cs:19-24`) | Admin recovery on boot | No (only for recovery) |
| `PRISMEDIA_OPENSUBTITLES_API_KEY` / `_USERNAME` / `_PASSWORD` | unset (`apps/backend/src/Prismedia.Infrastructure/Subtitles/OpenSubtitlesProtocol.cs:15-17`; used at `apps/backend/src/Prismedia.Infrastructure/Subtitles/SubtitleAcquisitionService.cs:364`) | OpenSubtitles credential override | Optional |
| `PRISMEDIA_CHANNEL` | `dev` (`apps/backend/src/Prismedia.Infrastructure/Updates/GhcrUpdateCheckService.cs:389-392`) | Update channel | No |
| `PRISMEDIA_COMMIT` | unset, which gives a non-networked dev status (`apps/backend/src/Prismedia.Infrastructure/Updates/GhcrUpdateCheckService.cs:18-29`, `:394-395`) | Build commit | No |
| `PRISMEDIA_GHCR_HOST` / `PRISMEDIA_IMAGE_REPOSITORY` / `PRISMEDIA_UPDATE_PAGE_URL` | `ghcr.io` / `pauljoda/prismedia` / GitHub package page (`apps/backend/src/Prismedia.Infrastructure/Updates/GhcrUpdateCheckService.cs:397-410`) | Update check target | No: it checks Docker images, which is meaningless for a native install |
| `PRISMEDIA_SECRET` | generated by `infra/docker/entrypoint.sh:17-28` | Described there as plugin-credential encryption | No: no `.cs` file reads it (the `PRISMEDIA_*` grep over `apps/backend/src` returns no `SECRET` hit) |

### 19.3 Web assets: how the API finds the SPA

- **Lookup:**
  - The API reads `PRISMEDIA_STATIC_WEB_ROOT`, then `Prismedia:StaticWebRoot` (`apps/backend/src/Prismedia.Api/Program.cs:26-27`).
  - `ResolveStaticWebRoot` returns `null` if the value is empty. For a relative path it tries `<contentRoot>/<path>` and then `<CWD>/<path>`, and it returns the first directory that exists, otherwise `null` with no warning (`apps/backend/src/Prismedia.Api/Program.cs:184-198`).
  - If a root was found, it is served with `UseDefaultFiles` and `UseStaticFiles` (`apps/backend/src/Prismedia.Api/Program.cs:115-120`). Otherwise the default web root is used (`apps/backend/src/Prismedia.Api/Program.cs:121-126`), which is `<contentRoot>/wwwroot`. In the repo that folder holds only a `.gitkeep` (`apps/backend/src/Prismedia.Api/wwwroot/`).
  - The SPA fallback serves `index.html` only if that file exists (`apps/backend/src/Prismedia.Api/Program.cs:149-176`).
  - In Development with no web root, requests are proxied to the Vite dev server `http://localhost:5173` (`apps/backend/src/Prismedia.Api/Program.cs:110-112`).
- **Docker layout:**
  - The builder stage runs `pnpm --filter @prismedia/web-svelte build` (`infra/docker/unified.Dockerfile:26`).
  - The runtime stage copies `/app/apps/web-svelte/build` to `/app/wwwroot` (`:140`) and sets `PRISMEDIA_STATIC_WEB_ROOT=/app/wwwroot` (`:133`; also defaulted in `infra/docker/entrypoint.sh:125`).
- **Build output:**
  - `svelte.config.js` uses `@sveltejs/adapter-static` with `fallback: "index.html"` and does not set `pages`/`assets` (`apps/web-svelte/svelte.config.js:1`, `:8-11`), so the output is the adapter's default `build/` directory.
  - This matches `apps/web-svelte/.gitignore` (`/build`, line 2) and the Dockerfile's `COPY …/apps/web-svelte/build`.
  - `build` is `vite build`, with a `prebuild` step running `node scripts/copy-jassub-assets.mjs` (`apps/web-svelte/package.json:9-10`). That script uses `node:path`, so it is cross-platform (`apps/web-svelte/scripts/copy-jassub-assets.mjs:17-31`).
  - The API base defaults to `/api` (`apps/web-svelte/src/lib/api/orval-fetch.ts:4`), so the build needs no env vars.
- **What to do natively (PowerShell):**
  1. `corepack enable; pnpm install`, from the repo root.
  2. `pnpm --filter @prismedia/web-svelte build`, which writes `apps\web-svelte\build\`.
  3. Either set `PRISMEDIA_STATIC_WEB_ROOT=C:\path\to\nitrate\apps\web-svelte\build`, or copy `build\*` into the published API's `wwwroot\`. An absolute path is safest: a mistyped relative path silently falls back to the empty `wwwroot`.

### 19.4 Port and address binding

- Nothing in the backend configures Kestrel: grep for `Kestrel|UseUrls|ListenAnyIP|8008` in `apps/backend/src` finds only a comment in `apps/backend/src/Prismedia.Api/SpaDevProxy.cs:8`. `appsettings.json` has no `Kestrel`/`Urls` section (`apps/backend/src/Prismedia.Api/appsettings.json:1-17`).
- `0.0.0.0:8008` comes only from three places:
  - `launchSettings.json` `http` profile (`apps/backend/src/Prismedia.Api/Properties/launchSettings.json:8`), which applies to `dotnet run` only.
  - Docker `ENV ASPNETCORE_URLS=http://0.0.0.0:8008` (`infra/docker/unified.Dockerfile:132`).
  - The entrypoint (`infra/docker/entrypoint.sh:128`).
- **On Windows you must set `ASPNETCORE_URLS=http://0.0.0.0:8008`** (or pass `--urls`) when running the published `Prismedia.Api.dll`/`.exe`. Otherwise Kestrel uses its built-in default (localhost:5000). Allow TCP 8008 in Windows Firewall for LAN clients.
- `ASPNETCORE_ENVIRONMENT` should stay Production (the default). Development would enable in-process restore but also the dev proxy and CORS (`apps/backend/src/Prismedia.Api/Program.cs:106-113`).

### 19.5 Data and cache directories

- Production defaults: `DataDir=/data`, `CacheDir=/data/cache` in both hosts (`apps/backend/src/Prismedia.Api/appsettings.json:5-8`, `apps/backend/src/Prismedia.Worker/appsettings.json:5-8`), with the same code fallbacks (`apps/backend/src/Prismedia.Api/Program.cs:35-40`, `DependencyInjection.cs:72-77`).
- Resolution: `Path.IsPathRooted("/data")` is true on Windows, so `Path.GetFullPath("/data")` becomes **`<drive of the process CWD>:\data`**, e.g. `C:\data` (`apps/backend/src/Prismedia.Api/Program.cs:200-203`; `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:578-581`). The API creates the cache dir at startup (`apps/backend/src/Prismedia.Api/Program.cs:128`).
- This works, but it is surprising: `C:\data` sits at the drive root, not under `%ProgramData%`. The resolved path also changes if the service's working directory is on another drive.
- Development uses `../../data` relative to the content root (`apps/backend/src/Prismedia.Api/appsettings.Development.json:6-7`).
- **Recommendation:** set `PRISMEDIA_DATA_DIR` and `PRISMEDIA_CACHE_DIR` explicitly (e.g. `D:\Prismedia\data`, `D:\Prismedia\data\cache`) for *both* API and worker. They must match: the API serves `/assets` from the cache that the worker writes, and the worker heartbeat lives in the data dir (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:420`).

### 19.6 Worker: Linux-only assumptions

- **Process priority:** `ProcessPriorityClass.BelowNormal` (`apps/backend/src/Prismedia.Infrastructure/Processes/ProcessExecutor.cs:240-249`) works on Windows. No `nice`/`renice`/`ionice`.
- **CPU permits:** based only on `Environment.ProcessorCount` (`apps/backend/src/Prismedia.Application/Jobs/AdaptiveJobCapacity.cs:14-15`; `apps/backend/src/Prismedia.Application/Jobs/QueueWorker.cs:35`, `:46-47`). No cgroup or `/sys` reads.
- **Host load:** `/proc` only (#13). On Windows the load gate is effectively disabled, so background ffmpeg is still bounded by permits but not by live CPU or memory pressure.
- **Supervision and restore:** the worker waits for any pending restore and for DB readiness (`apps/backend/src/Prismedia.Worker/Program.cs:26-27`). Restart-on-crash is provided only by the Docker entrypoint loop (#12).
- **Heartbeat:** file replace can fail under Windows locking (#18).

### 19.7 Backup and restore

- **Invocation:** `pg_dump --format=custom --no-owner --no-acl --file <path> <db>` and `pg_restore --clean --if-exists --no-owner --no-acl --dbname <db> <path>`, run with `PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD` from the connection string (`apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:294-306`, `:323-335`, `:625-642`).
- **Executables:** `PRISMEDIA_PG_DUMP_PATH` / `PRISMEDIA_PG_RESTORE_PATH`, defaulting to bare `pg_dump` / `pg_restore` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:408-413`).
- **Docker fallback:**
  - Used only if the tool is missing *and* the compose file exists *and* the DB host is local (`apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:308-317`, `:546-563`).
  - It shells out to `docker container ls` / `exec` / `cp` (`:400-448`, `:492-500`), controlled by `PRISMEDIA_BACKUP_DOCKER_COMPOSE_FILE` and `…_POSTGRES_SERVICE` (`apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:396-417`).
  - Without Docker it throws a clear error (`apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:585-588`).
- **Restore flow:**
  1. The API writes `restore-request.json` in the backup dir (`apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:123-124`; path at `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:407`).
  2. In Production it stops itself (`apps/backend/src/Prismedia.Api/Endpoints/Settings/SettingsEndpoints.cs:97-100`).
  3. On the next boot it applies the restore before migrations (`apps/backend/src/Prismedia.Api/Program.cs:178`; `apps/backend/src/Prismedia.Infrastructure/Database/DatabaseRestoreRunner.cs:19-30`).
- **Natively on Windows:**
  - Backup works once the PostgreSQL client tools are reachable (#9). Their major version should be at least the server's.
  - Restore needs `PRISMEDIA_BACKUP_RESTORE_IN_PROCESS=true`, or a supervisor that restarts after a *clean* exit (#11).
  - The `IsPathUnderDirectory` check uses `Path.GetRelativePath` (`apps/backend/src/Prismedia.Infrastructure/Database/DatabaseBackupService.cs:667-673`), which is fine on Windows.

### Summary

**Small & local fixes** (a few lines each, unit-testable):
1. `apps/backend/src/Prismedia.Api/Endpoints/Settings/SettingsEndpoints.cs:229`: default restore-in-process to true on Windows (`|| OperatingSystem.IsWindows()`), or document `PRISMEDIA_BACKUP_RESTORE_IN_PROCESS=true`. Today a Production restore stops the API and nothing restarts it.
2. `apps/backend/src/Prismedia.Infrastructure/Processes/HostLoadProbe.cs:40-42, 94-118`: add a Windows `IHostLoadSampleReader` (`GetSystemTimes` + `GlobalMemoryStatusEx`) so background admission backs off under load.
3. `apps/backend/src/Prismedia.Infrastructure/Health/FileWorkerHeartbeatStore.cs:28, 67`: open the reader with `FileShare.ReadWrite | FileShare.Delete`, and delete the temp file if the move fails.
4. `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.cs:315` and `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:454`: guard `Directory.Delete` (try/catch + retry, or rename aside) so a locked segment doesn't fail playback.
5. `apps/backend/src/Prismedia.Application/Acquisition/ImportPlanBuilder.cs:197-202`: also strip trailing `.` and escape reserved device names (`CON`, `NUL`, …).
6. `apps/backend/src/Prismedia.Infrastructure/Acquisition/ImportFileMover.cs:93`: open the copy source with `FileShare.ReadWrite` so copies from a still-seeding payload succeed.
7. Optional: `apps/backend/src/Prismedia.Api/Program.cs:37` and `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs:74`: default the data dir to `%ProgramData%\Prismedia` on Windows instead of `/data` (which becomes `C:\data`).

**Configuration only** (no code change): `ASPNETCORE_URLS=http://0.0.0.0:8008`, `PRISMEDIA_STATIC_WEB_ROOT=<repo>\apps\web-svelte\build`, `PRISMEDIA_DATA_DIR`, `PRISMEDIA_CACHE_DIR`, `DATABASE_URL` or `ConnectionStrings__Prismedia`, `PRISMEDIA_PG_DUMP_PATH`, `PRISMEDIA_PG_RESTORE_PATH`, and `PRISMEDIA_FFMPEG_PATH` pointing at an AMF-enabled ffmpeg. Select **AMD AMF** under Settings → HLS Transcoding in the app; `PRISMEDIA_HLS_TRANSCODER` is overridden by the persisted setting.

**Larger items:**
- Process supervision and packaging: the Docker entrypoint's worker restart loop and env injection (`infra/docker/entrypoint.sh:96-129`) have no native equivalent. Needs Windows Services (`UseWindowsService()` or a wrapper) for the API and worker, and a PostgreSQL install.
- Choosing AMF automatically: `Auto` means software on Windows (`apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs:410-424`), and auto-selecting AMF needs encoder probing.
- Robust Move-mode imports while a torrent client holds files (`apps/backend/src/Prismedia.Infrastructure/Acquisition/ImportFileMover.cs:59-67`).
- Python/Stash scraper runtime parity: the Docker image installs the pip deps (`infra/docker/unified.Dockerfile:103-109`); there is no native equivalent.

## 20. Proposed annotation system (summary)

> **Implemented in M3 (2026-09-26).** See `docs/nitrate/plans/2026-09-26-m3-reader-slice-1.md` for the "As built"
> notes. Entry points:
> - Backend: `Prismedia.Domain/Entities/Enums/Books/Annotation*.cs`, `Prismedia.Contracts/Books/BookAnnotations.cs`,
>   `Prismedia.Application/Books/IBookAnnotationService.cs` + `BookAnnotationWritePolicy.cs`,
>   `Prismedia.Infrastructure/Media/Books/EfBookAnnotationService.cs`, migration `*_AddBookAnnotations`, and the routes in
>   `Prismedia.Api/Endpoints/Books/BookEndpoints.cs`.
> - Web: `apps/web-svelte/src/lib/reader/*` (sync store, selection helpers, colours, progress catch-up),
>   `components/reader/ReaderSelectionMenu.svelte` + `AnnotationEditor.svelte`, `BookFileReader.svelte`, and
>   `routes/books/[id]/reader/+page.svelte`.
> - E2E: `e2e/reader-annotations.spec.ts`.
>
> **Extended in M4 (2026-09-26):**
> - Bookmarks (`book_bookmarks`, `/api/books/{id}/bookmarks`, `EfBookBookmarkService`).
> - The cross-library read side, `/api/annotations` (list, books, export; `EfAnnotationLibraryService`,
>   `AnnotationExportWriter`).
> - Client-side highlight re-anchoring (`lib/reader/annotation-reanchor.ts`).
> - The `/annotations` page (`components/annotations/AnnotationLibrary.svelte`).
> - Heartbeat-safe progress sync. See `plans/2026-09-26-m4-bookmarks-annotations-export.md`.

This summarises the design that `docs/nitrate/plans/2026-09-26-m3-reader-slice-1.md` expands into tasks. It rests on
§11, §13, §14 and §16, and on the product intent in `docs/nitrate/VISION.md` ("Highlights", "Notes", "Progress
Synchronization").

**Data model.** Add one new table, `book_annotations`. Its shape follows `entity_consumption_events` (§16a): many rows
per user and book, a client-supplied `Guid` id, and cascade FKs to `users` and `entities`.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | Generated by the client, so creates are idempotent and retries are safe |
| `user_id` | uuid FK → users (cascade) | From `ICurrentUserContext.UserId`; `Guid.Empty` is rejected (the worker/system has no user, §16 notes) |
| `entity_id` | uuid FK → entities (cascade) | The **Book** entity id, the same key progress uses (`(user_id, entity_id)`, §13) |
| `locator_kind` | text code | `[Code]` enum `AnnotationLocatorKind`: `epub-cfi` now; `pdf-page-quads` in M7. A new closed set, so it needs a `[Code]` enum and codegen (AGENTS.md identifier discipline) |
| `locator` | text | EPUB: the range CFI from `view.getCFI(index, range)` (`apps/web-svelte/src/lib/vendor/foliate-js/view.js:431-435`) |
| `spine_index`, `position` | int, double | Sort key: foliate section index plus fraction. The server cannot parse CFIs (§16 notes) |
| `selected_text`, `text_before`, `text_after` | text | The VISION `selectedText` + `surroundingText` (a text-quote selector: prefix, exact, suffix). Used to re-anchor the highlight, or flag it as stale, when the CFI no longer matches, because a file replaced at the same path keeps the entity id (§18) |
| `style` | text code | `[Code]` enum `AnnotationStyle`: `highlight` now; `underline`, `strikethrough` and `squiggly` later. These are already supported by the overlayer (`apps/web-svelte/src/lib/vendor/foliate-js/overlayer.js:52-163`) |
| `color` | text code | `[Code]` enum `AnnotationColor`: `yellow`, `blue`, `green`, `red`, `purple` |
| `note` | text null | Optional note on the highlight |
| `source_signature` | text null | Copy of `BookContentStateRow.SourceSignature` when the annotation is created. A mismatch later means the file changed |
| `created_at`, `updated_at` | timestamptz | `updated_at` drives last-write-wins |
| `deleted_at` | timestamptz null | Tombstone, so a delete propagates to other devices |

Indexes: `(user_id, entity_id, deleted_at)` and `(user_id, updated_at)`. The second leaves room for a later global
`/annotations` page.

**EPUB locator.** Use a foliate range CFI (the CFI of the selection `Range`), plus the text-quote fallback. The reader
already persists CFIs for progress (§13), and `view.addAnnotation({ value: cfi })` resolves one directly (§14).
Highlights are drawn only for reflowable EPUBs: `fixed-layout.js` has no overlayer (§14).

**PDF locator (M7, not M3).** A page index plus text-layer quads or character offsets. EPUB CFIs are not reused (§12).

**Sync semantics.**
- The server is the source of truth.
- The client fetches every annotation for the book, tombstones included, when the reader opens and on
  `visibilitychange`/focus.
- Writes are upserts by `id`, carrying the client's `updatedAt`. The server applies an incoming write only if
  `updatedAt` is at least the stored value (last-write-wins).
- A delete sets `deleted_at` (and `updated_at`), so it is itself a last-write-wins write.
- This keeps two devices of the same user convergent with no realtime channel.

**Endpoints.** These are additive, on the existing `/api/books` group (§16b), and auth comes from the global `/api`
middleware. They must not use `.RequireAdmin()`, and must check `IEntityVisibilityChecker` like
`EfBookChapterMappingService` does. Existing routes, including `/api/entities/{id}/progress` and the meaning of
`location`, are unchanged, so the closed-source Apple apps are unaffected (§15).
- `GET /api/books/{id}/annotations` returns the caller's annotations for the book, tombstones included.
- `PUT /api/books/{id}/annotations/{annotationId}` creates or updates (LWW), returning the stored row. A write the
  server does not apply returns 200 with the newer stored row, so the client adopts it.
- `DELETE /api/books/{id}/annotations/{annotationId}?updatedAt=…` writes a tombstone (LWW) and returns 204.

**Frontend integration points.** Most of these are in `BookFileReader.svelte`:
- Listen to foliate `load` and capture selection in each section document.
- Rework the paged-mode gesture overlay so it does not block text selection (`apps/web-svelte/src/lib/components/BookFileReader.svelte:417-426`).
- Handle `create-overlay`, calling `view.addAnnotation(...)` for that section's annotations.
- Handle `draw-annotation` with `draw(Overlayer.highlight, { color })`.
- Handle `show-annotation` to open the edit popover.
- Build the selection menu from the `@prismedia/ui-svelte` `Popover` as a list of action entries. M3 adds only
  Highlight and Add Note; Define, Translate and the rest are added in M5 as entries, not by rewriting the menu.
- The API wrapper goes in `apps/web-svelte/src/lib/api/annotations.ts`, over the orval client regenerated with
  `pnpm api:generate`.
