import { describe, expect, it } from "vitest";
import { bookmarkOnPage, pageStartLocator } from "./bookmark-page";

// A visible page spanning from paragraph 2 to paragraph 6 of section 2.
const PAGE = "epubcfi(/6/4!/4,/4/1:0,/12/1:30)";
const make = (id: string, locator: string) => ({ id, locator });

describe("bookmark-page", () => {
  it("uses the start of the visible range as the bookmark locator", () => {
    expect(pageStartLocator(PAGE)).toBe("epubcfi(/6/4!/4/4/1:0)");
    expect(pageStartLocator(null)).toBeNull();
  });

  it("finds a bookmark whose locator falls inside the visible page", () => {
    const inside = make("in", "epubcfi(/6/4!/4/8/1:5)");
    const before = make("before", "epubcfi(/6/4!/4/2/1:0)");
    const nextSection = make("next", "epubcfi(/6/6!/4/2/1:0)");
    expect(bookmarkOnPage([before, nextSection, inside], PAGE)?.id).toBe("in");
    expect(bookmarkOnPage([before, nextSection], PAGE)).toBeUndefined();
  });

  it("tolerates malformed locators", () => {
    expect(bookmarkOnPage([make("bad", "not-a-cfi")], PAGE)).toBeUndefined();
  });
});
