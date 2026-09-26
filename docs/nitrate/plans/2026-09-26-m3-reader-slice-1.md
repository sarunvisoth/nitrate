# M3 — Reader Slice 1: EPUB highlights and notes, synced across devices

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to carry out this plan task by task. Steps use checkbox (`- [ ]`) syntax. Tick
> boxes in this file and commit them as you go, so a later session can resume.
>
> **Status: approved by the owner on 2026-09-26, with one addition (Task 10A: reading position follows you
> between devices). Not yet executed.** See "Owner decisions" at the end.

**Goal:** The owner can select text in an EPUB, highlight it in one of five colours, and optionally attach a
note. Highlights survive reloads and server restarts, and appear on the owner's other devices the next time the
book is opened or the tab regains focus. The **reading position** follows the owner the same way: every page turn
is saved, which is already true, and a reader that is already open catches up to a newer position from another
device when its tab regains focus (Task 10A).

**Architecture:**
- **Backend.** A new per-user `book_annotations` table (EF Core, Postgres). An Application port,
  `IBookAnnotationService`, with an EF implementation that enforces visibility, ownership and last-write-wins.
  Three additive routes on the existing `/api/books` group. A regenerated orval client.
- **Frontend.** `BookFileReader.svelte` gains selection capture, a selection menu and foliate-overlay drawing,
  driven by props. The reader route owns fetching and sync, the same way it already owns progress.
- **Locators.** A foliate range CFI, plus a text-quote fallback (`selectedText`, `textBefore`, `textAfter`).

**Tech stack:** .NET 10 / EF Core 10 / Npgsql, xUnit, Svelte 5 + SvelteKit static, orval, Vitest + Testing
Library, Playwright, vendored foliate-js (`overlayer.js`, `view.js`).

**Spec:** [`VISION.md`](../VISION.md): "Highlights", "Notes", "Progress Synchronization", and decisions log #6
(Apple apps compatibility) and #7 (vertical slices).
**Evidence:** [`ARCHITECTURE_ANALYSIS.md`](../ARCHITECTURE_ANALYSIS.md): §10–§14, §16 (extension points and
"Notes for annotation design") and §20 (design summary). Read §11, §14 and §16 before Task 7.

## Global Constraints

- **Additive only.** Do not change `/api/entities/{id}/progress`, the meaning of `progress.location`, or any
  other existing route. The closed-source Prismedia Apple apps depend on them (§15). New routes live under
  `/api/books/{id}/annotations`.
- **Identifier discipline** (AGENTS.md). `style`, `color` and `locatorKind` are `[Code]` enums in
  `Prismedia.Domain`, auto-discovered into `codes.ts` by `pnpm api:generate`. The frontend uses
  `ANNOTATION_COLOR`, `ANNOTATION_STYLE` and `ANNOTATION_LOCATOR_KIND` from
  `$lib/api/generated/codes`, never bare literals. (AGENTS.md mentions `ENUM_EXPORTS`, but the real mechanism is
  discovery of the Domain assembly in `Prismedia.Api/Codegen/CodesManifest.cs`. Nothing needs registering.)
- **Per-user and visibility.**
  - Every read and write is scoped to `ICurrentUserContext.UserId`.
  - `Guid.Empty` (system or anonymous) is rejected.
  - Book visibility goes through `IEntityVisibilityChecker`, as `EfBookChapterMappingService` does.
  - A hidden book, or an annotation owned by someone else, behaves as **404**.
- **Sync semantics.**
  - The server is the source of truth.
  - The client loads all of the book's annotations, tombstones included, on open and on
    `visibilitychange`→visible or `focus`.
  - Writes are idempotent upserts keyed by a client-generated UUID, carrying the client's `updatedAt`.
  - The server applies a write only if `incoming.updatedAt >= stored.updatedAt`. Otherwise it returns the
    stored row, and the client adopts it.
  - A delete is a tombstone write (`deletedAt`), subject to the same rule.
  - Timestamps more than 1 day in the future are rejected as invalid.
- **Scope.** Reflowable EPUB only. Fixed-layout EPUBs have no foliate overlayer (§14), so hide the menu there.
  - **In scope:** a selection menu with **Highlight** (yellow, blue, green, red, purple) and **Add Note**;
    tapping a highlight opens a popover to change the colour, edit or delete the note, or delete the highlight.
  - **Out of scope:** bookmarks, the `/annotations` page, PDF, dictionary, translation, and the
    underline/strikethrough/squiggly styles. These belong to M4, M5 and M7; build the menu so they are added
    as entries.
- **No new third-party dependencies.** If one becomes unavoidable, record it in `docs/THIRD_PARTY_LICENSES.md`.
- Nitrate overrides in `AGENTS.md`: no version bumps, no `CHANGELOG.md` edits, and small conventional commits.
- Baseline: `docs/nitrate/BASELINE.md`. The known pre-existing failure
  `RecoveryMigrationReopensOnlyCompletedImportedUsenetTransfers` is not a regression.

## File Map

| Path | Change | Task |
|---|---|---|
| `apps/backend/src/Prismedia.Domain/Entities/Enums/Books/AnnotationStyle.cs` | create | 1 |
| `apps/backend/src/Prismedia.Domain/Entities/Enums/Books/AnnotationColor.cs` | create | 1 |
| `apps/backend/src/Prismedia.Domain/Entities/Enums/Books/AnnotationLocatorKind.cs` | create | 1 |
| `apps/backend/tests/Prismedia.Domain.Tests/AnnotationCodeTests.cs` | create | 1 |
| `apps/backend/src/Prismedia.Contracts/Books/BookAnnotations.cs` | create | 2 |
| `apps/backend/src/Prismedia.Contracts/System/ApiProblemCodes.cs` | add `InvalidBookAnnotation` | 2 |
| `apps/backend/src/Prismedia.Application/Books/IBookAnnotationService.cs` | create | 2 |
| `apps/backend/src/Prismedia.Application/Books/BookAnnotationWritePolicy.cs` | create | 2 |
| `apps/backend/tests/Prismedia.Application.Tests/BookAnnotationWritePolicyTests.cs` | create | 2 |
| `apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/BookAnnotationRow.cs` | create | 3 |
| `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaBookAnnotationModelConfiguration.cs` | create | 3 |
| `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaModelConfiguration.cs` | call new configure method | 3 |
| `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaDbContext.cs` | add `BookAnnotations` DbSet | 3 |
| `apps/backend/src/Prismedia.Infrastructure/Persistence/Migrations/*_AddBookAnnotations.cs` (+ Designer, snapshot) | generated | 3 |
| `apps/backend/tests/Prismedia.Infrastructure.Tests/BookAnnotationMigrationPostgresTests.cs` | create | 3 |
| `apps/backend/src/Prismedia.Infrastructure/Media/Books/EfBookAnnotationService.cs` | create | 4 |
| `apps/backend/src/Prismedia.Infrastructure/DependencyInjection.cs` | register service | 4 |
| `apps/backend/tests/Prismedia.Infrastructure.Tests/EfBookAnnotationServiceTests.cs` | create | 4 |
| `apps/backend/src/Prismedia.Api/Endpoints/Books/BookEndpoints.cs` | add 3 routes | 5 |
| `apps/backend/tests/Prismedia.Api.Tests/BookAnnotationEndpointTests.cs` | create | 5 |
| `apps/web-svelte/src/lib/api/generated/**` | regenerated | 6 |
| `apps/web-svelte/src/lib/api/annotations.ts` (+ `.test.ts`) | create | 6 |
| `apps/web-svelte/src/lib/reader/annotation-sync.ts` (+ `.test.ts`) | create | 6 |
| `apps/web-svelte/src/lib/reader/annotation-colors.ts` | create | 7 |
| `apps/web-svelte/src/lib/reader/epub-selection.ts` (+ `.test.ts`) | create | 7 |
| `apps/web-svelte/src/lib/foliate-vendor.d.ts` | declare `overlayer.js` | 7 |
| `apps/web-svelte/src/lib/components/reader/ReaderSelectionMenu.svelte` (+ `.test.ts`) | create | 8 |
| `apps/web-svelte/src/lib/components/reader/AnnotationEditor.svelte` (+ `.test.ts`) | create | 8 |
| `apps/web-svelte/src/lib/components/BookFileReader.svelte` | selection, overlay drawing, gesture overlay rework | 9 |
| `apps/web-svelte/src/routes/books/[id]/reader/+page.svelte` | load/sync/write annotations | 10 |
| `apps/web-svelte/src/lib/reader/progress-catch-up.ts` (+ `.test.ts`) | create | 10A |
| `apps/web-svelte/src/lib/components/PdfReader.svelte` | export `goToPage` | 10A |
| `tests/fixtures/media/books/nitrate-annotation-fixture.epub` | create (generated, ~2 KB) | 11 |
| `e2e/auth.ts`, `e2e/book-fixture.ts`, `e2e/reader-annotations.spec.ts` | create | 11 |
| `docs/nitrate/ROADMAP.md` | M3 status | 12 |

