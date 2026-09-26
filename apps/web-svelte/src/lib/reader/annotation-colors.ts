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
