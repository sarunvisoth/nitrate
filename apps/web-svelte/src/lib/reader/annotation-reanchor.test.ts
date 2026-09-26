import { describe, expect, it } from "vitest";
import { findTextQuote, sameQuotedText } from "./annotation-reanchor";

describe("findTextQuote", () => {
  it("finds the quote across element boundaries", () => {
    document.body.innerHTML = "<p>The <em>luminous</em> harbor lanterns glowed.</p>";
    const range = findTextQuote(document, { selectedText: "luminous harbor lanterns", textBefore: "The ", textAfter: " glowed" });
    expect(range?.toString()).toBe("luminous harbor lanterns");
  });

  it("prefers the occurrence whose surrounding text matches", () => {
    document.body.innerHTML =
      "<p>First the gull argued with the wind.</p><p>Later the gull argued with the tide.</p>";
    const range = findTextQuote(document, { selectedText: "the gull argued", textBefore: "Later ", textAfter: " with the tide" });
    expect(range?.startContainer.parentElement?.textContent).toContain("Later");
  });

  it("returns null when the text is gone", () => {
    document.body.innerHTML = "<p>Completely different edition text.</p>";
    expect(findTextQuote(document, { selectedText: "luminous harbor lanterns", textBefore: null, textAfter: null })).toBeNull();
  });

  it("compares quoted text ignoring whitespace differences", () => {
    expect(sameQuotedText("luminous  harbor\nlanterns", "luminous harbor lanterns")).toBe(true);
    expect(sameQuotedText("harbor", "lanterns")).toBe(false);
  });
});
