import type { ReaderFontFamilyCode, ReaderThemeCode } from "$lib/api/generated/codes";

/** The reader typography/theme settings that translate into injected book-content CSS. */
export interface ReaderContentPreferences {
  fontFamily: ReaderFontFamilyCode;
  fontSizePercent: number;
  lineHeight: number;
  marginStep: number;
  maxContentWidth: number;
  theme: ReaderThemeCode;
}

interface ThemeColors {
  background: string;
  foreground: string;
  link: string;
  linkHover: string;
}

/** Fixed palette per theme. Sepia keeps a warm paper tone at or above 4.5:1 text contrast. */
const THEME_COLORS: Record<Exclude<ReaderThemeCode, "auto">, ThemeColors> = {
  light: { background: "#f5f5f4", foreground: "#1c1c1e", link: "#3452c9", linkHover: "#22348f" },
  dark: { background: "#141416", foreground: "#e7e7ea", link: "#c7c9cc", linkHover: "#f7d59a" },
  sepia: { background: "#f1e7d0", foreground: "#3a2f1f", link: "#5a4326", linkHover: "#7a5b34" },
};

/** "Auto" matches the app's dark chrome: transparent background over the existing shell. */
const AUTO: ThemeColors = { background: "transparent !important", foreground: "#e7e7ea", link: "#c7c9cc", linkHover: "#f7d59a" };

const FONT_FAMILY_CSS: Record<ReaderFontFamilyCode, string | null> = {
  publisher: null,
  serif: "Georgia, 'Times New Roman', serif",
  sans: "'Inter', system-ui, sans-serif",
  monospace: "'JetBrains Mono', ui-monospace, monospace",
};

/** Margin steps 0-4 map to a side-padding scale, widest at step 4. */
const MARGIN_STEP_PX = [0, 16, 32, 48, 64] as const;

/** Builds the CSS injected into the book's content via `view.renderer.setStyles(css)`. */
export function buildReaderContentStyles(prefs: ReaderContentPreferences): string {
  const colors = prefs.theme === "auto" ? AUTO : THEME_COLORS[prefs.theme];
  const family = FONT_FAMILY_CSS[prefs.fontFamily];
  const margin = MARGIN_STEP_PX[Math.max(0, Math.min(MARGIN_STEP_PX.length - 1, prefs.marginStep))];
  return `
    html { color-scheme: ${prefs.theme === "light" || prefs.theme === "sepia" ? "light" : "dark"}; font-size: ${prefs.fontSizePercent}%; }
    html, body {
      background: ${colors.background} !important;
      color: ${colors.foreground} !important;
      line-height: ${prefs.lineHeight};
    }
    body { max-width: ${prefs.maxContentWidth}px; margin: 0 auto; padding-left: ${margin}px; padding-right: ${margin}px; }
    ${family ? `html, body, p, div, span { font-family: ${family} !important; }` : ""}
    a, a:link, a:visited, a *,
    a[href], a[href]:link, a[href]:visited, a[href] * {
      color: ${colors.link} !important;
      text-decoration: underline;
      text-underline-offset: 2px;
    }
    a:hover, a:hover *, a[href]:hover, a[href]:hover * { color: ${colors.linkHover} !important; }
    img { background: transparent; }
  `;
}

/** Parses a `#rrggbb` colour into 0-255 channels. */
function parseHex(hex: string): [number, number, number] {
  const value = hex.replace("#", "");
  return [
    parseInt(value.slice(0, 2), 16),
    parseInt(value.slice(2, 4), 16),
    parseInt(value.slice(4, 6), 16),
  ];
}

/** WCAG relative luminance of an sRGB channel. */
function channelLuminance(channel: number): number {
  const normalized = channel / 255;
  return normalized <= 0.03928 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
}

/** WCAG contrast ratio (1-21) between two `#rrggbb` colours. */
export function contrastRatio(hexA: string, hexB: string): number {
  const luminanceOf = (hex: string) => {
    const [r, g, b] = parseHex(hex);
    return 0.2126 * channelLuminance(r) + 0.7152 * channelLuminance(g) + 0.0722 * channelLuminance(b);
  };
  const [lighter, darker] = [luminanceOf(hexA), luminanceOf(hexB)].sort((a, b) => b - a);
  return (lighter + 0.05) / (darker + 0.05);
}

export { THEME_COLORS };
