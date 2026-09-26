import { beforeEach, describe, expect, it } from "vitest";
import {
  defaultTargetLanguage,
  getStoredTargetLanguage,
  setStoredTargetLanguage,
} from "./translate-language";

describe("defaultTargetLanguage", () => {
  it("uses the primary subtag of the given locale", () => {
    expect(defaultTargetLanguage("fr-FR")).toBe("fr");
  });

  it("lowercases the subtag", () => {
    expect(defaultTargetLanguage("EN-US")).toBe("en");
  });

  it("falls back to English when the locale is missing", () => {
    expect(defaultTargetLanguage(undefined)).toBe("en");
  });

  it("falls back to English when the subtag is not alphabetic", () => {
    expect(defaultTargetLanguage("123")).toBe("en");
  });
});

describe("stored target language", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("returns the device default when nothing is stored", () => {
    expect(getStoredTargetLanguage()).toBe(defaultTargetLanguage());
  });

  it("round-trips a stored preference", () => {
    setStoredTargetLanguage("de");
    expect(getStoredTargetLanguage()).toBe("de");
  });
});
