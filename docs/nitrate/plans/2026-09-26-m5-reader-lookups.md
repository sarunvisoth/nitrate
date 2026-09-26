# M5 — Reader lookups: Define, Translate, Wikipedia, Search book/web

> **Status: built 2026-09-26.** Executed autonomously in one session per the owner's standing
> instruction to work through milestones without stopping for questions. Design defaults below
> were chosen by the agent; the owner can override any of them.

**Goal:** the reader's selection menu gains **Define**, **Translate**, **Wikipedia**, **Search
book**, and **Search web**, backed by backend-proxied lookups so the reader never calls a
third-party API from the browser directly.

**Spec:** VISION "Dictionary System", "Translation", "Wikipedia / Search".

## Global constraints

- **Additive API only** — existing routes are untouched (Apple apps).
- **All new routes require auth**, same as `/api/books/*` (the shared `/api/*` auth middleware
  covers this automatically; no extra `RequireAuthorization()` needed).
- **Identifier discipline** — new closed sets are `[Code]` enums / constant classes; the frontend
  uses generated `codes.ts` types, never hand-written unions.
- **No secrets, no real network in tests.** All backend tests fake `HttpMessageHandler`; web tests
  mock the API; the e2e spec mocks `/api/reader/*` with `page.route`.
- **Not now:** PDF lookups (M7), vocabulary list (M6). Target-language selection is per-device
  (localStorage) for M5; M6 moves it into per-user reader settings.

## Decisions

