import { fireEvent, render, screen, waitFor } from "@testing-library/svelte";
import { describe, expect, it, vi } from "vitest";
import ReaderSelectionMenu from "./ReaderSelectionMenu.svelte";

vi.mock("$lib/api/reader-lookups", () => ({
  defineTerm: vi.fn().mockResolvedValue({ status: "not-found" }),
  translateText: vi.fn().mockResolvedValue({ status: "not-found" }),
  wikipediaSummary: vi.fn().mockResolvedValue({ status: "not-found" }),
}));

const anchor = { left: 100, top: 200, width: 40, height: 16 };

describe("ReaderSelectionMenu", () => {
  it("renders the given actions plus Wikipedia and Search web for a long selection", async () => {
    const highlight = vi.fn();
    const addNote = vi.fn();
    render(ReaderSelectionMenu, { props: {
      anchor,
      selectedText: "a sentence far too long to define as a single word",
      actions: [
        { id: "highlight", label: "Highlight", run: highlight },
        { id: "note", label: "Add Note", run: addNote },
      ],
      onDismiss: vi.fn(),
    } });

    await fireEvent.click(await screen.findByRole("button", { name: "Highlight" }));
    await fireEvent.click(screen.getByRole("button", { name: "Add Note" }));

    expect(highlight).toHaveBeenCalledTimes(1);
    expect(addNote).toHaveBeenCalledTimes(1);
    // Highlight, Add Note, Translate (short enough), Wikipedia, Search web.
    expect(screen.getAllByRole("button")).toHaveLength(5);
    expect(screen.queryByRole("button", { name: "Define" })).not.toBeInTheDocument();
  });

  it("offers Define only for a single-word selection", async () => {
    render(ReaderSelectionMenu, { props: {
      anchor,
      selectedText: "ubiquitous",
      actions: [{ id: "highlight", label: "Highlight", run: vi.fn() }],
      onDismiss: vi.fn(),
    } });

    expect(await screen.findByRole("button", { name: "Define" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Translate" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Wikipedia" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Search web" })).toBeInTheDocument();
  });

  it("dismisses on Escape", async () => {
    const onDismiss = vi.fn();
    render(ReaderSelectionMenu, { props: {
      anchor,
      selectedText: "ubiquitous",
      actions: [{ id: "highlight", label: "Highlight", run: vi.fn() }],
      onDismiss,
    } });
    const button = await screen.findByRole("button", { name: "Highlight" });
    button.focus();

    await fireEvent.keyDown(button, { key: "Escape" });

    await waitFor(() => expect(onDismiss).toHaveBeenCalled());
  });

  it("renders nothing without an anchor", () => {
    render(ReaderSelectionMenu, { props: {
      anchor: null,
      selectedText: "ubiquitous",
      actions: [{ id: "highlight", label: "Highlight", run: vi.fn() }],
      onDismiss: vi.fn(),
    } });

    expect(screen.queryByRole("button", { name: "Highlight" })).not.toBeInTheDocument();
  });
});
