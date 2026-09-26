import { describe, expect, it } from "vitest";
import { buildSourceSentence, type LookupSourceContext } from "./reader-lookup-actions";

const context: LookupSourceContext = {
  bookId: "book-1",
  locator: "epubcfi(/6/4!/4/2,/1:0,/1:5)",
  textBefore: "The dawn was ",
  textAfter: " over the hills.",
};

describe("buildSourceSentence", () => {
  it("joins textBefore + selection + textAfter into one sentence", () => {
    expect(buildSourceSentence("luminous", context)).toBe("The dawn was luminous over the hills.");
  });

  it("trims surrounding whitespace", () => {
    const padded: LookupSourceContext = { ...context, textBefore: "  ", textAfter: "  " };
    expect(buildSourceSentence("luminous", padded)).toBe("luminous");
  });

  it("returns null when there is no source context", () => {
    expect(buildSourceSentence("luminous", null)).toBeNull();
  });

  it("tolerates missing before/after text", () => {
    const noContext: LookupSourceContext = { bookId: null, locator: "epubcfi(/6/4)", textBefore: null, textAfter: null };
    expect(buildSourceSentence("luminous", noContext)).toBe("luminous");
  });

  it("clips to 1000 characters", () => {
    const long: LookupSourceContext = { ...context, textBefore: "a".repeat(2000), textAfter: null };
    expect(buildSourceSentence("luminous", long)?.length).toBe(1000);
  });
});
