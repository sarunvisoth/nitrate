using System.Text;
using System.Text.Json;
using Prismedia.Application.Books;
using Prismedia.Contracts.Books;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Tests;

public sealed class AnnotationExportWriterTests {
    private static readonly DateTimeOffset At = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid BookId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly IReadOnlyList<AnnotationLibraryItem> Items = [
        Item("Diffusion models gradually remove noise", "Compare with \"rectified flow\", later.", AnnotationColor.Yellow),
        Item("Second, plain highlight", null, AnnotationColor.Blue),
    ];

    [Fact]
    public void MarkdownGroupsByBookWithQuotesAndNotes() {
        var text = Encoding.UTF8.GetString(AnnotationExportWriter.Write(AnnotationExportFormat.Markdown, Items, At).Content);

        Assert.Contains("## Deep Learning", text);
        Assert.Contains("> Diffusion models gradually remove noise", text);
        Assert.Contains("**Note:** Compare with \"rectified flow\", later.", text);
        Assert.Contains("> Second, plain highlight", text);
        Assert.True(text.IndexOf("Diffusion", StringComparison.Ordinal) < text.IndexOf("Second", StringComparison.Ordinal));
    }

    [Fact]
    public void CsvQuotesFieldsAndStartsWithABom() {
        var file = AnnotationExportWriter.Write(AnnotationExportFormat.Csv, Items, At);
        var text = Encoding.UTF8.GetString(file.Content);

        Assert.Equal([0xEF, 0xBB, 0xBF], file.Content.Take(3));
        var lines = text.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("book,highlight,note,color,position,created_at,updated_at,locator", lines[0]);
        Assert.Contains("\"Compare with \"\"rectified flow\"\", later.\"", lines[1]);
        Assert.Equal(3, lines.Length);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.EndsWith(".csv", file.FileName);
    }

    [Fact]
    public void JsonHasAStableCamelCaseShape() {
        var file = AnnotationExportWriter.Write(AnnotationExportFormat.Json, Items, At);
        using var doc = JsonDocument.Parse(file.Content);

        var book = doc.RootElement.GetProperty("books")[0];
        Assert.Equal("Deep Learning", book.GetProperty("title").GetString());
        var first = book.GetProperty("annotations")[0];
        Assert.Equal("Diffusion models gradually remove noise", first.GetProperty("text").GetString());
        Assert.Equal(AnnotationColor.Yellow.ToCode(), first.GetProperty("color").GetString());
        Assert.Equal(JsonValueKind.Null, book.GetProperty("annotations")[1].GetProperty("note").ValueKind);
        Assert.Equal("application/json", file.ContentType);
    }

    [Fact]
    public void EmptyExportsAreStillValidFiles() {
        using var doc = JsonDocument.Parse(AnnotationExportWriter.Write(AnnotationExportFormat.Json, [], At).Content);
        Assert.Equal(0, doc.RootElement.GetProperty("books").GetArrayLength());
    }

    private static AnnotationLibraryItem Item(string text, string? note, AnnotationColor color) => new(
        Guid.NewGuid(), BookId, "Deep Learning", AnnotationLocatorKind.EpubCfi.ToCode(), "epubcfi(/6/4!/4/2,/1:0,/1:5)",
        1, 0.25, text, note, AnnotationStyle.Highlight.ToCode(), color.ToCode(), false, At, At);
}
