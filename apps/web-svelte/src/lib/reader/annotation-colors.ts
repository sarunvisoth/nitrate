import { ANNOTATION_COLOR, type AnnotationColorCode } from "$lib/api/generated/codes";

/**
 * Highlight paint for each annotation colour. Highlights are user content drawn over book text
 * (foliate's overlayer applies ~30% opacity), so they sit outside the entity-spectrum UI palette.
 * Values are saturated mid-tones that stay legible behind the reader's light text on dark ground.
 */
export const ANNOTATION_COLOR_PAINT: Record<AnnotationColorCode, string> = {
  [ANNOTATION_COLOR.yellow]: "#facc15",
  [ANNOTATION_COLOR.blue]: "#60a5fa",
  [ANNOTATION_COLOR.green]: "#4ade80",
  [ANNOTATION_COLOR.red]: "#f87171",
  [ANNOTATION_COLOR.purple]: "#c084fc",
};

/** Human-readable swatch labels (display text). */
export const ANNOTATION_COLOR_LABEL: Record<AnnotationColorCode, string> = {
  [ANNOTATION_COLOR.yellow]: "Yellow",
  [ANNOTATION_COLOR.blue]: "Blue",
  [ANNOTATION_COLOR.green]: "Green",
  [ANNOTATION_COLOR.red]: "Red",
  [ANNOTATION_COLOR.purple]: "Purple",
};

/** Swatch order shown in the reader. */
export const ANNOTATION_COLOR_ORDER: readonly AnnotationColorCode[] = [
  ANNOTATION_COLOR.yellow,
  ANNOTATION_COLOR.blue,
  ANNOTATION_COLOR.green,
  ANNOTATION_COLOR.red,
  ANNOTATION_COLOR.purple,
];

/** Paint for a stored colour code; unknown codes fall back to yellow. */
export function annotationColorPaint(code: string): string {
  return ANNOTATION_COLOR_PAINT[code as AnnotationColorCode] ?? ANNOTATION_COLOR_PAINT[ANNOTATION_COLOR.yellow];
}

const LAST_COLOR_STORAGE_KEY = "nitrate.reader.lastHighlightColor";

/** The last colour the reader used for a highlight, remembered per-device across sessions/formats. */
export function readLastHighlightColor(): AnnotationColorCode {
  try {
    const stored = localStorage.getItem(LAST_COLOR_STORAGE_KEY);
    if (stored && (ANNOTATION_COLOR_ORDER as readonly string[]).includes(stored)) return stored as AnnotationColorCode;
  } catch {
    // storage unavailable (private mode); fall back to the default colour
  }
  return ANNOTATION_COLOR.yellow;
}

/** Remembers the last-used highlight colour for next time. */
export function rememberHighlightColor(color: AnnotationColorCode): void {
  try {
    localStorage.setItem(LAST_COLOR_STORAGE_KEY, color);
  } catch {
    // ignore storage failures; the colour still applies for this session
  }
}
