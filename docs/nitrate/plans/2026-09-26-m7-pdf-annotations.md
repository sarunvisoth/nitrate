# M7: PDF annotations

Spec: [VISION.md](../VISION.md) "PDF Reader".

## Decisions

1. **PDF locator**: new `AnnotationLocatorKind.Pdf` (`"pdf"`) code. No schema migration — the existing generic
   `Locator` (string), `SpineIndex` (int), and `Position` (double 0-1) columns are reused: `SpineIndex` holds the
   0-based page index, `Position` holds the y-coordinate of the topmost rect (or 0 for a page bookmark) so
   ordering stays consistent with EPUB. `Locator` holds a small JSON payload
   (`PdfAnnotationLocator`: `{ page, rects: [{x,y,w,h}] }`), rects normalized to the page's unscaled viewport
   (0-1), so zoom/rotation/device size don't affect them. The existing `SelectedText`/`TextBefore`/`TextAfter`
   columns hold the text-quote fields, shared with EPUB. Server-side validation
   (`PdfAnnotationLocatorCodec.TryParse`) rejects malformed JSON, negative pages, more than 64 rects, or any rect
   coordinate outside 0-1, in both `EfBookAnnotationService` and `EfBookBookmarkService`.
2. **Selection → actions**: pdf.js's own text layer selection (already rendered by `PdfReader.svelte`) drives a
   `Range`; `pdf-selection.ts` turns it into normalized rects (merged per line) relative to the page wrapper,
   plus a text quote (reusing `textQuoteFor` from `epub-selection.ts`, which is DOM-generic). The same
   `ReaderSelectionMenu`/`AnnotationEditor`/`LookupPopover` components as EPUB are reused, unchanged.
3. **Highlights**: drawn by a new `PdfHighlightLayer.svelte`, one instance per rendered page, positioned from the
   stored locator's rects scaled to the page's current render size. Tapping a highlight opens `AnnotationEditor`.
4. **Bookmarks**: page bookmark (locator kind `pdf`, empty `rects`) using the same `BookBookmarkStore`; toolbar
   toggle + `ReaderBookmarkList` in `ReaderContentsSheet`'s Bookmarks tab, same as EPUB.
5. **Search**: `pdf-text-search.ts` builds a lazy, cancellable per-page text cache via `page.getTextContent()`
   and returns page + excerpt matches. `PdfSearchPanel.svelte` mirrors `BookSearchPanel`'s Sheet-based UI
   (search box, grouped/flat results with excerpt, click-to-navigate) but is a separate component: `BookSearchPanel`
   is written against foliate's CFI-based `view.search()`/`onNavigate(cfi)`; PDF search is page-based with no
   CFI, so the two aren't unifiable without CFI leaking into foliate's contract or page-index leaking into it.
   The UI pattern (Sheet, grouped results, excerpt) is mirrored instead of duplicated ad hoc.
6. **Dashboard**: `AnnotationLibraryItem` already carries `locatorKind`/`locator` (kind-agnostic). The frontend
   (`AnnotationLibrary.svelte`) shows "p. N" (1-based) instead of a percentage for `pdf`-kind items, using
   `parsePdfLocatorForDisplay`. `bookReaderHref` passes the raw JSON locator through the existing `location`
   query param unchanged (it is opaque there); the reader route parses it back into a starting page for the PDF
   surface only. Export (`AnnotationExportWriter`) adds a `page` field/column, 1-based, null for EPUB.
7. **Vocabulary**: `LookupSourceContext`/`saveVocabularyEntry` already store `bookId` + `sourceLocator` generically;
   no change needed there. Vocabulary's jump-to-source reuses the same `bookReaderHref` path, so it works for PDF
   once the reader route understands a PDF `location`.
8. **Theme**: `PdfReader.svelte` loads `ReaderPreferencesStore` (read-only use — it does not overwrite the user's
   flow/font choice) and colors its own stage/toolbar chrome from the M6 theme's `THEME_COLORS` (light/dark/sepia)
   background, matching `ReaderShell`'s existing surface. Page canvases are never recolored. "Dim pages" in dark
   mode was skipped: PDF canvases already render on a near-black stage background, so a separate dim filter added
   no visible effect worth the extra state — recorded here rather than silently dropped.
9. **File-size discipline**: `PdfReader.svelte` (951 lines) must not grow past 1000. Its previous ad hoc inline
   search UI/state (~90 lines) is extracted into `pdf-text-search.ts` + `PdfSearchPanel.svelte`; new selection/
   highlight/bookmark wiring is added in its place, and the net stays under budget (see as-built line count below).

## Tasks

