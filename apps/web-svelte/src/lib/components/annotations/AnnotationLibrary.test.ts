import { fireEvent, render, screen, waitFor } from "@testing-library/svelte";
import { describe, expect, it, vi } from "vitest";
import type { AnnotationLibraryItem } from "$lib/api/generated/model";
import { ANNOTATION_COLOR, ANNOTATION_LOCATOR_KIND, ANNOTATION_STYLE } from "$lib/api/generated/codes";
import AnnotationLibrary, { type AnnotationLibraryLoaders } from "./AnnotationLibrary.svelte";

function item(id: string, bookId: string, bookTitle: string, text: string, note: string | null, isStale = false): AnnotationLibraryItem {
  return {
    id, bookId, bookTitle, locatorKind: ANNOTATION_LOCATOR_KIND.epubCfi, locator: `epubcfi(/6/4!/4/2,/1:0,/1:${id.length})`,
    spineIndex: 1, position: 0.42, selectedText: text, note, style: ANNOTATION_STYLE.highlight,
    color: ANNOTATION_COLOR.yellow, isStale, createdAt: "2026-09-26T10:00:00Z", updatedAt: "2026-09-26T10:00:00Z",
  };
}

const ITEMS = [
  item("a1", "dune", "Dune", "Fear is the mind-killer.", "Litany"),
  item("a2", "dune", "Dune", "The spice must flow.", null, true),
  item("a3", "dl", "Deep Learning", "Diffusion models gradually remove noise", "Compare with rectified flow"),
];

function loaders(items = ITEMS): AnnotationLibraryLoaders & { list: ReturnType<typeof vi.fn> } {
  return {
    list: vi.fn(async () => ({ items, nextCursor: null, totalCount: items.length })),
    books: vi.fn(async () => ({
      books: [
        { bookId: "dl", title: "Deep Learning", highlightCount: 1, noteCount: 1, staleCount: 0, lastAnnotatedAt: "2026-09-26T10:00:00Z" },
        { bookId: "dune", title: "Dune", highlightCount: 2, noteCount: 1, staleCount: 1, lastAnnotatedAt: "2026-09-26T10:00:00Z" },
      ],
    })),
  };
}

describe("AnnotationLibrary", () => {
  it("groups highlights by book with counts, notes, stale badges, and reader links", async () => {
    render(AnnotationLibrary, { props: { loaders: loaders() } });

    const dune = await screen.findByRole("region", { name: "Dune" });
    expect(dune).toHaveTextContent("2 highlights · 1 notes");
    expect(dune).toHaveTextContent("Fear is the mind-killer.");
    expect(dune).toHaveTextContent("Litany");
    expect(dune).toHaveTextContent("File changed");
    expect(screen.getByRole("region", { name: "Deep Learning" })).toHaveTextContent("Compare with rectified flow");
    const link = screen.getByRole("link", { name: /Fear is the mind-killer/ });
    expect(link.getAttribute("href")).toContain("/books/dune/reader");
    expect(link.getAttribute("href")).toContain("location=");
    expect(screen.getByText(/3 highlights and 2 notes across 2 books/)).toBeInTheDocument();
  });

  it("reloads with the notes-only filter", async () => {
    const api = loaders();
    render(AnnotationLibrary, { props: { loaders: api } });
    await screen.findByRole("region", { name: "Dune" });

    await fireEvent.click(screen.getByRole("switch", { name: "Only highlights with notes" }));

    await waitFor(() => expect(api.list).toHaveBeenLastCalledWith(expect.objectContaining({ notesOnly: true })));
  });

  it("shows an empty state", async () => {
    render(AnnotationLibrary, { props: { loaders: loaders([]) } });

    expect(await screen.findByText("No highlights yet")).toBeInTheDocument();
  });

  it("offers Markdown, JSON and CSV downloads", async () => {
    render(AnnotationLibrary, { props: { loaders: loaders() } });

    expect(screen.getByRole("link", { name: "CSV" }).getAttribute("href")).toBe("/api/annotations/export?format=csv");
    expect(screen.getByRole("link", { name: "Markdown" })).toHaveAttribute("download");
  });
});
