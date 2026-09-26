# M8 — Mobile/tablet layout pass, unified global search

Spec: [VISION.md](../VISION.md) "Unified Search", "Device Strategy". Roadmap row: M8.

## Part A — Unified global search

- [x] Additive endpoint `GET /api/search/reader?q=&limit=` returns the current user's matching
  Annotations and Vocabulary entries, reusing `IAnnotationLibraryService`/`IVocabularyService`
  query logic (per-user, visibility-checked) instead of duplicating it.
- [x] New `[Code]` enum `ReaderSearchGroupKind` (`annotation`, `vocabulary`) so the frontend never
  hand-writes these group-kind strings; surfaced through `codes.ts` by the existing manifest/orval
  pipeline.
- [x] Frontend: `fetchReaderSearch` client + `searchReaderExtras` helper merged into the existing
  `SearchResponse` groups used by `/search` and `CommandPalette`.
- [x] `/search`, `CommandPalette`: render Annotation and Vocabulary groups (quote/word snippet,
  book title, colour badge for annotations), each item links to the reader at that location
  (`bookReaderHref`) or to `/vocabulary`. Palette caps a few per group with "see all" to
  `/annotations?q=` / `/vocabulary?q=`.
- [x] `/annotations` and `/vocabulary` pages read `q` from the URL on load (`+page.svelte` passes
  it to `AnnotationLibrary`/`VocabularyLibrary` as an initial search value).
- [x] Backend tests: per-user isolation (a second user's annotations/vocabulary never appear),
  short/blank query short-circuits, visibility respected for annotations.
- [x] Frontend unit tests for the new search-merge helper.
- [ ] e2e: find an annotation and a vocabulary word from the palette and from `/search`.
  (Not run in this session — see Finish notes.)

## Part B — Mobile/tablet layout pass

Audited/fixed the Nitrate-added reader surfaces only, given session time budget (existing
Prismedia entity pages were out of scope for this milestone and were not touched):

- [x] `LookupPopover` / `AnnotationEditor` popovers: clamp within viewport with an 8px margin
  (shared clamping helper) instead of only flipping side.
- [x] Reader toolbars: add `env(safe-area-inset-*)` padding so iPhone/iPad notch/home-indicator
  areas don't overlap controls.
- [x] Touch targets for reader toolbar icon buttons raised to >=44px hit area on touch/coarse
  pointers.
- [x] Playwright "mobile smoke" spec (`e2e/mobile-smoke.spec.ts`) at a 375x812 viewport with
  `hasTouch/isMobile`, asserting no horizontal overflow and that popovers stay on-screen, across
  reader, `/annotations`, `/vocabulary`, `/search`.
- [ ] Full audit across all four viewports with saved screenshots for every listed surface
  (contents/bookmarks sheet, settings sheet, PDF reader parity, app shell nav) — only a subset
  was screenshotted in this session; see as-built notes for what's covered vs. deferred.
- [ ] Real device testing (iPad Safari, Android Chrome, Windows touch) — **Owner-verified**.

## Files touched (backend)

- `apps/backend/src/Prismedia.Domain/Entities/Enums/Books/ReaderSearchGroupKind.cs` (new)
- `apps/backend/src/Prismedia.Contracts/Search/ReaderSearch.cs` (new)
- `apps/backend/src/Prismedia.Api/Endpoints/Search/ReaderSearchEndpoints.cs` (new)
- `apps/backend/src/Prismedia.Api/Endpoints/EndpointRouteBuilderExtensions.cs` (register)
- `apps/backend/tests/**` new Api tests for the endpoint

## Files touched (frontend)

- `apps/web-svelte/src/lib/api/generated/**` (orval + codes.ts regen)
- `apps/web-svelte/src/lib/search/reader-search.ts` (new)
- `apps/web-svelte/src/lib/components/CommandPalette.svelte`, `CommandSearchResult.svelte`
- `apps/web-svelte/src/routes/search/+page.svelte`
- `apps/web-svelte/src/routes/annotations/+page.svelte`, `routes/vocabulary/+page.svelte`
- `apps/web-svelte/src/lib/components/reader/LookupPopover.svelte`,
  `apps/web-svelte/src/lib/components/annotations/AnnotationEditor*.svelte` (viewport clamping)
- `apps/web-svelte/e2e/mobile-smoke.spec.ts` (new), `e2e/global-search.spec.ts` (new)

## Test commands

- `pnpm test:backend`
- `pnpm test:web-svelte`
- `pnpm check`
- `pnpm api:check`
- `pnpm --filter @prismedia/web-svelte exec playwright test e2e/mobile-smoke.spec.ts e2e/global-search.spec.ts --workers=1`
- existing reader e2e specs re-run per instructions

## As-built notes

(Filled in after implementation — see bottom of this file.)

## Owner-verified checklist

- [ ] iPad Safari: reader toolbar safe-area insets, popover placement, touch targets.
- [ ] Android Chrome: same.
- [ ] Windows touch (Surface-class device): same, plus text-selection vs. edge-tap page-turning.
- [ ] Full visual pass of `/search`, `/annotations`, `/vocabulary` on a real phone.
