using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Prismedia.Contracts.Books;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Books;

/// <summary>
/// Renders highlights and notes as Markdown, JSON, or CSV. Pure: callers pass items already ordered by book and
/// reading position; books appear in first-seen order.
/// </summary>
public static class AnnotationExportWriter {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Writes an export file.</summary>
    /// <param name="format">Target format.</param>
    /// <param name="items">Highlights, ordered by book then reading position.</param>
    /// <param name="exportedAt">Export timestamp (also used in the file name).</param>
    /// <param name="scopeName">Optional scope (e.g. the book title) for the file name.</param>
    /// <returns>The file bytes, MIME type, and suggested file name.</returns>
    public static AnnotationExportFile Write(
        AnnotationExportFormat format,
        IReadOnlyList<AnnotationLibraryItem> items,
        DateTimeOffset exportedAt,
        string? scopeName = null) {
        var books = items
            .GroupBy(item => item.BookId)
            .Select(group => (Title: group.First().BookTitle, Items: group.ToList()))
            .ToList();
        var baseName = $"nitrate-annotations{(scopeName is null ? "" : "-" + Slug(scopeName))}-{exportedAt:yyyyMMdd}";

        return format switch {
            AnnotationExportFormat.Markdown => new AnnotationExportFile(
                Encoding.UTF8.GetBytes(Markdown(books, exportedAt)),
                "text/markdown; charset=utf-8",
                baseName + ".md"),
            AnnotationExportFormat.Json => new AnnotationExportFile(
                JsonSerializer.SerializeToUtf8Bytes(Json(books, exportedAt), JsonOptions),
                "application/json",
                baseName + ".json"),
            AnnotationExportFormat.Csv => new AnnotationExportFile(
                [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Csv(books))],
                "text/csv; charset=utf-8",
                baseName + ".csv"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static string Markdown(
        IReadOnlyList<(string Title, List<AnnotationLibraryItem> Items)> books,
        DateTimeOffset exportedAt) {
        var text = new StringBuilder();
        text.Append("# Annotations\n\n");
        text.Append(CultureInfo.InvariantCulture, $"_Exported from Nitrate on {exportedAt:yyyy-MM-dd}._\n");
        foreach (var (title, items) in books) {
            text.Append(CultureInfo.InvariantCulture, $"\n## {title}\n");
            foreach (var item in items) {
                text.Append('\n');
                foreach (var line in item.SelectedText.Split('\n')) {
                    text.Append("> ").Append(line.TrimEnd('\r')).Append('\n');
                }

                if (!string.IsNullOrWhiteSpace(item.Note)) {
                    text.Append("\n**Note:** ").Append(item.Note.Trim()).Append('\n');
                }

                text.Append(CultureInfo.InvariantCulture, $"\n_{item.Color} · {item.CreatedAt:yyyy-MM-dd}_\n");
            }
        }

        return text.ToString();
    }

    private static object Json(
        IReadOnlyList<(string Title, List<AnnotationLibraryItem> Items)> books,
        DateTimeOffset exportedAt) => new {
        exportedAt,
        books = books.Select(book => new {
            bookId = book.Items[0].BookId,
            title = book.Title,
            annotations = book.Items.Select(item => new {
                id = item.Id,
                text = item.SelectedText,
                note = item.Note,
                color = item.Color,
                style = item.Style,
                locatorKind = item.LocatorKind,
                locator = item.Locator,
                position = item.Position,
                createdAt = item.CreatedAt,
                updatedAt = item.UpdatedAt
            })
        })
    };

    private static string Csv(IReadOnlyList<(string Title, List<AnnotationLibraryItem> Items)> books) {
        var text = new StringBuilder("book,highlight,note,color,position,created_at,updated_at,locator\r\n");
        foreach (var (title, items) in books) {
            foreach (var item in items) {
                text.AppendJoin(',', [
                    Field(title),
                    Field(item.SelectedText),
                    Field(item.Note ?? ""),
                    Field(item.Color),
                    Field(item.Position.ToString("0.####", CultureInfo.InvariantCulture)),
                    Field(item.CreatedAt.ToString("O", CultureInfo.InvariantCulture)),
                    Field(item.UpdatedAt.ToString("O", CultureInfo.InvariantCulture)),
                    Field(item.Locator)
                ]).Append("\r\n");
            }
        }

        return text.ToString();
    }

    /// <summary>RFC 4180 field: quoted when it contains a comma, quote, or line break; quotes doubled.</summary>
    private static string Field(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static string Slug(string value) {
        var slug = new string(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal)) {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        return slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
    }
}
