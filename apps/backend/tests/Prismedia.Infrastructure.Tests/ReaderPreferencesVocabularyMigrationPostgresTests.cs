using Microsoft.EntityFrameworkCore;
using Npgsql;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Tests;

public sealed class ReaderPreferencesVocabularyMigrationPostgresTests {
    [Fact]
    public async Task ReaderPreferencesRoundTripAndCascadeWithTheirUser() {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var context = database.CreateContext()) {
            context.Users.Add(NewUser(userId, now));
            context.UserReaderPreferences.Add(new UserReaderPreferencesRow {
                UserId = userId,
                FontFamily = ReaderFontFamily.Serif,
                FontSizePercent = 130,
                LineHeight = 1.7,
                MarginStep = 2,
                MaxContentWidth = 800,
                Theme = ReaderTheme.Sepia,
                Flow = ReaderMode.Scrolled,
                TranslationTargetLanguage = "fr",
                UpdatedAt = now
            });
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext()) {
            var stored = await context.UserReaderPreferences.AsNoTracking().SingleAsync(row => row.UserId == userId);
            Assert.Equal(ReaderTheme.Sepia, stored.Theme);
            Assert.Equal(ReaderMode.Scrolled, stored.Flow);
            Assert.Equal("fr", stored.TranslationTargetLanguage);
        }

        await using (var connection = await database.OpenConnectionAsync()) {
            await DeleteUserAsync(connection, userId);
            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM user_reader_preferences"));
        }
    }

    [Fact]
    public async Task VocabularyEntriesRoundTripCascadeAndEnforceCaseInsensitiveUniqueness() {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var context = database.CreateContext()) {
            context.Users.Add(NewUser(userId, now));
            context.Entities.Add(new EntityRow {
                Id = bookId, KindCode = EntityKind.Book.ToCode(), Title = "Vocabulary book", CreatedAt = now, UpdatedAt = now
            });
            context.VocabularyEntries.Add(new VocabularyEntryRow {
                Id = Guid.NewGuid(),
                UserId = userId,
                Word = "resile",
                Language = "en",
                BookId = bookId,
                Status = VocabularyStatus.New,
                CreatedAt = now,
                UpdatedAt = now
            });
            await context.SaveChangesAsync();
        }

        await using (var connection = await database.OpenConnectionAsync()) {
            await using var duplicate = connection.CreateCommand();
            duplicate.CommandText =
                $"""
                INSERT INTO vocabulary_entries (id, user_id, word, language, status, created_at, updated_at)
                VALUES ('{Guid.NewGuid()}', '{userId}', 'RESILE', 'en', 'new', now(), now())
                """;
            await Assert.ThrowsAsync<PostgresException>(() => duplicate.ExecuteNonQueryAsync());
        }

        await using (var connection = await database.OpenConnectionAsync()) {
            await DeleteEntityAsync(connection, bookId);
            Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM vocabulary_entries"));
            Assert.Equal(DBNull.Value, await ScalarAsync(connection, "SELECT book_id FROM vocabulary_entries"));

            await DeleteUserAsync(connection, userId);
            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM vocabulary_entries"));
        }
    }

    private static UserRow NewUser(Guid userId, DateTimeOffset now) => new() {
        Id = userId,
        Username = $"reader-{userId:N}",
        NormalizedUsername = $"reader-{userId:N}",
        DisplayName = "Reader Tester",
        Role = UserRole.Member,
        AllowNsfw = true,
        Enabled = true,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static async Task DeleteUserAsync(NpgsqlConnection connection, Guid userId) {
        await using var delete = connection.CreateCommand();
        delete.CommandText = $"DELETE FROM users WHERE id = '{userId}'";
        await delete.ExecuteNonQueryAsync();
    }

    private static async Task DeleteEntityAsync(NpgsqlConnection connection, Guid entityId) {
        await using var delete = connection.CreateCommand();
        delete.CommandText = $"DELETE FROM entities WHERE id = '{entityId}'";
        await delete.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql) {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