---

## Task 1: Annotation code enums (Domain)

**Files:** create the three enums in `apps/backend/src/Prismedia.Domain/Entities/Enums/Books/`, plus
`apps/backend/tests/Prismedia.Domain.Tests/AnnotationCodeTests.cs`.

- [x] **Step 1: Write the failing test**

```csharp
using Prismedia.Domain.Entities;

namespace Prismedia.Domain.Tests;

public sealed class AnnotationCodeTests {
    [Theory]
    [InlineData(AnnotationColor.Yellow, "yellow")]
    [InlineData(AnnotationColor.Blue, "blue")]
    [InlineData(AnnotationColor.Green, "green")]
    [InlineData(AnnotationColor.Red, "red")]
    [InlineData(AnnotationColor.Purple, "purple")]
    public void ColorsRoundTripThroughTheirCodes(AnnotationColor color, string code) {
        Assert.Equal(code, color.ToCode());
        Assert.Equal(color, code.DecodeAs<AnnotationColor>());
    }

    [Fact]
    public void StyleAndLocatorKindHaveStableCodes() {
        Assert.Equal("highlight", AnnotationStyle.Highlight.ToCode());
        Assert.Equal("epub-cfi", AnnotationLocatorKind.EpubCfi.ToCode());
    }
}
```

Before writing the test, check how existing Domain tests import `ToCode`/`DecodeAs`: grep
`DecodeAs<ConsumptionEventKind>` and copy its `using` lines. Also check whether an unknown code throws or returns
a default (grep the `DecodeAs` implementation). Task 4's validation relies on that behaviour.

- [x] **Step 2: Run it and confirm it fails.** Run
  `dotnet test apps/backend/tests/Prismedia.Domain.Tests --filter "FullyQualifiedName~AnnotationCodeTests"`.
  Expected: a build error, because the types do not exist yet.

- [x] **Step 3: Implement the enums.** Use the same namespace as `ConsumptionEventKind`, i.e. `Prismedia.Domain.Entities`:

```csharp
namespace Prismedia.Domain.Entities;

/// <summary>Visual style used to draw a reader annotation over its text range.</summary>
public enum AnnotationStyle {
    /// <summary>Translucent filled highlight behind the text.</summary>
    [Code("highlight")]
    Highlight
}
```

```csharp
namespace Prismedia.Domain.Entities;

/// <summary>Closed palette of reader highlight colours.</summary>
public enum AnnotationColor {
    /// <summary>Yellow highlight (default).</summary>
    [Code("yellow")] Yellow,
    /// <summary>Blue highlight.</summary>
    [Code("blue")] Blue,
    /// <summary>Green highlight.</summary>
    [Code("green")] Green,
    /// <summary>Red highlight.</summary>
    [Code("red")] Red,
    /// <summary>Purple highlight.</summary>
    [Code("purple")] Purple
}
```

```csharp
namespace Prismedia.Domain.Entities;

/// <summary>Format of an annotation's <c>locator</c> string.</summary>
public enum AnnotationLocatorKind {
    /// <summary>EPUB canonical fragment identifier for a text range, as produced by foliate-js <c>view.getCFI</c>.</summary>
    [Code("epub-cfi")]
    EpubCfi
}
```

Format the enums to match the repository style: one attribute per line, as `ConsumptionEventKind.cs` does.

- [x] **Step 4: Run the test and confirm it passes.** Also run
  `dotnet test apps/backend/tests/Prismedia.Domain.Tests`. It is all green at baseline, and the architecture and
  identifier-discipline tests live there.
- [x] **Step 5: Commit.** `feat(api): add annotation style, colour and locator code enums`

---

## Task 2: Contracts, the Application port and the last-write-wins policy

**Files:**
- Create `Prismedia.Contracts/Books/BookAnnotations.cs`, `Prismedia.Application/Books/IBookAnnotationService.cs`,
  `Prismedia.Application/Books/BookAnnotationWritePolicy.cs` and the policy tests.
- Modify `ApiProblemCodes.cs`.

- [x] **Step 1: Contracts.** DTOs expose codes as strings, the same way `BookChapterAudioMapping.Origin` does.

```csharp
namespace Prismedia.Contracts.Books;

/// <summary>A user's highlight (optionally with a note) anchored to a range in a Book's readable file.</summary>
/// <param name="Id">Client-generated stable identifier.</param>
/// <param name="BookId">Book Entity the annotation belongs to.</param>
/// <param name="LocatorKind">Locator format code (<c>epub-cfi</c>).</param>
/// <param name="Locator">Format-specific range locator; for EPUB a range CFI.</param>
/// <param name="SpineIndex">Reading-order section index, used for ordering.</param>
/// <param name="Position">Fraction 0..1 through the book at the start of the range, used for ordering.</param>
/// <param name="SelectedText">The exact highlighted text (text-quote fallback for re-anchoring).</param>
/// <param name="TextBefore">Up to 64 characters immediately before the selection.</param>
/// <param name="TextAfter">Up to 64 characters immediately after the selection.</param>
/// <param name="Style">Annotation style code.</param>
/// <param name="Color">Annotation colour code.</param>
/// <param name="Note">Optional user note.</param>
/// <param name="SourceSignature">Readable-file signature when the annotation was first saved; a later mismatch means the file changed.</param>
/// <param name="CreatedAt">When the annotation was first stored.</param>
/// <param name="UpdatedAt">Last-write-wins clock supplied by the writing client.</param>
/// <param name="DeletedAt">Tombstone time; non-null means deleted.</param>
public sealed record BookAnnotation(
    Guid Id,
    Guid BookId,
    string LocatorKind,
    string Locator,
    int SpineIndex,
    double Position,
    string SelectedText,
    string? TextBefore,
    string? TextAfter,
    string Style,
    string Color,
    string? Note,
    string? SourceSignature,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);

/// <summary>All of the current user's annotations for one Book, tombstones included.</summary>
public sealed record BookAnnotationsResponse(IReadOnlyList<BookAnnotation> Annotations);

/// <summary>Create-or-update payload for one annotation.</summary>
public sealed record UpsertBookAnnotationRequest(
    string LocatorKind,
    string Locator,
    int SpineIndex,
    double Position,
    string SelectedText,
    string? TextBefore,
    string? TextAfter,
    string Style,
    string Color,
    string? Note,
    DateTimeOffset UpdatedAt);
```

Add this to `ApiProblemCodes.cs`, next to `InvalidBookChapterMapping`:

```csharp
    /// <summary>Problem code <c>invalid_book_annotation</c>.</summary>
    public const string InvalidBookAnnotation = "invalid_book_annotation";
```

- [x] **Step 2: Application port**

