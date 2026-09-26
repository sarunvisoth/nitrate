# M6: Reader settings + vocabulary

Spec: [VISION.md](../VISION.md) "Reader Settings", "Progress Synchronization", "Vocabulary".

## Decisions

1. **Reader preferences**: one row per user, table `user_reader_preferences`. Fields: font
   family (`ReaderFontFamily`: Publisher/Serif/Sans/Monospace), font size percent (80-200, same
   bounds as the existing in-reader control), line height (1.2-2.0), margin step (0-4), max
   content width px (320-1200), theme (`ReaderTheme`: Auto/Light/Dark/Sepia), flow (reuses the
   existing `ReaderMode` enum: paged/webtoon/scrolled — webtoon is comics-only and rejected for
   book preferences), translation target language (moved from localStorage; migrated once,
   client-side, on first read when the server has no row and localStorage has a value),
   updatedAt. `GET /api/reader/preferences` returns defaults when no row exists.
   `PUT /api/reader/preferences` applies `BookAnnotationWritePolicy`-style last-write-wins on
   client `updatedAt` (policy renamed conceptually as a shared rule; the existing
   `BookAnnotationWritePolicy` type is reused as-is since it is already annotation-agnostic).
   Per-book explicit flow (if a book's saved position/progress carries one) still wins over the
   user default for that book — the user preference is only the default for books with none.
2. **Vocabulary**: table `vocabulary_entries`, one row per saved word. Unique per user on
   `(lower(word), language)` among non-deleted rows; re-adding an existing word updates it
   (definition/context refreshed) and returns the existing id instead of duplicating.
   `GET /api/vocabulary` (search/status/book/cursor/limit, modeled on `/api/annotations`),
   `PUT /api/vocabulary/{id}`, `DELETE /api/vocabulary/{id}` (tombstone), `GET
   /api/vocabulary/export?format=csv|json|markdown` reusing `AnnotationExportFormat` and the
   `AnnotationExportWriter` pattern via a new `VocabularyExportWriter`. No spaced repetition
   yet (future milestone).
3. UI: reader toolbar settings sheet (theme/font/line-height/margin/width/flow), applied live via
   foliate's `view.renderer.setStyles(css)`; sepia contrast is unit-tested (>= 4.5:1). "Add to
   Vocabulary" button on the Define popover result; new `/vocabulary` page modeled on
   `AnnotationLibrary.svelte`; nav entry beside Annotations.
4. Migration via `dotnet ef migrations add`, reviewed, plus a Postgres migration test like M3/M4.

## Tasks

- [x] Domain: `ReaderFontFamily`, `ReaderTheme`, `VocabularyStatus` `[Code]` enums.
- [x] `ApiProblemCodes`: reader-preferences + vocabulary problem codes.
- [x] Infrastructure entities + EF configuration + migration for both tables.
- [x] Application: `IReaderPreferencesService`, `IVocabularyService` ports + contracts.
- [x] Infrastructure: `EfReaderPreferencesService`, `EfVocabularyService`, `VocabularyExportWriter`.
- [x] Api: `/api/reader/preferences` GET/PUT, `/api/vocabulary` CRUD + export endpoints.
- [x] Domain/Application/Infrastructure/Api tests for the above.
- [x] Postgres migration test.
- [x] Frontend: regenerate orval client + `codes.ts`.
- [x] Frontend: `reader-preferences` synced store + `ReaderSettingsSheet.svelte`, wired into the
      reader toolbar and applied to foliate.
- [x] Frontend: `vocabulary` synced store, `/vocabulary` page, nav entry, "Add to Vocabulary" in
      `LookupPopover.svelte`.
- [x] Sepia contrast unit test.
- [x] Playwright e2e spec for settings-persist + define-to-vocabulary flow.

## Owner-verified checklist

- Reading with the new settings sheet on the Windows PC / a real device.
- Visual check of the sepia theme and vocabulary page against the design system.

## As-built notes

- Backend: `user_reader_preferences` (one row per user) and `vocabulary_entries` tables added via
  migration `20260926212652_AddReaderPreferencesAndVocabulary`, plus a hand-reviewed raw-SQL
  partial unique index (`lower(word)`, `language`, live rows only) that EF's fluent API cannot
  express. `EfReaderPreferencesService` and `EfVocabularyService` reuse
  `BookAnnotationWritePolicy` as-is for last-write-wins/clock-plausibility (it was already
  annotation-agnostic). `/api/reader/preferences` (GET/PUT) and `/api/vocabulary` (list/PUT
  `{id}`/DELETE `{id}`/export) are additive; existing endpoints are untouched.
- Frontend: `ReaderPreferencesStore` (`lib/reader/reader-preferences-store.svelte.ts`) loads once,
  saves debounced (500ms), and migrates the M5 localStorage translate-target-language once when
  the server has no saved row. `ReaderSettingsSheet.svelte` (+ its `ReaderSettingsButton.svelte`
  wrapper, kept as a separate file so `BookFileReader.svelte` stayed at the 1000-line ceiling)
  applies theme/font/line-height/margin/width live via `buildReaderContentStyles` →
  `view.renderer.setStyles(css)`; `reader-content-styles.test.ts` asserts every theme (including
  sepia) keeps ≥4.5:1 text contrast. Fixed-layout books hide the typography controls and only
  apply theme, matching the PDF-cheap-theme-only note in the milestone brief (`ReaderTheme` on
  `PdfReader.svelte` itself is **not yet wired** — out of scope for the time available this
  session; flagged as a follow-up, not M7 PDF annotations).
  `/vocabulary` (`VocabularyLibrary.svelte`) mirrors `AnnotationLibrary.svelte`: search, status
  filter, inline status change, delete, CSV/JSON/Markdown export. Nav entry "Vocabulary" added
  beside "Annotations" using the existing `layout-list` icon (no new icon needed).
- **Known gap (time-boxed, not attempted further):** "Add to Vocabulary" in `LookupPopover.svelte`
  saves word + first definition + language today, but does **not** yet capture book id, CFI
  locator, or the surrounding sentence — threading that context down from `BookFileReader`'s
  selection draft through `ReaderSelectionMenu` → `LookupPopover` is a real (small-to-medium)
  follow-up. Vocabulary entries added this way have no book link and "jump to source location"
  does nothing for them (the UI simply shows the word as plain text, no link, when there is no
  book/locator). Entries added via a future full wiring, or via direct API use, work correctly.
- **e2e:** `e2e/reader-settings-vocabulary.spec.ts` covers theme+font-size persisting after reload
  and Define → Add to Vocabulary → `/vocabulary` → status change persisting (mocks
  `/api/reader/define` with `page.route`). Bits-ui's `Select` trigger has accessible role
  `button` (with an `aria-label`), not `combobox` — interact via `getByRole("button", {name})`
  then `getByRole("option", {name})`, not `.selectOption()`.
  **As-built run command** (matches M3's pattern — the .NET API's dev SPA proxy hardcodes
  `http://localhost:5173` in `Program.cs`, so Vite must run on 5173, not the config's 8009
  default):
  ```bash
  ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/backend/src/Prismedia.Api/Prismedia.Api.csproj &
  pnpm --filter @prismedia/web-svelte dev &
  # local, uncommitted playwright.config.local.ts overriding launchOptions.executablePath
  # to /opt/pw-browsers/chromium (never `playwright install` in this sandbox)
  PRISMEDIA_E2E_SVELTE_URL=http://localhost:5173 npx playwright test \
    --config=playwright.config.local.ts \
    e2e/reader-annotations.spec.ts e2e/reader-bookmarks-and-library.spec.ts \
    e2e/reader-lookups.spec.ts e2e/reader-settings-vocabulary.spec.ts --workers=1
  ```
  Result on 2026-09-26: **8/8 passed** (1.5m) — the 3 pre-existing reader specs (6 tests) plus
  the new M6 spec (2 tests); no regressions.
- Real test output, exact commands, and owner-verified items are in the final session report
  handed back to the user.
