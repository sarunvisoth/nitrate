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
- [ ] Playwright e2e spec for settings-persist + define-to-vocabulary flow (not completed this
      session — see as-built notes).

## Owner-verified checklist

- Reading with the new settings sheet on the Windows PC / a real device.
- Visual check of the sepia theme and vocabulary page against the design system.

## As-built notes

See the final session report for exact test output, what got cut for time, and why.
