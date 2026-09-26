import { describe, expect, it } from "vitest";
import { selectionAnchorRect, textQuoteFor } from "./epub-selection";

describe("textQuoteFor", () => {
  it("captures the exact text plus bounded context on both sides", () => {
    document.body.innerHTML = "<p>The quick brown fox jumps over the lazy dog.</p>";
    const text = document.querySelector("p")!.firstChild!;
    const range = document.createRange();
    range.setStart(text, 10);
    range.setEnd(text, 19);

    expect(textQuoteFor(range, 6)).toEqual({
      selectedText: "brown fox",
      textBefore: "quick ",
      textAfter: " jumps",
    });
  });

  it("returns null context at the document edges", () => {
    document.body.innerHTML = "<p>Edge</p>";
    const text = document.querySelector("p")!.firstChild!;
    const range = document.createRange();
    range.setStart(text, 0);
    range.setEnd(text, 4);

    expect(textQuoteFor(range)).toEqual({ selectedText: "Edge", textBefore: null, textAfter: null });
  });
});

describe("selectionAnchorRect", () => {
  it("offsets an iframe-local rect by the frame's position in the top window", () => {
    expect(
      selectionAnchorRect({ left: 10, top: 20, width: 30, height: 5 }, { left: 100, top: 200 }),
    ).toEqual({ left: 110, top: 220, width: 30, height: 5 });
  });

  it("makes the rect relative to a container when one is given", () => {
    expect(
      selectionAnchorRect({ left: 10, top: 20, width: 30, height: 5 }, { left: 100, top: 200 }, { left: 40, top: 60 }),
    ).toEqual({ left: 70, top: 160, width: 30, height: 5 });
  });
});
