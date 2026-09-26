import { fireEvent, render, screen } from "@testing-library/svelte";
import { describe, expect, it, vi } from "vitest";
import { ANNOTATION_COLOR } from "$lib/api/generated/codes";
import AnnotationEditor from "./AnnotationEditor.svelte";

const anchor = { left: 100, top: 200, width: 40, height: 16 };

function renderEditor(overrides: Record<string, unknown> = {}) {
  const props = {
    anchor,
    color: ANNOTATION_COLOR.yellow,
    note: "Existing note",
    onChangeColor: vi.fn(),
    onSaveNote: vi.fn(),
    onDelete: vi.fn(),
    onDismiss: vi.fn(),
    ...overrides,
  };
  render(AnnotationEditor, { props });
  return props;
}

describe("AnnotationEditor", () => {
  it("emits the colour code for a swatch and marks the current colour", async () => {
    const props = renderEditor();

    const yellow = await screen.findByRole("button", { name: "Yellow" });
    await fireEvent.click(screen.getByRole("button", { name: "Purple" }));

    expect(yellow).toHaveAttribute("aria-pressed", "true");
    expect(props.onChangeColor).toHaveBeenCalledWith(ANNOTATION_COLOR.purple);
  });

  it("saves the edited note text", async () => {
    const props = renderEditor();
    const field = await screen.findByRole("textbox", { name: "Note" });
    expect(field).toHaveValue("Existing note");

    await fireEvent.input(field, { target: { value: "Compare with rectified flow." } });
    await fireEvent.click(screen.getByRole("button", { name: "Save note" }));

    expect(props.onSaveNote).toHaveBeenCalledWith("Compare with rectified flow.");
  });

  it("deletes the highlight", async () => {
    const props = renderEditor();

    await fireEvent.click(await screen.findByRole("button", { name: "Delete highlight" }));

    expect(props.onDelete).toHaveBeenCalledTimes(1);
  });
});