```csharp
using Prismedia.Contracts.Books;

namespace Prismedia.Application.Books;

/// <summary>Reads and writes the current user's annotations on a visible Book with last-write-wins semantics.</summary>
public interface IBookAnnotationService {
    /// <summary>Lists the current user's annotations for a visible Book, including tombstones.</summary>
    /// <returns>The annotations, or <c>null</c> when the Book is missing, hidden, or there is no user.</returns>
    Task<BookAnnotationsResponse?> ListAsync(Guid bookId, CancellationToken cancellationToken);

    /// <summary>Creates or updates one annotation; applied only when the incoming clock is not older than the stored one.</summary>
    Task<BookAnnotationWriteResult> UpsertAsync(
        Guid bookId,
        Guid annotationId,
        UpsertBookAnnotationRequest request,
        CancellationToken cancellationToken);

    /// <summary>Tombstones one annotation under the same last-write-wins rule.</summary>
    Task<BookAnnotationWriteResult> DeleteAsync(
        Guid bookId,
        Guid annotationId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}

/// <summary>Outcome of an annotation write.</summary>
public enum BookAnnotationWriteStatus {
    /// <summary>The write was stored.</summary>
    Applied,
    /// <summary>A newer stored version won; <see cref="BookAnnotationWriteResult.Annotation"/> is that version.</summary>
    Superseded,
    /// <summary>The Book is missing/hidden, there is no user, or the annotation belongs to another user or Book.</summary>
    NotFound,
    /// <summary>The payload failed validation; see <see cref="BookAnnotationWriteResult.Error"/>.</summary>
    Invalid
}

/// <summary>Result of an annotation write.</summary>
public sealed record BookAnnotationWriteResult(
    BookAnnotationWriteStatus Status,
    BookAnnotation? Annotation,
    string? Error);
```

- [x] **Step 3: Write the failing policy tests** (`Prismedia.Application.Tests/BookAnnotationWritePolicyTests.cs`)

```csharp
using Prismedia.Application.Books;

namespace Prismedia.Application.Tests;

public sealed class BookAnnotationWritePolicyTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstWriteAlwaysApplies() =>
        Assert.True(BookAnnotationWritePolicy.ShouldApply(stored: null, incoming: Now));

    [Fact]
    public void EqualOrNewerClockApplies() {
        Assert.True(BookAnnotationWritePolicy.ShouldApply(Now, Now));
        Assert.True(BookAnnotationWritePolicy.ShouldApply(Now, Now.AddMilliseconds(1)));
    }

    [Fact]
    public void OlderClockIsSuperseded() =>
        Assert.False(BookAnnotationWritePolicy.ShouldApply(Now, Now.AddSeconds(-1)));

    [Fact]
    public void ClocksFarInTheFutureAreRejected() {
        Assert.True(BookAnnotationWritePolicy.IsPlausibleClock(Now.AddHours(23), Now));
        Assert.False(BookAnnotationWritePolicy.IsPlausibleClock(Now.AddDays(2), Now));
    }
}
```

- [x] **Step 4: Run and confirm failure** (a build error), then implement:

```csharp
namespace Prismedia.Application.Books;

/// <summary>Last-write-wins rules shared by every annotation write path.</summary>
public static class BookAnnotationWritePolicy {
    /// <summary>Maximum tolerated client clock lead over the server clock.</summary>
    public static readonly TimeSpan MaximumClockLead = TimeSpan.FromDays(1);

    /// <summary>True when an incoming write should replace the stored version.</summary>
    /// <param name="stored">Stored <c>updatedAt</c>, or null when nothing is stored yet.</param>
    /// <param name="incoming">Client-supplied <c>updatedAt</c> of the write.</param>
    public static bool ShouldApply(DateTimeOffset? stored, DateTimeOffset incoming) =>
        stored is null || incoming >= stored.Value;

    /// <summary>True when a client clock is not implausibly far ahead of the server clock.</summary>
    public static bool IsPlausibleClock(DateTimeOffset incoming, DateTimeOffset serverNow) =>
        incoming <= serverNow + MaximumClockLead;
}
```

- [x] **Step 5: Run** `dotnet test apps/backend/tests/Prismedia.Application.Tests` and confirm it is green.
- [x] **Step 6: Commit.** `feat(api): add book annotation contracts and last-write-wins policy`

---

## Task 3: Persistence (row, EF mapping, migration)

**Files:** see the File Map. Follow `EntityConsumptionEventRow` and its configuration in
`PrismediaCapabilityModelConfiguration.cs` (`entity_consumption_events`). Use snake_case columns,
`ValueGeneratedNever` ids, code conversions and cascade FKs.

- [x] **Step 1: Row**

```csharp
using Prismedia.Domain.Entities;

namespace Prismedia.Infrastructure.Persistence.Entities;

/// <summary>One user's annotation on a Book (per-user, many rows, tombstoned deletes).</summary>
public sealed class BookAnnotationRow {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BookId { get; set; }
    public AnnotationLocatorKind LocatorKind { get; set; }
    public string Locator { get; set; } = string.Empty;
    public int SpineIndex { get; set; }
    public double Position { get; set; }
    public string SelectedText { get; set; } = string.Empty;
    public string? TextBefore { get; set; }
    public string? TextAfter { get; set; }
    public AnnotationStyle Style { get; set; }
    public AnnotationColor Color { get; set; }
    public string? Note { get; set; }
    public string? SourceSignature { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

- [x] **Step 2: Mapping.** Create `PrismediaBookAnnotationModelConfiguration.cs` as a new partial of
  `PrismediaModelConfiguration`. Add `ConfigureBookAnnotations(modelBuilder);` to `ConfigurePrismediaModel` in
  `PrismediaModelConfiguration.cs`.

```csharp
using Microsoft.EntityFrameworkCore;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Persistence;

