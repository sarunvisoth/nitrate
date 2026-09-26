/**
 * Rules deciding when the reader's "Define" action applies to the current text selection.
 * Define only makes sense for a single word (or a short hyphenated/apostrophe'd word like
 * "well-known" or "don't"), never a sentence — so it is offered only for a short, few-word
 * selection; longer selections still get Translate/Wikipedia/Search web.
 */

/** Selections longer than this many characters are never definable. */
export const MAX_DEFINABLE_LENGTH = 60;

/** Selections with more than this many words are never definable. */
export const MAX_DEFINABLE_WORDS = 3;

/**
 * True when `text` is short enough to look up in a dictionary: at most
 * {@link MAX_DEFINABLE_WORDS} whitespace-separated words and at most
 * {@link MAX_DEFINABLE_LENGTH} characters, once trimmed. Empty or whitespace-only text is not
 * definable.
 */
export function isDefinableSelection(text: string): boolean {
  const trimmed = text.trim();
  if (!trimmed || trimmed.length > MAX_DEFINABLE_LENGTH) return false;
  const words = trimmed.split(/\s+/).filter(Boolean);
  return words.length > 0 && words.length <= MAX_DEFINABLE_WORDS;
}

/** Selections longer than this many characters cannot be sent to Translate (server enforces this too). */
export const MAX_TRANSLATABLE_LENGTH = 500;

/** True when `text` is non-empty and short enough for the Translate action. */
export function isTranslatableSelection(text: string): boolean {
  const trimmed = text.trim();
  return trimmed.length > 0 && trimmed.length <= MAX_TRANSLATABLE_LENGTH;
}