- [x] Domain: `AnnotationLocatorKind.Pdf` code.
- [x] Application: `PdfAnnotationLocator`/`PdfAnnotationLocatorCodec` (parse/validate/write).
- [x] Infrastructure: per-kind validation in `EfBookAnnotationService` and `EfBookBookmarkService`.
- [x] Application: `AnnotationExportWriter` includes a 1-based `page` in Markdown/JSON/CSV for `pdf`-kind items.
- [x] Backend tests: `PdfAnnotationLocatorTests` (Application), PDF accept/reject cases in
      `EfBookAnnotationServiceTests`/`EfBookBookmarkServiceTests` (Infrastructure), PDF export case in
      `AnnotationExportWriterTests`.
- [x] Frontend: regenerate orval client + `codes.ts` (`pnpm api:generate` against the dev API) for the new
      `pdf` locator-kind code.
- [x] Frontend: `pdf-locator.ts` (locator JSON parse/build + rect validity check mirroring the server) and
      `pdf-selection.ts` (Range → normalized rects, per-line merge) with unit tests.
- [x] Frontend: `PdfHighlightLayer.svelte` (draw + tap-to-edit) wired into `PdfReader.svelte`.
- [x] Frontend: selection capture in `PdfReader.svelte` wired to `ReaderSelectionMenu` + `AnnotationEditor`
      (highlight, note, Define/Translate/Wikipedia/Search web/Search book/Add to Vocabulary).
- [x] Frontend: PDF page bookmarks (toolbar toggle + `ReaderContentsSheet` Bookmarks tab).
- [x] Frontend: `pdf-text-search.ts` + `PdfSearchPanel.svelte`, replacing the old inline search in
      `PdfReader.svelte`.
- [x] Frontend: `books/[id]/reader/+page.svelte` creates `BookAnnotationStore`/`BookBookmarkStore` for the PDF
      surface too, and understands a PDF JSON `location` (from the annotations/vocabulary dashboards) as a
      starting page.
- [x] Frontend: `AnnotationLibrary.svelte` shows "p. N" for PDF items.
- [x] Frontend: M6 reader theme applied to `PdfReader.svelte`'s chrome (stage/toolbar background only).
- [x] Frontend unit tests: `pdf-locator.test.ts`, `pdf-selection.test.ts`, `pdf-text-search.test.ts`,
      `PdfHighlightLayer` covered indirectly via the above + a rendering smoke test if time allows.
- [ ] Playwright e2e: `e2e/fixtures/make-annotation-pdf.py` (deterministic tiny multi-page PDF fixture) +
      `reader-pdf-annotations.spec.ts` covering highlight/reload, bookmark, search-and-navigate, and the
      dashboard showing the PDF entry with its page. **Not done — see "Gaps" below.**
- [x] `docs/nitrate/ROADMAP.md` M7 row updated.

## Files

- Backend: `Prismedia.Domain/Entities/Enums/Books/AnnotationLocatorKind.cs`,
  `Prismedia.Application/Books/PdfAnnotationLocator.cs`, `AnnotationExportWriter.cs`,
  `Prismedia.Infrastructure/Media/Books/EfBookAnnotationService.cs`, `EfBookBookmarkService.cs`, and their tests.
- Frontend: `apps/web-svelte/src/lib/reader/pdf-locator.ts`, `pdf-selection.ts`, `pdf-text-search.ts` (+ `.test.ts`
  siblings), `apps/web-svelte/src/lib/components/reader/PdfHighlightLayer.svelte`,
  `PdfSearchPanel.svelte`, `apps/web-svelte/src/lib/components/PdfReader.svelte`,
  `apps/web-svelte/src/routes/books/[id]/reader/+page.svelte`,
  `apps/web-svelte/src/lib/components/annotations/AnnotationLibrary.svelte`,
  `apps/web-svelte/src/lib/entities/book-reader-route.ts`,
  `apps/web-svelte/src/lib/reader/book-annotation-store.svelte.ts`, `book-bookmark-store.svelte.ts`.

## Test commands

- `pnpm test:backend` (dotnet test across the solution)
- `pnpm test:web-svelte` (vitest)
- `pnpm check` (svelte-check)
- `pnpm api:check` (orval drift check)
- Playwright: `reader-annotations`, `reader-bookmarks-and-library`, `reader-lookups`,
  `reader-settings-vocabulary` (existing EPUB specs, regression check) — PDF spec not added (see Gaps).

## Owner-verified checklist

- [ ] Windows/native run of the full stack (this session ran Linux/Docker-adjacent dev tooling only).
- [ ] Manual check of PDF highlight/bookmark/search on a real PDF and a real touch device (pinch-zoom +
      highlight interplay was not exercised beyond code review).
- [ ] Manual check that `pnpm api:generate`'s regenerated `codes.ts`/orval client matches what ships (regenerated
      here against a local dev API instance; verify no drift against the owner's copy).

## As-built notes / gaps

- No EF migration was needed (see decision 1): the existing generic locator columns were reused as designed by
  M3/M4, which is exactly the ceiling case the milestone anticipated.
- `PdfReader.svelte` line count before/after and the Playwright e2e spec status are recorded after implementation
  below.
