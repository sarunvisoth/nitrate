import type { ReaderFontFamilyCode, ReaderModeCode, ReaderThemeCode } from "$lib/api/generated/codes";
import { READER_FONT_FAMILY, READER_MODE, READER_THEME } from "$lib/api/generated/codes";
import { fetchReaderPreferences, saveReaderPreferences } from "$lib/api/reader-preferences";
import { getStoredTargetLanguage, setStoredTargetLanguage } from "./translate-language";

/** In-memory shape the reader settings sheet edits; mirrors `ReaderPreferencesResponse`. */
export interface ReaderPreferences {
  fontFamily: ReaderFontFamilyCode;
  fontSizePercent: number;
  lineHeight: number;
  marginStep: number;
  maxContentWidth: number;
  theme: ReaderThemeCode;
  flow: ReaderModeCode;
  translationTargetLanguage: string | null;
}

export const READER_PREFERENCES_DEFAULTS: ReaderPreferences = {
  fontFamily: READER_FONT_FAMILY.publisher,
  fontSizePercent: 100,
  lineHeight: 1.5,
  marginStep: 1,
  maxContentWidth: 900,
  theme: READER_THEME.auto,
  flow: READER_MODE.paged,
  translationTargetLanguage: null,
};

const SAVE_DEBOUNCE_MS = 500;

/**
 * The current user's reader preferences, loaded once and saved (debounced, last-write-wins) on
 * every change. Migrates the M5 localStorage translation target language once, when the server
 * has no saved preferences yet and a localStorage value exists.
 */
export class ReaderPreferencesStore {
  #value = $state<ReaderPreferences>(READER_PREFERENCES_DEFAULTS);
  #updatedAt: string | null = null;
  #saveTimer: ReturnType<typeof setTimeout> | null = null;
  #loaded = false;

  constructor(private readonly now: () => string = () => new Date().toISOString()) {}

  get value(): ReaderPreferences {
    return this.#value;
  }

  /** Loads from the server (or migrates from localStorage on a fresh account). */
  async load(): Promise<void> {
    const response = await fetchReaderPreferences();
    if (response.updatedAt) {
      this.#value = {
        fontFamily: response.fontFamily as ReaderFontFamilyCode,
        fontSizePercent: Number(response.fontSizePercent),
        lineHeight: Number(response.lineHeight),
        marginStep: Number(response.marginStep),
        maxContentWidth: Number(response.maxContentWidth),
        theme: response.theme as ReaderThemeCode,
        flow: response.flow as ReaderModeCode,
        translationTargetLanguage: response.translationTargetLanguage,
      };
      this.#updatedAt = response.updatedAt;
      this.#loaded = true;
      return;
    }

    // No server row yet: start from defaults, migrate a previously-local translate language once.
    const local = getStoredTargetLanguage();
    this.#value = { ...READER_PREFERENCES_DEFAULTS, translationTargetLanguage: local };
    this.#loaded = true;
    if (local) this.#scheduleSave();
  }

  /** Refreshes from the server (e.g. on tab focus), keeping unsent local edits. */
  async refresh(): Promise<void> {
    if (this.#saveTimer) return; // a local edit is pending; don't clobber it.
    await this.load();
  }

  /** Applies a partial change immediately and queues a debounced save. */
  update(patch: Partial<ReaderPreferences>): void {
    this.#value = { ...this.#value, ...patch };
    if (patch.translationTargetLanguage !== undefined) {
      setStoredTargetLanguage(patch.translationTargetLanguage ?? "");
    }

    this.#scheduleSave();
  }

  #scheduleSave() {
    if (this.#saveTimer) clearTimeout(this.#saveTimer);
    this.#saveTimer = setTimeout(() => {
      this.#saveTimer = null;
      void this.#save();
    }, SAVE_DEBOUNCE_MS);
  }

  async #save(): Promise<void> {
    if (!this.#loaded) return;
    const updatedAt = this.now();
    const prefs = this.#value;
    try {
      const stored = await saveReaderPreferences({
        fontFamily: prefs.fontFamily,
        fontSizePercent: prefs.fontSizePercent,
        lineHeight: prefs.lineHeight,
        marginStep: prefs.marginStep,
        maxContentWidth: prefs.maxContentWidth,
        theme: prefs.theme,
        flow: prefs.flow,
        translationTargetLanguage: prefs.translationTargetLanguage,
        updatedAt,
      });
      this.#updatedAt = stored.updatedAt ?? updatedAt;
    } catch {
      // Keep the local value; the next edit (or a manual retry) resends it.
    }
  }
}
