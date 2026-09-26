import { fireEvent, render, screen } from "@testing-library/svelte";
import { describe, expect, it, vi } from "vitest";
import type { BookBookmark } from "$lib/api/generated/model";
import { ANNOTATION_LOCATOR_KIND } from "$lib/api/generated/codes";
import ReaderBookmarkList from "./ReaderBookmarkList.svelte";

const bookmark: BookBookmark = {
  id: "m1", bookId: "b1", locatorKind: ANNOTATION_LOCATOR_KIND.epubCfi, locator: "epubcfi(/6/4!/4/2/1:0)",
  spineIndex: 1, position: 0.42, chapterLabel: "The Harbour", label: null,
  createdAt: "2026-09-26T10:00:00Z", updatedAt: "2026-09-26T10:00:00Z", deletedAt: null,
};

describe("ReaderBookmarkList", () => {
  it("opens, renames, and deletes bookmarks", async () => {
    const props = { bookmarks: [bookmark], onOpen: vi.fn(), onRename: vi.fn(), onDelete: vi.fn() };
    render(ReaderBookmarkList, { props });

    await fireEvent.click(screen.getByRole("button", { name: /The Harbour/ }));
    expect(props.onOpen).toHaveBeenCalledWith(bookmark);
    expect(screen.getByText("42%")).toBeInTheDocument();

    await fireEvent.click(screen.getByRole("button", { name: "Rename bookmark" }));
    await fireEvent.input(screen.getByRole("textbox", { name: "Bookmark label" }), { target: { value: "Mara's letter" } });
    await fireEvent.click(screen.getByRole("button", { name: "Save label" }));
    expect(props.onRename).toHaveBeenCalledWith(bookmark, "Mara's letter");

    await fireEvent.click(screen.getByRole("button", { name: "Delete bookmark" }));
    expect(props.onDelete).toHaveBeenCalledWith(bookmark);
  });

  it("explains how to add one when empty", () => {
    render(ReaderBookmarkList, { props: { bookmarks: [], onOpen: vi.fn(), onRename: vi.fn(), onDelete: vi.fn() } });
    expect(screen.getByText(/No bookmarks yet/)).toBeInTheDocument();
  });
});
