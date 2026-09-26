using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Prismedia.Infrastructure.Persistence;
using Xunit.Sdk;

namespace Prismedia.Infrastructure.Tests;

/// <summary>PostgreSQL upgrade and rollback coverage for managed subtitle source identity.</summary>
public sealed class SubtitleSidecarLifecycleMigrationPostgresTests {
    private const string PreviousMigration = "20260710092229_AddEntityLifecycleClaimsAndNormalizeMonitors";
    private const string MigrationUnderTest = "20260710150629_AddSubtitleSidecarLifecycle";
    private const string LegacyIndex = "IX_entity_subtitles_entity_id_language_source";
    private const string SourceKeyIndex = "IX_entity_subtitles_entity_id_source_source_key";

    [Fact]
    [Trait("Category", "PostgreSQL")]
    public async Task UpgradeAndRollbackSwitchSubtitleIdentityWithoutLosingTracks() {
        await using var database = await PostgresTestDatabase.CreateAsync(PreviousMigration);
        var fixture = MigrationFixture.Create();
        await SeedLegacyRowsAsync(database, fixture);

        await database.MigrateAsync(MigrationUnderTest);

        await using (var connection = await database.OpenConnectionAsync()) {
            var upgraded = await ReadUpgradedSubtitlesAsync(connection);
            Assert.Equal(3, upgraded.Count);
            Assert.Equal("stream:7", upgraded[fixture.FirstEmbeddedId].SourceKey);
            Assert.Equal("stream:8", upgraded[fixture.SecondEmbeddedId].SourceKey);
            Assert.Equal("eng", upgraded[fixture.FirstEmbeddedId].Language);
            Assert.Equal("eng", upgraded[fixture.SecondEmbeddedId].Language);

            var manual = upgraded[fixture.ManualId];
            Assert.Equal("manual", manual.Source);
            Assert.Equal("eng", manual.Language);
            Assert.Equal($"legacy:{fixture.ManualId:N}", manual.SourceKey);
            Assert.Equal("Manual English", manual.Label);
            Assert.Equal("/cache/manual.vtt", manual.StoragePath);

            Assert.Null(await ScalarAsync<DateTimeOffset?>(
                connection,
                "SELECT subtitles_extracted_at FROM video_details WHERE entity_id = @entity_id",
                ("entity_id", fixture.VideoId)));
            Assert.Null(await ScalarAsync<string?>(
                connection,
                "SELECT subtitle_sidecar_signature FROM video_details WHERE entity_id = @entity_id",
                ("entity_id", fixture.VideoId)));

            Assert.Equal(
                ["scan-gallery"],
                await ReadStringsAsync(connection, "SELECT scan_kind FROM scanned_files ORDER BY scan_kind"));
            Assert.Null(await IndexDefinitionAsync(connection, LegacyIndex));
            var sourceKeyIndex = await IndexDefinitionAsync(connection, SourceKeyIndex);
            Assert.NotNull(sourceKeyIndex);
            Assert.Contains("UNIQUE", sourceKeyIndex, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("(entity_id, source, source_key)", sourceKeyIndex, StringComparison.Ordinal);

            await InsertSameLanguageSidecarsAsync(connection, fixture);
            Assert.Equal(
                2,
                await ScalarAsync<int>(
                    connection,
                    """
                    SELECT count(*)::int
                    FROM entity_subtitles
                    WHERE entity_id = @entity_id AND source = 'sidecar' AND language = 'eng'
                    """,
                    ("entity_id", fixture.VideoId)));
        }

        await database.MigrateAsync(PreviousMigration);

        await using (var connection = await database.OpenConnectionAsync()) {
            var rolledBack = await ReadLegacySubtitlesAsync(connection);
            Assert.Equal(5, rolledBack.Count);
            Assert.Equal("eng", rolledBack[fixture.FirstEmbeddedId].Language);
            Assert.Equal(
                LegacyDuplicateLanguage("eng", fixture.SecondEmbeddedId),
                rolledBack[fixture.SecondEmbeddedId].Language);
            Assert.Equal("eng", rolledBack[fixture.FirstSidecarId].Language);
            Assert.Equal(
                LegacyDuplicateLanguage("eng", fixture.SecondSidecarId),
                rolledBack[fixture.SecondSidecarId].Language);

            var manual = rolledBack[fixture.ManualId];
            Assert.Equal("manual", manual.Source);
            Assert.Equal("eng", manual.Language);
            Assert.Equal("Manual English", manual.Label);
            Assert.Equal("/cache/manual.vtt", manual.StoragePath);

            Assert.False(await ColumnExistsAsync(connection, "entity_subtitles", "source_key"));
            Assert.False(await ColumnExistsAsync(connection, "video_details", "subtitle_sidecar_signature"));
            Assert.Null(await IndexDefinitionAsync(connection, SourceKeyIndex));
            var legacyIndex = await IndexDefinitionAsync(connection, LegacyIndex);
            Assert.NotNull(legacyIndex);
            Assert.Contains("UNIQUE", legacyIndex, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("(entity_id, language, source)", legacyIndex, StringComparison.Ordinal);
        }
    }

    private static async Task SeedLegacyRowsAsync(
        PostgresTestDatabase database,
        MigrationFixture fixture) {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO library_roots
                (id, path, label, enabled, recursive, scan_videos, scan_images, scan_audio,
                 scan_books, is_nsfw, created_at, updated_at)
            VALUES
                (@root_id, '/media/videos', 'Videos', TRUE, TRUE, TRUE, FALSE, FALSE,
                 FALSE, FALSE, @created_at, @created_at);

            INSERT INTO entities
                (id, kind_code, title, is_nsfw, is_organized, is_wanted, created_at, updated_at)
            VALUES
                (@video_id, 'video', 'Movie', FALSE, FALSE, FALSE, @created_at, @created_at);

            INSERT INTO video_details (entity_id, library_root_id, subtitles_extracted_at)
            VALUES (@video_id, @root_id, @created_at);

            INSERT INTO entity_subtitles
                (id, entity_id, language, label, format, source, storage_path, source_format,
                 source_path, is_default, created_at)
            VALUES
                (@first_embedded_id, @video_id, 'eng', 'Embedded 7', 'vtt', 'embedded',
                 '/cache/embedded-7.vtt', 'subrip', '7', FALSE, @created_at),
                (@second_embedded_id, @video_id, 'eng.2', 'Embedded 8', 'vtt', 'embedded',
                 '/cache/embedded-8.vtt', 'subrip', '8', FALSE, @created_at),
                (@manual_id, @video_id, 'eng', 'Manual English', 'vtt', 'manual',
                 '/cache/manual.vtt', 'vtt', NULL, TRUE, @created_at);

            INSERT INTO scanned_files
                (library_root_id, scan_kind, path, size_bytes, modified_ticks, updated_at)
            VALUES
                (@root_id, 'scan-library', '/media/videos/Movie.mkv', 100, 1, @created_at),
                (@root_id, 'scan-gallery', '/media/videos/poster.jpg', 50, 2, @created_at);
            """,
            connection);
        command.Parameters.AddWithValue("root_id", fixture.RootId);
        command.Parameters.AddWithValue("video_id", fixture.VideoId);
        command.Parameters.AddWithValue("first_embedded_id", fixture.FirstEmbeddedId);
        command.Parameters.AddWithValue("second_embedded_id", fixture.SecondEmbeddedId);
        command.Parameters.AddWithValue("manual_id", fixture.ManualId);
        command.Parameters.AddWithValue("created_at", fixture.CreatedAt);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertSameLanguageSidecarsAsync(
        NpgsqlConnection connection,
        MigrationFixture fixture) {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO entity_subtitles
                (id, entity_id, language, label, format, source, source_key, storage_path,
                 source_format, source_path, is_default, created_at)
            VALUES
                (@first_id, @video_id, 'eng', 'Sidecar one', 'vtt', 'sidecar', @first_key,
                 '/cache/sidecar-one.vtt', 'srt', NULL, FALSE, @created_at),
                (@second_id, @video_id, 'eng', 'Sidecar two', 'vtt', 'sidecar', @second_key,
                 '/cache/sidecar-two.vtt', 'srt', NULL, FALSE, @created_at);
            """,
            connection);
        command.Parameters.AddWithValue("first_id", fixture.FirstSidecarId);
        command.Parameters.AddWithValue("second_id", fixture.SecondSidecarId);
        command.Parameters.AddWithValue("video_id", fixture.VideoId);
        command.Parameters.AddWithValue("first_key", new string('a', 64));
        command.Parameters.AddWithValue("second_key", new string('b', 64));
        command.Parameters.AddWithValue("created_at", fixture.CreatedAt.AddMinutes(1));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyDictionary<Guid, UpgradedSubtitleSnapshot>> ReadUpgradedSubtitlesAsync(
        NpgsqlConnection connection) {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, language, label, source, source_key, storage_path
            FROM entity_subtitles
            ORDER BY id
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new Dictionary<Guid, UpgradedSubtitleSnapshot>();
        while (await reader.ReadAsync()) {
            var id = reader.GetGuid(0);
            rows.Add(id, new UpgradedSubtitleSnapshot(
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return rows;
    }

    private static async Task<IReadOnlyDictionary<Guid, LegacySubtitleSnapshot>> ReadLegacySubtitlesAsync(
        NpgsqlConnection connection) {
        await using var command = new NpgsqlCommand(
            """
            SELECT id, language, label, source, storage_path
            FROM entity_subtitles
            ORDER BY id
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new Dictionary<Guid, LegacySubtitleSnapshot>();
        while (await reader.ReadAsync()) {
            var id = reader.GetGuid(0);
            rows.Add(id, new LegacySubtitleSnapshot(
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<string>> ReadStringsAsync(
        NpgsqlConnection connection,
        string sql) {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync()) {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static async Task<T?> ScalarAsync<T>(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters) {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) {
            command.Parameters.AddWithValue(name, value);
        }

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? default : (T)scalar;
    }

    private static Task<string?> IndexDefinitionAsync(NpgsqlConnection connection, string indexName) =>
        ScalarAsync<string>(
            connection,
            """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'public' AND indexname = @index_name
            """,
            ("index_name", indexName));

    private static async Task<bool> ColumnExistsAsync(
        NpgsqlConnection connection,
        string tableName,
        string columnName) =>
        await ScalarAsync<bool>(
            connection,
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = @table_name AND column_name = @column_name)
            """,
            ("table_name", tableName),
            ("column_name", columnName));

    private static string LegacyDuplicateLanguage(string language, Guid id) {
        var hash = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(id.ToString())));
        return $"{language[..Math.Min(language.Length, 16)]}.legacy-{hash[..8]}";
    }

    private sealed record UpgradedSubtitleSnapshot(
        string Language,
        string? Label,
        string Source,
        string SourceKey,
        string StoragePath);

    private sealed record LegacySubtitleSnapshot(
        string Language,
        string? Label,
        string Source,
        string StoragePath);

    private sealed record MigrationFixture(
        Guid RootId,
        Guid VideoId,
        Guid FirstEmbeddedId,
        Guid SecondEmbeddedId,
        Guid ManualId,
        Guid FirstSidecarId,
        Guid SecondSidecarId,
        DateTimeOffset CreatedAt) {
        public static MigrationFixture Create() => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero));
    }

    private sealed class PostgresTestDatabase(
        string databaseName,
        string adminConnectionString,
        string connectionString) : IAsyncDisposable {
        public async Task<NpgsqlConnection> OpenConnectionAsync() {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task MigrateAsync(string targetMigration) {
            await using var context = new PrismediaDbContext(
                new DbContextOptionsBuilder<PrismediaDbContext>()
                    .UseNpgsql(connectionString)
                    .Options);
            await context.GetService<IMigrator>().MigrateAsync(targetMigration);
        }

        public static async Task<PostgresTestDatabase> CreateAsync(string targetMigration) {
            var configured = Environment.GetEnvironmentVariable("PRISMEDIA_TEST_DATABASE_URL")
                ?? "Host=localhost;Port=5432;Database=postgres;Username=prismedia;Password=prismedia";
            var adminBuilder = new NpgsqlConnectionStringBuilder(configured) {
                Database = "postgres",
                Pooling = false
            };
            try {
                await using var probe = new NpgsqlConnection(adminBuilder.ConnectionString);
                await probe.OpenAsync();
            } catch (Exception exception) when (exception is NpgsqlException or TimeoutException) {
                throw SkipException.ForSkip(
                    $"PostgreSQL subtitle migration test requires PRISMEDIA_TEST_DATABASE_URL or the local dev database: {exception.Message}");
            }

            var name = $"prismedia_subtitle_migration_{Guid.NewGuid():N}";
            await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString)) {
                await admin.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
                await create.ExecuteNonQueryAsync();
            }

            var testBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString) {
                Database = name,
                Pooling = false
            };
            var database = new PostgresTestDatabase(
                name,
                adminBuilder.ConnectionString,
                testBuilder.ConnectionString);
            try {
                await database.MigrateAsync(targetMigration);
                return database;
            } catch {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync() {
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(adminConnectionString);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
                admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