internal static partial class PrismediaModelConfiguration {
    private static void ConfigureBookAnnotations(ModelBuilder modelBuilder) {
        modelBuilder.Entity<BookAnnotationRow>(entity => {
            entity.ToTable("book_annotations");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.UserId).HasColumnName("user_id");
            entity.Property(row => row.BookId).HasColumnName("book_id");
            entity.Property(row => row.LocatorKind)
                .HasColumnName("locator_kind")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<AnnotationLocatorKind>());
            entity.Property(row => row.Locator).HasColumnName("locator").HasMaxLength(4096).IsRequired();
            entity.Property(row => row.SpineIndex).HasColumnName("spine_index");
            entity.Property(row => row.Position).HasColumnName("position");
            entity.Property(row => row.SelectedText).HasColumnName("selected_text").HasMaxLength(8000).IsRequired();
            entity.Property(row => row.TextBefore).HasColumnName("text_before").HasMaxLength(256);
            entity.Property(row => row.TextAfter).HasColumnName("text_after").HasMaxLength(256);
            entity.Property(row => row.Style)
                .HasColumnName("style")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<AnnotationStyle>());
            entity.Property(row => row.Color)
                .HasColumnName("color")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<AnnotationColor>());
            entity.Property(row => row.Note).HasColumnName("note").HasMaxLength(20000);
            entity.Property(row => row.SourceSignature).HasColumnName("source_signature").HasMaxLength(512);
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.UpdatedAt).HasColumnName("updated_at");
            entity.Property(row => row.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(row => new { row.UserId, row.BookId });
            entity.HasIndex(row => new { row.UserId, row.UpdatedAt });
            entity.HasOne<EntityRow>().WithMany().HasForeignKey(row => row.BookId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserRow>().WithMany().HasForeignKey(row => row.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

In `PrismediaDbContext.cs`, next to `EntityConsumptionEvents`, add:
`public DbSet<BookAnnotationRow> BookAnnotations => Set<BookAnnotationRow>();`

- [x] **Step 3: Generate the migration.** `dotnet-ef` 10.0.8 is pinned in `apps/backend/dotnet-tools.json`.

```bash
cd apps/backend
dotnet tool restore
dotnet ef migrations add AddBookAnnotations \
  --project src/Prismedia.Infrastructure --startup-project src/Prismedia.Api \
  --output-dir Persistence/Migrations
```

Review the generated file as required by `docs/database-migrations.md`. `Up` must create **only**
`book_annotations`, its two FKs and its two indexes. `Down` must drop it. Nothing else may change. If the
snapshot diff touches other tables, stop: the model has drifted and needs investigating.

- [x] **Step 4: Postgres migration test.** Copy the connection and skip helper pattern from
  `PostgresTestDatabase.cs`, as used by `UserEntityStateConcurrencyPostgresTests.cs`. Migrate a fresh database to
  latest, insert a user, a Book entity and one `BookAnnotationRow`, then assert:
  - it round-trips with codes intact;
  - deleting the Book cascades the annotation away.

  Run
  `dotnet test apps/backend/tests/Prismedia.Infrastructure.Tests --filter "FullyQualifiedName~BookAnnotationMigrationPostgresTests"`,
  with Postgres up (BASELINE "Running locally").
- [x] **Step 5: Commit** the row, mapping, DbSet, migration, Designer, snapshot and test together:
  `feat(api): add book_annotations table`

---

## Task 4: `EfBookAnnotationService`

**Files:** create `Prismedia.Infrastructure/Media/Books/EfBookAnnotationService.cs` and its tests. Register it in
`DependencyInjection.cs` next to `IBookChapterMappingService` (ARCHITECTURE_ANALYSIS §16a).

- [ ] **Step 1: Write the failing tests** (`EfBookAnnotationServiceTests.cs`). Use EF InMemory, as in
  `EpubBookContentsServiceTests.CreateContext`, with `TestUserContext.MemberAs(userId)` / `Anonymous()` and an
  always-visible `IEntityVisibilityChecker` fake. Seed an `EntityRow` with `KindCode = EntityKind.Book.ToCode()`;
  look up an existing test that seeds an `EntityRow` for the required properties. Cover these cases:
  1. `Upsert_then_List_returns_the_annotation_for_its_owner_only`: user A writes, then user B's `ListAsync` is empty.
  2. `Older_updatedAt_is_Superseded_and_returns_the_stored_version`.
  3. `Delete_sets_DeletedAt_and_List_still_returns_the_tombstone`.
  4. `Upsert_with_an_id_owned_by_another_user_is_NotFound` (do not leak the row or overwrite it).
  5. `Upsert_for_a_non_Book_entity_or_hidden_Book_is_NotFound`.
  6. `Invalid_codes_empty_locator_future_clock_and_out_of_range_position_are_Invalid`.
  7. `Anonymous_user_gets_NotFound` (`Guid.Empty`).
  8. `CreatedAt_is_kept_on_update_and_SourceSignature_is_copied_from_BookContentStates_on_create`
     (seed a `BookContentStateRow { BookId, SourceSignature = "epub:1:2:3" }`).

- [ ] **Step 2: Run and confirm failure**, then implement:

```csharp
using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Books;
using Prismedia.Application.Entities;
using Prismedia.Application.Security;
using Prismedia.Contracts.Books;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Media.Books;

/// <summary>EF store for per-user Book annotations with last-write-wins writes and tombstoned deletes.</summary>
internal sealed class EfBookAnnotationService(
    PrismediaDbContext db,
    IEntityVisibilityChecker visibility,
    ICurrentUserContext currentUser,
    TimeProvider clock) : IBookAnnotationService {
    private const int ContextMaxLength = 256;

    /// <inheritdoc />
    public async Task<BookAnnotationsResponse?> ListAsync(Guid bookId, CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty || !await IsVisibleBookAsync(bookId, cancellationToken)) {
            return null;
        }

        var rows = await db.BookAnnotations.AsNoTracking()
            .Where(row => row.UserId == currentUser.UserId && row.BookId == bookId)
            .OrderBy(row => row.SpineIndex).ThenBy(row => row.Position).ThenBy(row => row.CreatedAt)
            .ToListAsync(cancellationToken);
        return new BookAnnotationsResponse(rows.Select(ToContract).ToList());
    }

    /// <inheritdoc />
    public async Task<BookAnnotationWriteResult> UpsertAsync(
        Guid bookId,
        Guid annotationId,
        UpsertBookAnnotationRequest request,
        CancellationToken cancellationToken) {
        var owned = await LoadOwnedAsync(bookId, annotationId, cancellationToken);
        if (owned.Status is not null) {
            return owned.Status;
        }

        var error = Validate(request);
        if (error is not null) {
            return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Invalid, null, error);
        }

        var row = owned.Row;
        if (row is not null && !BookAnnotationWritePolicy.ShouldApply(row.UpdatedAt, request.UpdatedAt)) {
            return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Superseded, ToContract(row), null);
        }

        if (row is null) {
            row = new BookAnnotationRow {
                Id = annotationId,
                UserId = currentUser.UserId,
                BookId = bookId,
                CreatedAt = clock.GetUtcNow(),
                SourceSignature = await db.BookContentStates.AsNoTracking()
                    .Where(state => state.BookId == bookId)
                    .Select(state => state.SourceSignature)
                    .FirstOrDefaultAsync(cancellationToken)
            };
            db.BookAnnotations.Add(row);
        }

        row.LocatorKind = request.LocatorKind.DecodeAs<AnnotationLocatorKind>();
        row.Locator = request.Locator.Trim();
        row.SpineIndex = request.SpineIndex;
        row.Position = request.Position;
        row.SelectedText = request.SelectedText;
        row.TextBefore = Clip(request.TextBefore);
        row.TextAfter = Clip(request.TextAfter);
        row.Style = request.Style.DecodeAs<AnnotationStyle>();
        row.Color = request.Color.DecodeAs<AnnotationColor>();
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note;
        row.UpdatedAt = request.UpdatedAt;
        row.DeletedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Applied, ToContract(row), null);
    }

    /// <inheritdoc />
    public async Task<BookAnnotationWriteResult> DeleteAsync(
        Guid bookId,
        Guid annotationId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken) {
        var owned = await LoadOwnedAsync(bookId, annotationId, cancellationToken);
        if (owned.Status is not null) {
            return owned.Status;
        }

        if (owned.Row is null) {
            return new BookAnnotationWriteResult(BookAnnotationWriteStatus.NotFound, null, null);
        }

        if (!BookAnnotationWritePolicy.IsPlausibleClock(updatedAt, clock.GetUtcNow())) {
            return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Invalid, null, "updatedAt is too far in the future.");
        }

        if (!BookAnnotationWritePolicy.ShouldApply(owned.Row.UpdatedAt, updatedAt)) {
            return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Superseded, ToContract(owned.Row), null);
        }

        owned.Row.UpdatedAt = updatedAt;
        owned.Row.DeletedAt = updatedAt;
        await db.SaveChangesAsync(cancellationToken);
        return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Applied, ToContract(owned.Row), null);
    }

    private async Task<(BookAnnotationRow? Row, BookAnnotationWriteResult? Status)> LoadOwnedAsync(
        Guid bookId,
        Guid annotationId,
        CancellationToken cancellationToken) {
        var notFound = new BookAnnotationWriteResult(BookAnnotationWriteStatus.NotFound, null, null);
        if (currentUser.UserId == Guid.Empty || annotationId == Guid.Empty ||
            !await IsVisibleBookAsync(bookId, cancellationToken)) {
            return (null, notFound);
        }

        var row = await db.BookAnnotations.FirstOrDefaultAsync(r => r.Id == annotationId, cancellationToken);
        if (row is not null && (row.UserId != currentUser.UserId || row.BookId != bookId)) {
            return (null, notFound);
        }

        return (row, null);
    }

    private string? Validate(UpsertBookAnnotationRequest request) {
        // Use the repository's non-throwing code lookup here (see Task 1, Step 1: check how DecodeAs treats
        // unknown codes, and prefer a TryDecode helper if one exists).
        if (!IsKnown<AnnotationLocatorKind>(request.LocatorKind)) return "Unknown locatorKind.";
        if (!IsKnown<AnnotationStyle>(request.Style)) return "Unknown style.";
        if (!IsKnown<AnnotationColor>(request.Color)) return "Unknown color.";
        if (string.IsNullOrWhiteSpace(request.Locator) || request.Locator.Length > 4096) return "locator is required (max 4096).";
        if (string.IsNullOrEmpty(request.SelectedText) || request.SelectedText.Length > 8000) return "selectedText is required (max 8000).";
        if (request.SpineIndex < 0) return "spineIndex must be >= 0.";
        if (double.IsNaN(request.Position) || request.Position is < 0 or > 1) return "position must be within 0..1.";
        if (request.Note is { Length: > 20000 }) return "note is too long (max 20000).";
        if (!BookAnnotationWritePolicy.IsPlausibleClock(request.UpdatedAt, clock.GetUtcNow())) return "updatedAt is too far in the future.";
        return null;
    }

    private async Task<bool> IsVisibleBookAsync(Guid bookId, CancellationToken cancellationToken) =>
        await visibility.IsVisibleAsync(bookId, cancellationToken) &&
        await db.Entities.AsNoTracking().AnyAsync(
            row => row.Id == bookId && row.KindCode == EntityKind.Book.ToCode(),
            cancellationToken);

    private static string? Clip(string? value) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= ContextMaxLength ? value : value[..ContextMaxLength];

    private static BookAnnotation ToContract(BookAnnotationRow row) => new(
        row.Id, row.BookId, row.LocatorKind.ToCode(), row.Locator, row.SpineIndex, row.Position,
        row.SelectedText, row.TextBefore, row.TextAfter, row.Style.ToCode(), row.Color.ToCode(), row.Note,
        row.SourceSignature, row.CreatedAt, row.UpdatedAt, row.DeletedAt);
}
```

Implement `IsKnown<T>` with whatever non-throwing lookup `CodecRegistry` offers; grep `TryDecode` in
`Prismedia.Domain`. If none exists, wrap `DecodeAs` in a `try`/`catch` for the specific exception it throws. Check
that `TimeProvider` is registered in DI (grep `TimeProvider.System`). If it is not, register
`services.TryAddSingleton(TimeProvider.System);`. In the tests, use
`Microsoft.Extensions.Time.Testing.FakeTimeProvider` if the test project already references it; otherwise use a
tiny test subclass of `TimeProvider` overriding `GetUtcNow()`.

Register the service: `services.AddScoped<IBookAnnotationService, EfBookAnnotationService>();`

- [ ] **Step 3: Run the new tests** and confirm they are green.
- [ ] **Step 4: Commit.** `feat(api): persist book annotations with last-write-wins sync`

---

## Task 5: API routes

**Files:** modify `BookEndpoints.cs`; create `BookAnnotationEndpointTests.cs`, copying the fake-service and
factory pattern from `BookChapterMappingEndpointTests.cs`.

- [ ] **Step 1: Write the failing endpoint tests.** Use a `FakeBookAnnotationService`, `WithTestAuth()` and
  `CreateAuthenticatedClient()`. Cover:
  - GET gives 200 with the list, and 404 when the service returns null.
  - PUT: `Applied` gives 200 with the body; `Superseded` gives 200 with the stored version; `Invalid` gives 400
    with `ApiProblemCodes.InvalidBookAnnotation`; `NotFound` gives 404.
  - DELETE `?updatedAt=` gives 200 with the tombstone. DELETE with no `updatedAt` gives 400.
  - Unauthenticated GET gives 401. Build a client without auth, following an existing 401 test; grep
    `Status401Unauthorized` in `Prismedia.Api.Tests`.
- [ ] **Step 2: Implement.** Add the routes inside `MapBookEndpoints`, before `return group;`:

```csharp
        group.MapGet("/{id:guid}/annotations", async (
            Guid id,
            IBookAnnotationService annotations,
            CancellationToken cancellationToken) => {
                var result = await annotations.ListAsync(id, cancellationToken);
                return result is null
                    ? Results.NotFound(new ApiProblem(ApiProblemCodes.EntityNotFound, $"Book '{id}' was not found."))
                    : Results.Ok(result);
            })
            .WithName("ListBookAnnotations")
            .WithSummary("List the current user's highlights and notes for a Book, including deleted tombstones.")
            .Produces<BookAnnotationsResponse>()
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/annotations/{annotationId:guid}", async (
            Guid id,
            Guid annotationId,
            UpsertBookAnnotationRequest request,
            IBookAnnotationService annotations,
            CancellationToken cancellationToken) =>
                ToAnnotationResult(id, await annotations.UpsertAsync(id, annotationId, request, cancellationToken)))
            .WithName("UpsertBookAnnotation")
            .WithSummary("Create or update one annotation (last write wins on updatedAt).")
            .Produces<BookAnnotation>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/annotations/{annotationId:guid}", async (
            Guid id,
            Guid annotationId,
            DateTimeOffset updatedAt,
            IBookAnnotationService annotations,
            CancellationToken cancellationToken) =>
                ToAnnotationResult(id, await annotations.DeleteAsync(id, annotationId, updatedAt, cancellationToken)))
            .WithName("DeleteBookAnnotation")
            .WithSummary("Tombstone one annotation (last write wins on updatedAt).")
            .Produces<BookAnnotation>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);
```

Add this private helper to `BookEndpoints`:

```csharp
    private static IResult ToAnnotationResult(Guid bookId, BookAnnotationWriteResult result) =>
        result.Status switch {
            BookAnnotationWriteStatus.Applied or BookAnnotationWriteStatus.Superseded => Results.Ok(result.Annotation),
            BookAnnotationWriteStatus.NotFound => Results.NotFound(new ApiProblem(
                ApiProblemCodes.EntityNotFound, $"Annotation on book '{bookId}' was not found.")),
            BookAnnotationWriteStatus.Invalid => Results.BadRequest(new ApiProblem(
                ApiProblemCodes.InvalidBookAnnotation, result.Error ?? "The annotation is invalid.")),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
```

Minimal APIs bind `DateTimeOffset updatedAt` from the query string, and a missing value gives 400. Confirm this in
the test. Do **not** add `.RequireAdmin()`: the `/api` authentication middleware already requires a user.

- [ ] **Step 3: Run** `pnpm test:backend` and compare with the baseline. Only the new tests change.
- [ ] **Step 4: Commit.** `feat(api): add book annotation routes`

---

## Task 6: Regenerated client, API wrapper and sync helper

- [ ] **Step 1: Regenerate.** Start the dev API (BASELINE "Running locally"), then run `pnpm api:generate` and
  `pnpm api:check`. Confirm that `codes.ts` now has `ANNOTATION_COLOR`, `ANNOTATION_STYLE` and
  `ANNOTATION_LOCATOR_KIND`, and note the exact names. Confirm that `generated/prismedia.ts` has
  `listBookAnnotations`, `upsertBookAnnotation` and `deleteBookAnnotation`, and note their exact signatures (orval
  names come from `.WithName`). Commit the generated files on their own:
  `chore(web): regenerate API client for book annotations`.

- [ ] **Step 2: Write the failing tests** for `apps/web-svelte/src/lib/reader/annotation-sync.ts`, which is pure
  and unit-testable:

```ts
import { describe, expect, it } from "vitest";
import { mergeAnnotation, visibleAnnotations } from "./annotation-sync";

const base = {
  id: "a1", bookId: "b1", locatorKind: "epub-cfi", locator: "epubcfi(/6/4!/4/2,/1:0,/1:5)",
  spineIndex: 1, position: 0.1, selectedText: "Hello", textBefore: null, textAfter: null,
  style: "highlight", color: "yellow", note: null, sourceSignature: null,
  createdAt: "2026-09-26T10:00:00Z", updatedAt: "2026-09-26T10:00:00Z", deletedAt: null,
};

describe("annotation-sync", () => {
  it("keeps the newer updatedAt (last write wins)", () => {
    const newer = { ...base, color: "blue", updatedAt: "2026-09-26T11:00:00Z" };
    expect(mergeAnnotation(base, newer).color).toBe("blue");
    expect(mergeAnnotation(newer, base).color).toBe("blue");
  });

  it("hides tombstones from the drawable set", () => {
    const deleted = { ...base, id: "a2", deletedAt: "2026-09-26T12:00:00Z" };
    expect(visibleAnnotations([base, deleted]).map((a) => a.id)).toEqual(["a1"]);
  });
});
```

(Replace the `"epub-cfi"`, `"highlight"` and `"yellow"` literals in the fixture with the generated `codes.ts`
constants.)

- [ ] **Step 3: Implement** `annotation-sync.ts`, using the generated `BookAnnotation` model type:

```ts
import type { BookAnnotation } from "$lib/api/generated/model";

/** Returns whichever version of the same annotation has the later `updatedAt` (ties keep `incoming`). */
export function mergeAnnotation(current: BookAnnotation | undefined, incoming: BookAnnotation): BookAnnotation {
  if (!current) return incoming;
  return Date.parse(incoming.updatedAt) >= Date.parse(current.updatedAt) ? incoming : current;
}

/** Folds a server list into the local map, keeping the newest version of each id. */
export function mergeAll(
  local: ReadonlyMap<string, BookAnnotation>,
  server: readonly BookAnnotation[],
): Map<string, BookAnnotation> {
  const next = new Map(local);
  for (const item of server) next.set(item.id, mergeAnnotation(next.get(item.id), item));
  return next;
}

/** Annotations that should be drawn (not tombstoned), in reading order. */
export function visibleAnnotations(items: Iterable<BookAnnotation>): BookAnnotation[] {
  return [...items]
    .filter((item) => !item.deletedAt)
    .sort((a, b) => a.spineIndex - b.spineIndex || a.position - b.position);
}
```

- [ ] **Step 4: Implement** `apps/web-svelte/src/lib/api/annotations.ts`, following the `books.ts` pattern
  (generated function → `requestInit` → `unwrapGenerated`), with tests that mock `fetch` as in `files.test.ts`.
  Export `fetchBookAnnotations(bookId)`, `saveBookAnnotation(bookId, annotation)` (a PUT built from the
  annotation's fields) and `deleteBookAnnotation(bookId, id, updatedAt)`.
- [ ] **Step 5: Run** `pnpm test:web-svelte` and `pnpm check`, and confirm they are green.
- [ ] **Step 6: Commit.** `feat(web): add book annotation API wrapper and sync helpers`

---

## Task 7: Selection and locator helpers

**Files:** `apps/web-svelte/src/lib/reader/epub-selection.ts` (+ test), `annotation-colors.ts`, and
`foliate-vendor.d.ts`.

- [ ] **Step 1: Write the failing tests** (jsdom):

```ts
import { describe, expect, it } from "vitest";
import { textQuoteFor, selectionAnchorRect } from "./epub-selection";

describe("textQuoteFor", () => {
  it("captures the exact text plus bounded context on both sides", () => {
    document.body.innerHTML = "<p>The quick brown fox jumps over the lazy dog.</p>";
    const text = document.querySelector("p")!.firstChild!;
    const range = document.createRange();
    range.setStart(text, 10);
    range.setEnd(text, 19);
    expect(textQuoteFor(range, 6)).toEqual({ selectedText: "brown fox", textBefore: "quick ", textAfter: " jumps" });
  });
});

describe("selectionAnchorRect", () => {
  it("offsets an iframe-local rect by the frame's position in the top window", () => {
    const rect = selectionAnchorRect(
      { left: 10, top: 20, width: 30, height: 5 } as DOMRect,
      { left: 100, top: 200 } as DOMRect,
    );
    expect(rect).toEqual({ left: 110, top: 220, width: 30, height: 5 });
  });
});
```

- [ ] **Step 2: Implement:**

```ts
/** Exact text plus up to `contextLength` characters of surrounding text within the same document. */
export function textQuoteFor(range: Range, contextLength = 64) {
  const doc = range.startContainer.ownerDocument ?? document;
  const before = doc.createRange();
  before.setStart(doc.body, 0);
  before.setEnd(range.startContainer, range.startOffset);
  const after = doc.createRange();
  after.setStart(range.endContainer, range.endOffset);
  after.setEnd(doc.body, doc.body.childNodes.length);
  return {
    selectedText: range.toString(),
    textBefore: before.toString().slice(-contextLength) || null,
    textAfter: after.toString().slice(0, contextLength) || null,
  };
}

/** Converts a rect measured inside a section iframe into top-window coordinates. */
export function selectionAnchorRect(
  local: Pick<DOMRect, "left" | "top" | "width" | "height">,
  frame: Pick<DOMRect, "left" | "top"> | null,
) {
  return {
    left: local.left + (frame?.left ?? 0),
    top: local.top + (frame?.top ?? 0),
    width: local.width,
    height: local.height,
  };
}
```

- [ ] **Step 3: Colours.** Create `annotation-colors.ts`, which maps each `ANNOTATION_COLOR` code to a CSS colour
  for the overlay and the swatches. Keep the values in this one module. Highlight colours are user content, not
  entity-spectrum UI, so they sit outside the design-system accent rules. Check the contrast of the text under
  `--overlayer-highlight-opacity: .3` against the reader's dark theme (`BookFileReader.svelte` `contentStyles`).
- [ ] **Step 4: Types.** Add `declare module "$lib/vendor/foliate-js/overlayer.js";` to `foliate-vendor.d.ts`
  (§16c: `Overlayer` is exported from `overlayer.js:4`).
- [ ] **Step 5: Run** the tests and `pnpm check`, then commit: `feat(web): add EPUB selection and text-quote helpers`

---

## Task 8: Selection menu and annotation editor components

**Files:** `ReaderSelectionMenu.svelte` and `AnnotationEditor.svelte` under
`apps/web-svelte/src/lib/components/reader/`, each with a Testing Library test like `ReaderContentsSheet.test.ts`.

- **`ReaderSelectionMenu`**
  - Props: `anchor` (a top-window rect, or null), `actions: ReaderSelectionAction[]` and `onDismiss`.
    `ReaderSelectionAction` is `{ id: string; label: string; icon?: Component; run: () => void }`.
  - It renders **only the entries it is given**. M5 adds Define, Translate and the rest by passing more entries.
  - Build it from `@prismedia/ui-svelte` (`Popover`, `Button`). Position it absolutely inside the reader stage at
    the anchor. Do not portal it: the reader is inside a fullscreen `Dialog`, and a portal would land underneath
    it (§16c).
  - Keyboard: Escape dismisses. Every action is a real button; no hover-only affordances (AGENTS.md).
- **`AnnotationEditor`**
  - Props: `annotation`, `onChangeColor(code)`, `onSaveNote(text)` and `onDelete()`.
  - Five colour swatches keyed by `ANNOTATION_COLOR`, a `Textarea` for the note, and Save / Delete highlight
    buttons.
  - Used both for **Add Note** straight after highlighting and when a highlight is tapped.
- **Tests:**
  - The menu renders the given entries and calls `run`.
  - Escape calls `onDismiss`.
  - The editor emits the colour code for a swatch click, and sends the note on Save.
- **Commit:** `feat(web): add reader selection menu and annotation editor`

---

## Task 9: Wire annotations into `BookFileReader.svelte`

Read `BookFileReader.svelte` in full and ARCHITECTURE_ANALYSIS §11, §14 and §16c first.

**New props** (all optional, so `BookFileReader` stays usable without annotations):
- `annotations?: BookAnnotation[]` (already filtered with `visibleAnnotations`)
- `onCreateAnnotation?(draft)`, `onUpdateAnnotation?(annotation)` and `onDeleteAnnotation?(annotation)`

Here `draft` is `{ locator, spineIndex, position, selectedText, textBefore, textAfter, color, note? }`.

- [ ] **Step 1: Rework the gesture overlay** (`.book-reader-gestures`, `BookFileReader.svelte:417-426,517-522`).
  Today it covers the whole stage in paged mode and swallows the pointer events that start a text selection (§11).
  Change it to **two edge strips**, `previous` and `next`, each about 18% of the width, that keep tap-to-turn. The
  middle is then open to the book content, which gives text selection. Move the other gestures:
  - **Centre tap → toggle controls:** in foliate's `load` handler, add a `click` listener on `doc` that calls
    `shell?.toggleControls()`. Only when the selection is collapsed, the target is not a link, and no annotation
    was hit (foliate emits `show-annotation` first, so set a one-tick flag there).
  - **Swipe to turn:** foliate's paginator already handles touch swipes on the section documents
    (`paginator.js:568-574,823-853`).
  - **Swipe down to close:** keep it on the edge strips only.

  Record this UX change in the commit body. It is also an owner decision (below).
- [ ] **Step 2: Capture selections.**
  - Add `view.addEventListener("load", handleSectionLoad)` next to `relocate` (and remove it in the cleanup).
  - In `handleSectionLoad({ detail: { doc, index } })`, listen for `pointerup` and `keyup` on `doc` (a debounced
    `selectionchange` is an acceptable alternative).
  - When the selection is non-empty, take `range = doc.getSelection().getRangeAt(0)`. Then:
    - locator: `view.getCFI(index, range)`
    - text quote: `textQuoteFor(range)`
    - position: for the start of the range, use `view.getCFI(index, collapsedStartRange)`, then read
      `fraction` via `view.getProgressOf`, or the latest `relocate` fraction. Use the latter if the former is not
      public, and note the choice.
    - anchor rect: `selectionAnchorRect(range.getBoundingClientRect(), doc.defaultView?.frameElement?.getBoundingClientRect() ?? null)`,
      made relative to `stageEl`'s rect.
  - Open `ReaderSelectionMenu` with two actions:
    - **Highlight:** creates the draft with the last-used colour (default yellow, remembered in `localStorage`
      under a namespaced key), then calls `view.deselect()`.
    - **Add Note:** creates the draft and opens `AnnotationEditor` focused on the note.
  - Skip all of this when `isFixedLayout`.
- [ ] **Step 3: Draw highlights.**
  - `import { Overlayer } from "$lib/vendor/foliate-js/overlayer.js"` (dynamic import next to `view.js`).
  - Listen for `draw-annotation` and call `e.detail.draw(Overlayer.highlight, { color: annotationColorCss(e.detail.annotation.color) })`.
  - Listen for `create-overlay` and call `view.addAnnotation({ value: a.locator, color: a.color })` for each
    annotation whose `spineIndex === e.detail.index`.
  - When the `annotations` prop changes, diff by id against what was drawn:
    - removed → `view.deleteAnnotation({ value })`
    - added or changed → `view.addAnnotation(...)`, which removes and redraws the same value.
  - Keep a `Map<locator, id>`, because foliate keys overlays by `value` (the CFI). Two annotations on an identical
    range would collide. On create, if an annotation with the same locator already exists, update that one
    instead of creating a duplicate.
- [ ] **Step 4: Tap a highlight.** Listen for `show-annotation` and look up the annotation by `detail.value`.
  Open `AnnotationEditor` anchored at `detail.range`'s rect (converted as in Step 2). Its callbacks map to
  `onUpdateAnnotation` (colour or note) and `onDeleteAnnotation`.
- [ ] **Step 5: Tests.** No component test exists for `BookFileReader` (§16c), and foliate needs a real browser,
  so the behaviour here is covered by the Playwright test in Task 11. Add a unit test only for any pure function
  you extract, such as the edge-strip zone calculation: extend `comicTapZone` or add a sibling function with a
  test.
- [ ] **Step 6: Run** `pnpm check` and `pnpm test:web-svelte`, then commit:
  `feat(web): highlight and annotate EPUB selections in the reader`

---

## Task 10: Reader route sync (`routes/books/[id]/reader/+page.svelte`)

- [ ] **Step 1:** Hold `let annotationMap = $state(new Map<string, BookAnnotation>())`. Pass
  `annotations={visibleAnnotations(annotationMap.values())}` and the three callbacks to `BookFileReader` in the
  EPUB branch (`+page.svelte:317-330`).
- [ ] **Step 2: Load and refresh.**
  - On mount, once `bookId` is known, run `fetchBookAnnotations(bookId)` and then
    `annotationMap = mergeAll(annotationMap, response.annotations)`.
  - Repeat on `visibilitychange` when the page becomes visible, and on window `focus`, throttled to once per 5
    seconds. The page already has a `visibilitychange` handler for progress (`+page.svelte:88-92`); extend it
    rather than adding a second one.
- [ ] **Step 3: Write.**
  - **Create:** generate the id with `crypto.randomUUID()` and set `updatedAt = new Date().toISOString()`.
    Apply the change locally first (optimistic), then call `saveBookAnnotation`, and merge the server's response.
    If the server returns a newer version, it wins.
  - **Update:** same, with a fresh `updatedAt`.
  - **Delete:** set `deletedAt` locally, call `deleteBookAnnotation(bookId, id, updatedAt)`, and merge the
    response.
  - Serialize writes through a promise queue like `progressSaveQueue` (`+page.svelte:60,208-214`).
  - On a network failure, keep the local change and retry on the next visibility or focus refresh by re-sending
    local entries that are newer than the server's. Keep that in memory only; offline persistence is out of scope.
- [ ] **Step 4: Run** `pnpm check` and `pnpm test:web-svelte`, then commit: `feat(web): sync reader annotations across devices`


---

## Task 10A: Reading position follows you between devices

**Owner request (2026-09-26):** "it should sync my book page progress whenever i switch page."

**What already works:**
- Every EPUB page turn fires `relocate`, which calls `queueEpubSave()` with no debounce
  (`routes/books/[id]/reader/+page.svelte:178-182`).
- Every PDF page change calls `queuePdfSave` (`:248-253`).
- The saves go to `PATCH /api/entities/{id}/progress`, and opening the book on another device resumes there.

**What is missing:** a reader that is **already open** on another device, for example an iPad tab left on the
book, never picks up the newer position. This task adds that catch-up, and reuses the existing progress endpoint
and fields. No backend change and no contract change is needed.

- [ ] **Step 1: Write the failing tests** for `apps/web-svelte/src/lib/reader/progress-catch-up.ts`:

```ts
import { describe, expect, it } from "vitest";
import { remoteProgressToAdopt } from "./progress-catch-up";

const saved = "2026-09-26T10:00:00Z";

describe("remoteProgressToAdopt", () => {
  it("adopts a newer EPUB CFI from another device", () => {
    const remote = { updatedAt: "2026-09-26T10:05:00Z", location: "epubcfi(/6/8!/4/2/1:0)", index: 4200, total: 10000 };
    expect(remoteProgressToAdopt(remote, { lastSavedAt: saved, currentLocation: "epubcfi(/6/4!/4/2/1:0)" }))
      .toEqual({ kind: "cfi", cfi: "epubcfi(/6/8!/4/2/1:0)" });
  });

  it("ignores progress that is not newer than this device's last save", () => {
    const remote = { updatedAt: saved, location: "epubcfi(/6/8!/4/2/1:0)", index: 4200, total: 10000 };
    expect(remoteProgressToAdopt(remote, { lastSavedAt: saved, currentLocation: null })).toBeNull();
  });

  it("ignores the same location", () => {
    const cfi = "epubcfi(/6/8!/4/2/1:0)";
    const remote = { updatedAt: "2026-09-26T10:05:00Z", location: cfi, index: 4200, total: 10000 };
    expect(remoteProgressToAdopt(remote, { lastSavedAt: saved, currentLocation: cfi })).toBeNull();
  });

  it("falls back to a fraction when the remote locator is not a CFI (native app formats)", () => {
    const remote = { updatedAt: "2026-09-26T10:05:00Z", location: "{\"native\":1}", index: 2500, total: 10000 };
    expect(remoteProgressToAdopt(remote, { lastSavedAt: saved, currentLocation: null }))
      .toEqual({ kind: "fraction", fraction: 0.25 });
  });

  it("returns a zero-based page for PDF progress", () => {
    const remote = { updatedAt: "2026-09-26T10:05:00Z", location: null, index: 11, total: 40 };
    expect(remoteProgressToAdopt(remote, { lastSavedAt: saved, currentLocation: null, pdf: true }))
      .toEqual({ kind: "page", page: 11 });
  });
});
```

- [ ] **Step 2: Implement** `remoteProgressToAdopt`.
  - Use `epub-contents.ts`'s existing CFI check (the function behind `epub-contents.ts:66-79`, which accepts
    `epubcfi(` and rejects the native-app `{`/`[`/`#prismedia-progress=` formats). Do not retype that logic.
    Import it, exporting it first if it is private.
  - Return `null` when `updatedAt` is missing, not newer than `lastSavedAt`, or the location is unchanged.
- [ ] **Step 3: Make both readers able to jump.**
  - `BookFileReader.svelte`: add `export function goToLocation(target: { cfi: string } | { fraction: number })`,
    which calls `view.goTo(cfi)` or `view.goToFraction(fraction)`. Check the name in `view.js`; `init` already
    accepts `{ fraction }`, see `BookFileReader.svelte:301-310`.
  - `PdfReader.svelte`: add `export function goToPage(page: number)`, reusing its existing page-navigation code.
  - Bind both instances in `+page.svelte` with `bind:this`.
- [ ] **Step 4: Catch up on focus.** In `+page.svelte`:
  - Record `lastProgressSavedAt = new Date().toISOString()` after every successful `saveEpubProgress` and
    `savePdfProgress`.
  - On `visibilitychange`→visible and window `focus`, throttled to once per 5 s and run in the same handler as
    Task 10's annotation refresh, call `fetchEntity(bookId)` (already imported, `+page.svelte:7,127`).
  - Read the `CAPABILITY_KIND.progress` capability (as in `:170`), run `remoteProgressToAdopt`, and jump if it
    returns a target.
  - Show a small non-blocking notice, "Moved to where you left off on another device", with a **Go back** action.
    Go back returns to the previous location, which then saves as the newest position. Use the app's existing
    toast/notice component (grep `toast` in `apps/web-svelte/src/lib`), not a hand-rolled one.
  - The jump fires `relocate`/page change, which saves as usual. The newest position therefore stays consistent
    on every device.
- [ ] **Step 5: Run** the tests and `pnpm check`, then commit: `feat(web): catch up to reading progress from other devices on focus`

---

## Task 11: End-to-end test (Playwright)

The repository has **no EPUB fixture** and **no reader e2e test** (§16c). The existing `e2e/smoke.spec.ts` does not
authenticate: it predates multi-user auth. Build the minimum needed.

- [ ] **Step 1: Fixture.** Generate a tiny reflowable EPUB of two chapters with a few paragraphs each, and commit
  it at `tests/fixtures/media/books/nitrate-annotation-fixture.epub`. Keep the generator script in the plan
  commit message or in `e2e/fixtures/README.md`; there is no npm zip dependency. The Python standard library is
  enough: write `mimetype` first and uncompressed, then `META-INF/container.xml`, `OEBPS/content.opf`,
  `OEBPS/nav.xhtml` and `OEBPS/ch1.xhtml`/`ch2.xhtml`. Use `EpubBookContentsServiceTests.CreateEpubAsync` as the
  shape reference.
- [ ] **Step 2: Auth helper** (`e2e/auth.ts`).
  - `GET /api/auth/setup-status`. If `needsSetup`, `POST /api/auth/setup` with
    `{ username, password }` from `PRISMEDIA_E2E_USERNAME`/`PRISMEDIA_E2E_PASSWORD` (defaults `e2e-admin` /
    a fixed test password).
  - Then `POST /api/auth/login` with `{ username, password }`.
  - Return the `prismedia-session` cookie, and apply it with `context.addCookies`. The cookie name is
    `SessionCookieName` in `apps/backend/src/Prismedia.Api/Security/PrismediaAuthentication.cs`.
- [ ] **Step 3: Book root helper** (`e2e/book-fixture.ts`). Mirror `ensureFixtureLibraryRoot` in
  `e2e/fixture-library.ts`: create a watched root on `tests/fixtures/media/books` with the Books toggle, trigger a
  scan, and poll `GET /api/books` until the fixture's title appears. Return its id.
- [ ] **Step 4: Spec** (`e2e/reader-annotations.spec.ts`).
  1. Log in. Open `/books/{id}/reader` and switch to **scrolled** flow, which makes selecting simpler; add a
     paged-mode case once scrolled passes.
  2. The foliate section iframe sits inside a **closed** shadow root, so locators cannot pierce it. Use
     `page.frames()`, which lists every frame from the browser frame tree, and pick the frame whose document
     contains the fixture's first paragraph.
  3. In that frame, programmatically select a known phrase (`document.createRange` + `getSelection().addRange`)
     and dispatch `pointerup` on the document.
  4. Click **Highlight** in the menu (the top page). Wait for the `PUT .../annotations/...` response.
  5. Reload. Wait for the `GET .../annotations` response and assert the list has one annotation with the phrase.
     Assert that the overlay is drawn: the frame's parent document contains the foliate overlayer `svg` with a
     `rect`. If the closed shadow DOM blocks that check, fall back to asserting via the API.
  6. **Second browser context, same user:** new context, log in, open the reader, and assert the annotation
     loads. Then, in context 2, change the colour to blue. Bring context 1's page to the front (`page.bringToFront()`
     triggers `visibilitychange`/`focus`) and assert that the colour update is reflected, via the API response.
  7. **Progress follows (Task 10A):** in context 2, turn two pages (the `next` edge strip, or `view.next()` via the
     frame). Wait for its `PATCH .../progress`. Bring context 1 to the front, and assert that context 1 jumps: its
     next `PATCH .../progress` carries context 2's `location`, and the notice is visible.
- [ ] **Step 5: Run** `pnpm test:e2e -- reader-annotations` against a running stack (BASELINE "Running
  locally"; the API on :8008). Record real output. If the sandbox cannot run Chromium, say so; do not claim a
  pass.
- [ ] **Step 6: Commit.** `test(e2e): cover highlight persistence and same-user cross-device sync`

---

## Task 12: Verify, document, hand off

- [ ] **Step 1:** Run `pnpm test:backend`, `pnpm test:web-svelte`, `pnpm test:unit`, `pnpm check` and
  `pnpm api:check`. Compare against `BASELINE.md`, and report real counts.
- [ ] **Step 2:** Add an "Annotations" note to `docs/nitrate/ARCHITECTURE_ANALYSIS.md` §20 saying it is
  implemented, with pointers to the new files. Update `docs/nitrate/ROADMAP.md`: M3 Status becomes
  `Built; awaiting owner reading test`.
- [ ] **Step 3: Owner-verified acceptance** (list these for the owner; do not claim them):
  - [ ] On the Mac browser, highlight a passage in an EPUB in two colours and add a note to one.
  - [ ] Open the same book on the iPad browser. Both highlights and the note are there.
  - [ ] Change a colour on the iPad and return to the Mac tab. The change appears after focus.
  - [ ] Delete a highlight on one device. It disappears on the other after reopening or focusing.
  - [ ] Read a few pages on the Mac, then switch to the iPad tab that already had the book open. It jumps to the
        Mac's page, and **Go back** returns you to where the iPad was. Repeat with a PDF.
  - [ ] Text selection works in both paged and scrolled modes on the iPad (touch long-press) and on the Mac
        (mouse drag). Edge taps still turn pages, and a centre tap still toggles the controls.
  - [ ] The Prismedia iPhone/iPad App Store app still opens books and resumes progress. No existing endpoint
        changed.
- [ ] **Step 4: Commit and push**, then stop for owner review.

---

## Owner decisions (answered 2026-09-26)

1. **Paged-mode gestures: approved.** Edge strips of about 18% turn pages, a centre tap toggles the controls, and
   foliate handles swipes. Taps in the middle no longer turn pages.
2. **Colours: approved.** Yellow, blue, green, red and purple at foliate's default 30% opacity.
3. **Sync freshness: approved, with an addition.**
   - Highlights and notes refresh on open, focus and visibility; there is no live push.
   - The owner also asked for reading progress to sync on every page switch. Saving on every page turn already
     happens. **Task 10A** adds the missing half: an already-open reader catches up to a newer position from
     another device when its tab regains focus, with a "Go back" escape.
4. **Clock trust: accepted.** Last-write-wins uses device clocks. Writes more than a day in the future are rejected.
5. **Changed book files: accepted.** M3 stores the text quote and the file signature. Re-anchoring, and flagging
   stale highlights, are deferred to M4.
