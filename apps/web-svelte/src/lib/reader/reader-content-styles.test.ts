import { describe, expect, it } from "vitest";
import { buildReaderContentStyles, contrastRatio, THEME_COLORS } from "./reader-content-styles";

describe("reader theme contrast", () => {
  it.each(Object.entries(THEME_COLORS))("meets WCAG AA (4.5:1) body text contrast for %s", (_theme, colors) => {
    expect(contrastRatio(colors.foreground, colors.background)).toBeGreaterThanOrEqual(4.5);
  });

  it("sepia keeps at least 4.5:1 contrast (the spec-called-out theme)", () => {
    expect(contrastRatio(THEME_COLORS.sepia.foreground, THEME_COLORS.sepia.background)).toBeGreaterThanOrEqual(4.5);
  });
});

describe("buildReaderContentStyles", () => {
  it("applies the font size percentage and line height", () => {
    const css = buildReaderContentStyles({
      fontFamily: "publisher",
      fontSizePercent: 130,
      lineHeight: 1.8,
      marginStep: 1,
      maxContentWidth: 900,
      theme: "sepia",
    });

    expect(css).toContain("font-size: 130%");
    expect(css).toContain("line-height: 1.8");
    expect(css).toContain(THEME_COLORS.sepia.background);
  });

  it("leaves the font family untouched for the publisher option", () => {
    const css = buildReaderContentStyles({
      fontFamily: "publisher",
      fontSizePercent: 100,
      lineHeight: 1.5,
      marginStep: 0,
      maxContentWidth: 900,
      theme: "auto",
    });

    expect(css).not.toContain("font-family");
  });

  it("forces a bundled font family when one is chosen", () => {
    const css = buildReaderContentStyles({
      fontFamily: "serif",
      fontSizePercent: 100,
      lineHeight: 1.5,
      marginStep: 0,
      maxContentWidth: 900,
      theme: "auto",
    });

    expect(css).toContain("font-family: Georgia");
  });
});