1. **Backend-proxied lookups**, adapter interfaces in `Prismedia.Application.Reader`, implemented
   in `Prismedia.Infrastructure.Reader` with named `HttpClient`s (`IHttpClientFactory`), an
   in-memory cache (`IMemoryCache`, ~1h TTL) and an ~8s timeout. Provider failure maps to a
   502-equivalent `ApiProblem` (`ApiProblemCodes.ReaderLookupProviderUnavailable`). New endpoints,
   all under `/api/reader`:
   - `GET /api/reader/define?term=&language=` → `IDictionaryProvider`. Default:
     `WiktionaryDictionaryProvider` against
     `https://{lang}.wiktionary.org/api/rest_v1/page/definition/{term}`, filtered to the requested
     language block (default `en`). HTML is stripped from definition/example text server-side.
   - `POST /api/reader/translate` `{ text (≤500 chars), sourceLanguage?, targetLanguage }` →
     `ITranslationProvider`. Default `MyMemoryTranslationProvider` (MyMemory `GET /get?q=&langpair=`,
     source defaults to `"en"` when not given per MyMemory's autodetect quirks). An optional
     `LibreTranslateTranslationProvider` is used instead when
     `AppSettings.Reader.LibreTranslateBaseUrl` is configured (+ optional API key).
   - `GET /api/reader/wikipedia?term=&language=` → `IEncyclopediaProvider`, Wikipedia REST
     `https://{lang}.wikipedia.org/api/rest_v1/page/summary/{title}`.
   - Every outbound request sends `User-Agent: Nitrate/1.0 (self-hosted personal media server)`
     (Wikimedia requires a descriptive UA).
2. **Provider identity is a `[Code]` enum**, `ReaderProviderKind`
   (`Prismedia.Domain/Entities/Enums/Reader`), values `wiktionary`, `wikipedia`, `mymemory`,
   `libretranslate`. It is server-internal (drives which attribution/provider code a response
   reports); the reader never lets the user pick a provider in M5, so it is **not** added to the
   frontend codegen manifest.
3. **New setting keys** (`AppSettings.Reader` group, mirroring the existing settings-registry
   pattern): `reader.translationLibreUrl` (string, default empty = disabled) and
   `reader.translationLibreApiKey` (string, default empty). Documented here and in
   `docs/nitrate/WINDOWS_HOST.md` rather than added to the Settings UI screen (a full registry
   group + UI section is not "cheap" relative to M5's scope; the owner can set them via the
   existing generic `PUT /api/settings/{key}` route or a future Settings pass).
4. **Frontend selection menu** gains, alongside Highlight/Note:
   - **Define** — shown only when the selection is a single word: ≤3 space-separated words and
     ≤60 characters (`isDefinableSelection` in `$lib/reader/selection-lookup.ts`, unit-tested).
     Opens a `LookupPopover` anchored the same way as `AnnotationEditor` (reused `Popover` +
     `measurableFromRect`), showing part-of-speech groups, definitions, examples, an attribution
     link, and **Translate**/**Wikipedia** buttons for the same term.
   - **Translate** — available for any selection (word or passage up to 500 chars); shows the
     translation inline in the same popover, with a small target-language `Select` (defaults from
     `navigator.language`'s primary subtag, else `"en"`, persisted to `localStorage` under
     `nitrate.reader.translateLanguage`).
   - **Wikipedia** — summary + extract + "Open article" external link, in the same popover.
   - **Search web** — opens `https://duckduckgo.com/?q=<encodeURIComponent(selection)>` in a new
     tab; no backend call.
   - **Search book** — a separate sheet (`BookSearchPanel.svelte`) driving foliate's
     `view.search({ query })` async generator and `view.clearSearch()`; results are grouped by
     section with excerpts, click calls `view.goTo(cfi)` and closes the sheet; a "Clear" action
     re-runs `clearSearch()`.
   - All lookup calls go through the regenerated orval client
     (`$lib/api/generated/reader/reader.ts` after `pnpm api:generate`).
5. **Privacy note.** Define/Translate/Wikipedia send the selected text from the Nitrate server to
   Wiktionary, MyMemory, or Wikipedia (or a configured LibreTranslate instance). Self-hosting
   LibreTranslate keeps translation text from ever leaving the LAN. Documented in
   `docs/nitrate/WINDOWS_HOST.md`.

## File map

| Area | Files |
| --- | --- |
| Domain enum | `Prismedia.Domain/Entities/Enums/Reader/ReaderProviderKind.cs` |
| Problem codes | `Prismedia.Contracts/System/ApiProblemCodes.cs` (+2 codes) |
| Contracts (DTOs) | `Prismedia.Contracts/Reader/ReaderLookupContracts.cs` |
| Application ports | `Prismedia.Application/Reader/IDictionaryProvider.cs`, `ITranslationProvider.cs`, `IEncyclopediaProvider.cs`, `ReaderLookupService.cs` |
| Infrastructure providers | `Prismedia.Infrastructure/Reader/WiktionaryDictionaryProvider.cs`, `MyMemoryTranslationProvider.cs`, `LibreTranslateTranslationProvider.cs`, `TranslationProviderSelector.cs`, `WikipediaEncyclopediaProvider.cs`, `HtmlTextStripper.cs` |
| Settings | `Prismedia.Application/Settings/AppSettings.cs`, `AppSettingsRegistry.cs` (Reader group) |
| API endpoints | `Prismedia.Api/Endpoints/Reader/ReaderEndpoints.cs`, registered in `EndpointRouteBuilderExtensions.cs` |
| DI | `Prismedia.Infrastructure/DependencyInjection.cs`, `Prismedia.Api/Program.cs` (named HttpClients + MemoryCache) |
| Backend tests | `Prismedia.Infrastructure.Tests/Reader/*Tests.cs`, `Prismedia.Api.Tests/Endpoints/ReaderEndpointsTests.cs` |
| Frontend helpers | `apps/web-svelte/src/lib/reader/selection-lookup.ts`, `translate-language.ts` |
| Frontend components | `apps/web-svelte/src/lib/components/reader/LookupPopover.svelte`, `BookSearchPanel.svelte` |
| Frontend wiring | `apps/web-svelte/src/lib/components/reader/ReaderSelectionMenu.svelte`, `apps/web-svelte/src/lib/components/BookFileReader.svelte` |
| Frontend tests | `apps/web-svelte/src/lib/reader/selection-lookup.test.ts`, component tests for `LookupPopover` |
| e2e | `e2e/reader-lookups.spec.ts` |
| Docs | `docs/nitrate/WINDOWS_HOST.md` (privacy + LibreTranslate note), this plan, `docs/nitrate/ROADMAP.md` |

## Tasks

- [x] 1. Domain enum `ReaderProviderKind`, `ApiProblemCodes` additions, `Prismedia.Contracts.Reader` DTOs.
- [x] 2. `Prismedia.Application.Reader` ports (`IDictionaryProvider`, `ITranslationProvider`,
      `IEncyclopediaProvider`) + thin `ReaderLookupService` orchestrator used by the endpoints.
- [x] 3. Infrastructure: Wiktionary/MyMemory/LibreTranslate/Wikipedia providers with
      `IHttpClientFactory` + `IMemoryCache` + timeout + HTML stripping + ApiProblem mapping on
      failure. DI registration (named HttpClients, `AddMemoryCache`).
- [x] 4. New `AppSettings.Reader` setting keys (LibreTranslate base URL / API key), registry entries.
- [x] 5. `ReaderEndpoints` (`/api/reader/define`, `/translate`, `/wikipedia`), registered in
      `MapPrismediaEndpoints`.
- [x] 6. Backend unit tests: provider parsing/HTML-stripping/language-filter/caching/error-mapping
      with a fake `HttpMessageHandler`; endpoint tests for auth/validation/problem codes.
- [x] 7. Frontend: `pnpm api:generate` to add the generated reader client + regenerate `codes.ts`
      (none of the new enums are exposed to the frontend, so `codes.ts` content is unchanged apart
      from the new DTO models/operations).
- [x] 8. `selection-lookup.ts` (`isDefinableSelection`) + `translate-language.ts` (device default +
      localStorage) with unit tests.
- [x] 9. `LookupPopover.svelte` (Define/Translate/Wikipedia views) and `BookSearchPanel.svelte`
      (foliate search), wired into `ReaderSelectionMenu`/`BookFileReader`.
- [x] 10. e2e spec `reader-lookups.spec.ts` covering Define, Translate, and Search book, with
      `/api/reader/*` mocked via `page.route`.
- [x] 11. Docs: this plan, `WINDOWS_HOST.md` privacy note, `ROADMAP.md` M5 row.

## Test commands (real output recorded at hand-off)

- `pnpm test:backend` (or the four backend test projects individually)
- `pnpm test:web-svelte`
- `pnpm check`
- `pnpm api:check` (dev API running)
- `npx playwright test e2e/reader-lookups.spec.ts e2e/reader-annotations.spec.ts --workers=1` (and
  the rest of the existing e2e suite as time allows)

## Owner-verified checklist (needs the Windows PC or a real device — not claimed here)

- [ ] Real Wiktionary/MyMemory/Wikipedia responses render correctly in the Define/Translate/
      Wikipedia popovers on the target Windows/AMD host (the cloud sandbox may block these hosts;
      only a `curl`-based smoke check, if any, is recorded below, never claimed as a full pass).
- [ ] LibreTranslate self-hosting flow (setting the base URL, confirming translations stay local).
- [ ] Touch-device behavior of the selection menu's new actions and the Search book sheet.

## As-built notes

- `ReaderSelectionMenu.svelte` ended up owning the Define/Translate/Wikipedia popover and the
  Define/Translate visibility rules directly (via `reader-lookup-actions.ts`), rather than
  `BookFileReader.svelte` holding that state, so `BookFileReader.svelte` stays under the 1000-line
  ceiling (994 lines after this change, was 979 before).
- Real test output at hand-off:
  - `pnpm test:backend` (`dotnet test apps/backend/Prismedia.slnx`): 4576 tests total across the
    four projects (197 + 1153 + 350 + 2876), **1 failed** —
    `AcquisitionDownloadRemovalTests.RecoveryMigrationReopensOnlyCompletedImportedUsenetTransfers`,
    the documented pre-existing failure (unrelated migration/schema mismatch). All new
    `*Reader*` tests passed (28 Infrastructure, 8 Application, 10 Api).
  - `pnpm test:web-svelte` (`vitest run`): **1419/1419 tests passed** across 258 files. The process
    still exited non-zero because of one unhandled async teardown error from
    `ComicReader.test.ts` (a `bits-ui` scroll-lock timer firing after the test environment tore
    down) — pre-existing flake, same class as the documented `AcquisitionProtocolPreference`
    teardown race, not caused by this milestone's changes.
  - `pnpm check` (svelte-check + typecheck across workspaces): 0 errors. One pre-existing warning
    (`BookFileReader.svelte:114`, the untyped foliate `view` handle) is unrelated to M5.
  - `pnpm api:check`: in sync after `pnpm api:generate` (dev API running).
  - e2e: `reader-lookups.spec.ts` (2 tests, Define+Translate+Wikipedia and Search book, both
    mocking `/api/reader/*` with `page.route`) — both passed. Re-ran `reader-annotations.spec.ts`
    and `reader-bookmarks-and-library.spec.ts` as a regression check — all passed.
    `smoke.spec.ts` failed on an unrelated 401 from `/api/libraries` in `fixture-library.ts`
    (unauthenticated request helper, no relation to reader/books work) — flagged for the owner as
    a separate, pre-existing e2e gap, not fixed here (out of scope for M5).
- LibreTranslate and the real Wiktionary/MyMemory/Wikipedia hosts were not reachable from this
  session in a way that was exercised end-to-end against the real internet; all provider tests use
  a fake `HttpMessageHandler` and the e2e spec mocks `/api/reader/*`, per the environment
  constraint. No real-provider response was observed or claimed.
