using Microsoft.EntityFrameworkCore;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Tests;

public sealed class BookAnnotationMigrationPostgresTests {
    [Fact]
    public async Task AnnotationsRoundTripAndCascadeWithTheirBook() {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var annotationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var context = database.CreateContext()) {
            context.Users.Add(new UserRow {
                Id = userId,
                Username = $"annotation-{userId:N}",
                NormalizedUsername = $"annotation-{userId:N}",
                DisplayName = "Annotation Tester",
                Role = UserRole.Member,
                AllowNsfw = true,
                Enabled = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            context.Entities.Add(new EntityRow {
                Id = bookId,
                KindCode = EntityKind.Book.ToCode(),
                Title = "Annotated book",
                CreatedAt = now,
                UpdatedAt = now
            });
            context.BookAnnotations.Add(new BookAnnotationRow {
                Id = annotationId,
                UserId = userId,
                BookId = bookId,
                LocatorKind = AnnotationLocatorKind.EpubCfi,
                Locator = "epubcfi(/6/4!/4/2,/1:0,/1:5)",
                SpineIndex = 1,
                Position = 0.1,
                SelectedText = "Hello",
                Style = AnnotationStyle.Highlight,
                Color = AnnotationColor.Purple,
                Note = "note",
                CreatedAt = now,
                UpdatedAt = now
            });
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext()) {
            var stored = await context.BookAnnotations.AsNoTracking().SingleAsync(row => row.Id == annotationId);
            Assert.Equal(AnnotationColor.Purple, stored.Color);
            Assert.Equal(AnnotationStyle.Highlight, stored.Style);
            Assert.Equal(AnnotationLocatorKind.EpubCfi, stored.LocatorKind);
            Assert.Equal("note", stored.Note);
            Assert.Null(stored.DeletedAt);
        }

        await using (var connection = await database.OpenConnectionAsync()) {
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT color FROM book_annotations";
            Assert.Equal("purple", await check.ExecuteScalarAsync());

            await using var delete = connection.CreateCommand();
            delete.CommandText = $"DELETE FROM entities WHERE id = '{bookId}'";
            await delete.ExecuteNonQueryAsync();

            await using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM book_annotations";
            Assert.Equal(0L, await count.ExecuteScalarAsync());
        }
    }
}
