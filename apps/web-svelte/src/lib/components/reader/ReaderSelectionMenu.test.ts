import { fireEvent, render, screen, waitFor } from "@testing-library/svelte";
import { describe, expect, it, vi } from "vitest";
import ReaderSelectionMenu from "./ReaderSelectionMenu.svelte";

const anchor = { left: 100, top: 200, width: 40, height: 16 };

describe("ReaderSelectionMenu", () => {
  it("renders exactly the given actions and runs them", async () => {
    const highlight = vi.fn();
    const addNote = vi.fn();
    render(ReaderSelectionMenu, { props: {
      anchor,
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
    expect(screen.getAllByRole("button")).toHaveLength(2);
  });

  it("dismisses on Escape", async () => {
    const onDismiss = vi.fn();
    render(ReaderSelectionMenu, { props: {
      anchor,
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
      actions: [{ id: "highlight", label: "Highlight", run: vi.fn() }],
      onDismiss: vi.fn(),
    } });

    expect(screen.queryByRole("button", { name: "Highlight" })).not.toBeInTheDocument();
  });
});
