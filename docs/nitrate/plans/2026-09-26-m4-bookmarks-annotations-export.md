# M4 — Bookmarks, `/annotations` dashboard, export, and highlight re-anchoring

> **For agentic workers:** execute task by task (superpowers:executing-plans or subagent-driven-development).
> Tick the checkboxes and commit as you go.
>
> **Status:** written and executed on 2026-09-26, in the same session, at the owner's request ("just continue").
> The design defaults below were chosen by the agent, not reviewed in advance. The owner can override any of them
> (see "Owner decisions").

**Goal:**
- **Bookmarks:** the owner can bookmark pages in the EPUB reader and jump back to them.
- **Dashboard:** browse, search and filter every highlight and note across the library at `/annotations`.
- **Export:** export highlights and notes as Markdown, JSON or CSV.
- **Re-anchoring:** highlights survive a book file being replaced (the M3 decision #5 follow-up).

**Spec:** VISION "Bookmarks", "Annotation Dashboard", "Notes" (search, filter by book and colour), "Export".
**Builds on:** M3 (`2026-09-26-m3-reader-slice-1.md`) and ARCHITECTURE_ANALYSIS §16 and §20.

## Global constraints (same as M3)

- **Additive API only.** Existing routes are untouched (Apple apps).
- **Per user.** Hidden books behave as 404, and `Guid.Empty` is rejected.
- **Identifier discipline.** Every new closed set is a `[Code]` enum, and the frontend uses the generated `codes.ts`.
- **No new third-party dependencies.** CSV, JSON and Markdown are written by hand.
- **Baseline:** the one pre-existing backend failure (`RecoveryMigrationReopensOnlyCompletedImportedUsenetTransfers`)
  is not a regression.

## Design defaults (agent-chosen; owner may override)

1. **Bookmarks are their own table**, `book_bookmarks`, separate from highlights (VISION: "Bookmarks are separate
   from highlights").
   - Fields: id, user, book, `locator` (CFI of the page start), `spineIndex`, `position`, `chapterLabel`, optional
     `label`, `createdAt`/`updatedAt`/`deletedAt`.
   - Sync is the same as annotations: client ids, last-write-wins on `updatedAt`, and tombstones.
2. **Reader UI.**
   - A bookmark toggle in the reader's top bar marks or unmarks the current page. It is filled when the page
     already has a bookmark.
   - The contents sheet gains **Contents | Bookmarks** tabs. In Bookmarks you can jump to a bookmark, rename its
     label inline, or delete it.
3. **Dashboard at `/annotations`, in the nav's Books section** after eBooks, with a `highlighter` icon.
   - Search covers highlighted text and notes (case-insensitive substring).
   - Filters: book, colour, "with notes only".
   - Results are grouped by book, with per-book highlight and note counts.
   - Clicking an item opens the reader at that highlight: `/books/{id}/reader?location=<cfi>`, the existing
     launch-location route param.
4. **Export is done by the server:** `GET /api/annotations/export?format=markdown|json|csv[&bookId=…]` returns an
   attachment (`[Code]` enum `AnnotationExportFormat`). The web app downloads it with a plain same-origin link,
   which is authenticated by the session cookie.
5. **Stale highlights (M3 #5).**
   - **Server:** a highlight is flagged `isStale` when its stored `sourceSignature` differs from the book's current
     `BookContentState.SourceSignature`, meaning the file changed since it was made.
   - **Reader, on draw:** if the CFI no longer resolves to the stored `selectedText`, re-find the text quote in that
     section. Prefer matches whose `textBefore`/`textAfter` agree.
     - Found: save the repaired CFI (a normal last-write-wins update).
     - Not found: leave the highlight undrawn. The dashboard still lists it, with a "file changed" badge.

## File map

| Area | Files |
|---|---|
| Domain | `Prismedia.Domain/Entities/Enums/Books/AnnotationExportFormat.cs` |
| Contracts | `Prismedia.Contracts/Books/BookBookmarks.cs`, `Prismedia.Contracts/Books/AnnotationLibrary.cs` |
| Application | `Prismedia.Application/Books/IBookBookmarkService.cs`, `IAnnotationLibraryService.cs`, `AnnotationExportWriter.cs` (pure, unit-tested) |
| Infrastructure | `Persistence/Entities/BookBookmarkRow.cs`, `PrismediaBookAnnotationModelConfiguration.cs` (+bookmarks), DbSet, migration `AddBookBookmarks`, `Media/Books/EfBookBookmarkService.cs`, `Media/Books/EfAnnotationLibraryService.cs` |
| API | `Endpoints/Books/BookEndpoints.cs` (bookmark routes), `Endpoints/Annotations/AnnotationLibraryEndpoints.cs` (list, books, export) |
| Web | `lib/api/bookmarks.ts`, `lib/api/annotation-library.ts`, `lib/reader/book-bookmark-store.svelte.ts`, `lib/reader/annotation-reanchor.ts`, `components/reader/ReaderContentsSheet.svelte` (tabs), `BookFileReader.svelte`, reader route, `routes/annotations/+page.svelte`, nav (`packages/ui-svelte/src/navigation/app-shell-sections.ts`, `app-shell-nav-icon-map.ts`, `nav-catalog.test.ts`) |
| E2E | `e2e/reader-bookmarks-and-library.spec.ts` |

## Tasks

### Task 1: Bookmarks backend
- [x] **Step 1:** Domain/contracts/application:
  - `BookBookmark`, `UpsertBookBookmarkRequest` and `BookBookmarksResponse`;
  - `IBookBookmarkService` with a write result, reusing `BookAnnotationWritePolicy` for last-write-wins and clock
    plausibility.
- [x] **Step 2:** Add `BookBookmarkRow` with its mapping, DbSet and migration `AddBookBookmarks`. The migration must
  create only `book_bookmarks`, with cascade FKs to users and entities.
- [x] **Step 3:** Write `EfBookBookmarkService` test-first, covering the same cases as `EfBookAnnotationServiceTests`:
  owner-only, superseded, tombstone, other user → NotFound, non-book or hidden → NotFound, anonymous, invalid.
- [x] **Step 4:** Add `GET/PUT/DELETE /api/books/{id}/bookmarks[/{bookmarkId}]` with endpoint tests.
- [x] **Step 5:** Run `pnpm test:backend`, regenerate the client in the same commit, then commit.

### Task 2: Annotation library and export backend
- [x] **Step 1:** Add the `AnnotationExportFormat` `[Code]` enum (`markdown`, `json`, `csv`).
- [x] **Step 2:** Write `AnnotationExportWriter` test-first. It is pure: given a book-grouped list, it produces
  Markdown, JSON or CSV.
  - CSV follows RFC 4180 quoting and uses a UTF-8 BOM so Excel opens it correctly.
  - Markdown has one `##` heading per book, with each highlight as a quote and its note underneath.
  - JSON is a stable camelCase shape.
- [x] **Step 3:** Write `EfAnnotationLibraryService` test-first:
  - `ListAsync(query)`: search, book, colour and has-note filters; non-deleted only; only books visible to the user;
    newest first; offset-cursor paging.
  - `BooksAsync()`: per-book highlight and note counts plus a stale count.
  - `ExportAsync(format, bookId?)`.

  Items carry `bookTitle` and `isStale`.
- [x] **Step 4:** Add the endpoints in a new `AnnotationLibraryEndpoints` group at `/api/annotations`: `GET /`,
  `GET /books`, and `GET /export`, which returns a file with `fileDownloadName`. An unknown code returns 400
  `RequestInvalid`. Add endpoint tests.
- [x] **Step 5:** Run `pnpm test:backend`, regenerate the client, then commit.

### Task 3: Reader bookmarks
- [x] **Step 1:** Add `book-bookmark-store.svelte.ts`, the same shape as `BookAnnotationStore`, test-first.
- [x] **Step 2:** Give `ReaderContentsSheet` an optional `bookmarks` snippet. When present it renders
  `Tabs` with Contents | Bookmarks. Keep the existing sheet test green and add a tab test.
- [x] **Step 3:** `BookFileReader`:
  - props `bookmarks`, `onAddBookmark`, `onUpdateBookmark`, `onDeleteBookmark`;
  - a bookmark toggle in the controls, for reflowable books only;
  - a bookmarks list with jump, inline label edit, and delete.

  The "current page" is the latest `relocate` CFI, which is the start of the visible range. A page counts as
  bookmarked when a bookmark's CFI falls within the visible range: foliate's relocate `range` plus `CFI.compare`,
  with an exact-locator match as the fallback.
- [x] **Step 4:** Wire the store into the reader route: create it, refresh on open and on focus, and pass the props.
- [x] **Step 5:** Run the web tests and `pnpm check`, then commit.

### Task 4: Highlight re-anchoring
- [ ] **Step 1:** Write `annotation-reanchor.ts` test-first (jsdom). `findTextQuote(doc, quote)` returns the best
  `Range` for `selectedText` in a document, scored by agreement with `textBefore`/`textAfter`, or `null`.
- [ ] **Step 2:** `BookFileReader`: after `create-overlay`, check each annotation of that section.
  - If the CFI resolves and its text matches `selectedText`, draw it.
  - Otherwise, re-find the quote. If found, draw it and call `onUpdateAnnotation` with the new locator; if not, skip
    drawing.
- [ ] **Step 3:** Run the tests, then commit.

### Task 5: `/annotations` dashboard and export UI
- [ ] **Step 1:** Add the `annotation-library.ts` API wrapper with tests, and an export-URL helper.
- [ ] **Step 2:** Add `routes/annotations/+page.svelte`, following the calendar and search page patterns:
  - a header rail;
  - a `SearchInput`, with `Select` filters for book and colour, and a "Notes only" toggle;
  - an Export menu (Markdown / JSON / CSV, scoped to the current book filter);
  - results grouped by book with counts, each item showing its colour swatch, the quote, the note, and a "file
    changed" badge when stale;
  - clicking an item opens the reader at that highlight;
  - `Empty` states.
- [ ] **Step 3:** Nav: add `{ label: "Annotations", href: "/annotations", icon: "highlighter" }` to Books, and map the
  `highlighter` icon. Update `nav-catalog.test.ts`.
- [ ] **Step 4:** Add a component test for the page, covering grouping, filters and empty states, using mocked API
  wrappers.
- [ ] **Step 5:** Run the web tests and `pnpm check`, then commit.

### Task 6: E2E, verification, docs
- [ ] **Step 1:** `e2e/reader-bookmarks-and-library.spec.ts`:
  - bookmark a page, reload, and see it in the Bookmarks tab; jump to it;
  - highlight with a note, open `/annotations`, find it by searching the note, and filter by colour;
  - download the CSV export and check it contains the quote and the note.
- [ ] **Step 2:** Run the full backend and web suites, `pnpm check` and `pnpm api:check`, and the e2e (`--workers=1`).
- [ ] **Step 3:** Update `docs/THIRD_PARTY_LICENSES.md` (no new dependencies expected), ARCHITECTURE_ANALYSIS
  §20, and the ROADMAP (M4 → `Built; awaiting owner check`).
- [ ] **Step 4:** Owner-verified checklist (list it; do not claim it):
  - [ ] Bookmark on the Mac, then open the Bookmarks tab on the iPad and jump to it.
  - [ ] `/annotations` on the iPad: search a note and filter by colour; tapping an item opens the book at the
        highlight.
  - [ ] Download the CSV and open it in Excel or Numbers; open the Markdown export in a notes app.
  - [ ] Replace an EPUB file with a slightly different edition. Highlights whose text still exists are repaired;
        the rest show "file changed" on `/annotations`.

## Owner decisions

Defaults 1–5 above were not reviewed in advance. Tell the agent to change any of them. The likely candidates:
- where Annotations sits in the nav;
- whether bookmarks need labels at all;
- which export formats come first.
