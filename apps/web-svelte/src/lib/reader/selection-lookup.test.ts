import { describe, expect, it } from "vitest";
import { isDefinableSelection, isTranslatableSelection } from "./selection-lookup";

describe("isDefinableSelection", () => {
  it("accepts a single word", () => {
    expect(isDefinableSelection("ubiquitous")).toBe(true);
  });

  it("accepts a short hyphenated word", () => {
    expect(isDefinableSelection("well-known")).toBe(true);
  });

  it("accepts up to three words", () => {
    expect(isDefinableSelection("a few words")).toBe(true);
  });

  it("rejects more than three words", () => {
    expect(isDefinableSelection("this is definitely too many words")).toBe(false);
  });

  it("rejects text over the character limit even if it is one long word", () => {
    expect(isDefinableSelection("a".repeat(61))).toBe(false);
  });

  it("accepts text at exactly the character limit", () => {
    expect(isDefinableSelection("a".repeat(60))).toBe(true);
  });

  it("rejects empty or whitespace-only selections", () => {
    expect(isDefinableSelection("")).toBe(false);
    expect(isDefinableSelection("   ")).toBe(false);
  });

  it("trims surrounding whitespace before measuring", () => {
    expect(isDefinableSelection("  ubiquitous  ")).toBe(true);
  });
});

describe("isTranslatableSelection", () => {
  it("accepts a normal passage", () => {
    expect(isTranslatableSelection("A short paragraph of selected prose.")).toBe(true);
  });

  it("rejects empty selections", () => {
    expect(isTranslatableSelection("   ")).toBe(false);
  });

  it("rejects text over 500 characters", () => {
    expect(isTranslatableSelection("a".repeat(501))).toBe(false);
  });

  it("accepts text at exactly 500 characters", () => {
    expect(isTranslatableSelection("a".repeat(500))).toBe(true);
  });
});
