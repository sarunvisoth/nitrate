/**
 * Per-device target-translation-language preference. Stored in `localStorage` (not per-user
 * reader settings yet — M6 moves it there). The default comes from the browser's own language,
 * falling back to English when that is unavailable or storage cannot be read.
 */

const STORAGE_KEY = "nitrate.reader.translateLanguage";
const FALLBACK_LANGUAGE = "en";

/** A short list of common target languages for the translate popover's language picker. */
export const TRANSLATE_LANGUAGE_OPTIONS: readonly { value: string; label: string }[] = [
  { value: "en", label: "English" },
  { value: "es", label: "Spanish" },
  { value: "fr", label: "French" },
  { value: "de", label: "German" },
  { value: "it", label: "Italian" },
  { value: "pt", label: "Portuguese" },
  { value: "nl", label: "Dutch" },
  { value: "ru", label: "Russian" },
  { value: "ja", label: "Japanese" },
  { value: "ko", label: "Korean" },
  { value: "zh", label: "Chinese" },
  { value: "ar", label: "Arabic" },
  { value: "hi", label: "Hindi" },
];

/** Derives the default target language from the browser's primary language subtag. */
export function defaultTargetLanguage(locale: string | undefined = globalThis.navigator?.language): string {
  const primary = locale?.split(/[-_]/)[0]?.toLowerCase().trim();
  return primary && /^[a-z]{2,3}$/.test(primary) ? primary : FALLBACK_LANGUAGE;
}

/** Reads the stored target-language preference, falling back to the device default. */
export function getStoredTargetLanguage(): string {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored && stored.trim()) return stored.trim();
  } catch {
    // storage unavailable (private mode); fall back to the device default
  }
  return defaultTargetLanguage();
}

/** Persists the target-language preference for future reader sessions on this device. */
export function setStoredTargetLanguage(language: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, language);
  } catch {
    // ignore storage failures; the choice still applies for this session
  }
}
