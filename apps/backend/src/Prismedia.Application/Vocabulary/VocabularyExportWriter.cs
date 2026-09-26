using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Prismedia.Contracts.Vocabulary;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Vocabulary;

/// <summary>Renders saved vocabulary as Markdown, JSON, or CSV. Pure: callers pass already-filtered items.</summary>
public static class VocabularyExportWriter {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Writes an export file.</summary>
    /// <param name="format">Target format.</param>
    /// <param name="items">Vocabulary entries to export.</param>
    /// <param name="exportedAt">Export timestamp (also used in the file name).</param>
    public static VocabularyExportFile Write(
        AnnotationExportFormat format,
        IReadOnlyList<VocabularyEntry> items,
        DateTimeOffset exportedAt) {
        var baseName = $"nitrate-vocabulary-{exportedAt:yyyyMMdd}";
        return format switch {
            AnnotationExportFormat.Markdown => new VocabularyExportFile(
                Encoding.UTF8.GetBytes(Markdown(items, exportedAt)),
                "text/markdown; charset=utf-8",
                baseName + ".md"),
            AnnotationExportFormat.Json => new VocabularyExportFile(
                JsonSerializer.SerializeToUtf8Bytes(Json(items, exportedAt), JsonOptions),
                "application/json",
                baseName + ".json"),
            AnnotationExportFormat.Csv => new VocabularyExportFile(
                [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Csv(items))],
                "text/csv; charset=utf-8",
                baseName + ".csv"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static string Markdown(IReadOnlyList<VocabularyEntry> items, DateTimeOffset exportedAt) {
        var text = new StringBuilder();
        text.Append("# Vocabulary\n\n");
        text.Append(CultureInfo.InvariantCulture, $"_Exported from Nitrate on {exportedAt:yyyy-MM-dd}._\n");
        foreach (var item in items) {
            text.Append(CultureInfo.InvariantCulture, $"\n## {item.Word} ({item.Language})\n");
            if (!string.IsNullOrWhiteSpace(item.Definition)) {
                text.Append(item.Definition.Trim()).Append('\n');
            }

            if (!string.IsNullOrWhiteSpace(item.SourceSentence)) {
                text.Append("\n> ").Append(item.SourceSentence.Trim()).Append('\n');
            }

            text.Append(CultureInfo.InvariantCulture, $"\n_{item.Status} · {item.CreatedAt:yyyy-MM-dd}_\n");
        }

        return text.ToString();
    }

    private static object Json(IReadOnlyList<VocabularyEntry> items, DateTimeOffset exportedAt) => new {
        exportedAt,
        words = items.Select(item => new {
            id = item.Id,
            word = item.Word,
            language = item.Language,
            definition = item.Definition,
            bookId = item.BookId,
            bookTitle = item.BookTitle,
            sourceSentence = item.SourceSentence,
            status = item.Status,
            createdAt = item.CreatedAt,
            updatedAt = item.UpdatedAt
        })
    };

    private static string Csv(IReadOnlyList<VocabularyEntry> items) {
        var text = new StringBuilder("word,language,definition,status,book,context,created_at,updated_at\r\n");
        foreach (var item in items) {
            text.AppendJoin(',', [
                Field(item.Word),
                Field(item.Language),
                Field(item.Definition ?? ""),
                Field(item.Status),
                Field(item.BookTitle ?? ""),
                Field(item.SourceSentence ?? ""),
                Field(item.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
                Field(item.UpdatedAt.ToString("O", CultureInfo.InvariantCulture))
            ]).Append("\r\n");
        }

        return text.ToString();
    }

    /// <summary>RFC 4180 field: quoted when it contains a comma, quote, or line break; quotes doubled.</summary>
    private static string Field(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
