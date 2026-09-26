import type { Component } from "svelte";
import { BookOpenText, Globe, Languages } from "@lucide/svelte";
import { isDefinableSelection, isTranslatableSelection } from "./selection-lookup";

/** One reader-lookup selection-menu entry (matches `ReaderSelectionAction`'s shape). */
export interface LookupSelectionAction {
  id: string;
  label: string;
  icon: Component;
  run: () => void;
}

export interface LookupActionCallbacks {
  onDefine: () => void;
  onTranslate: () => void;
  onWikipedia: () => void;
}

/**
 * Where the current selection lives in the book: enough to save a vocabulary entry that links
 * back to its source (bookId + CFI locator) and shows the surrounding sentence. Null when there
 * is no book/annotation context (e.g. a fixed-layout book, or annotations disabled).
 */
export interface LookupSourceContext {
  bookId: string | null;
  locator: string;
  textBefore: string | null;
  textAfter: string | null;
}

const SOURCE_SENTENCE_MAX_LENGTH = 1000;

/** Joins the text-quote before/selected/after into one surrounding-sentence string, clipped. */
export function buildSourceSentence(selectedText: string, context: LookupSourceContext | null): string | null {
  if (!context) return null;
  const sentence = `${context.textBefore ?? ""}${selectedText}${context.textAfter ?? ""}`.trim();
  return sentence ? sentence.slice(0, SOURCE_SENTENCE_MAX_LENGTH) : null;
}

/**
 * Builds the Define/Translate/Wikipedia selection-menu entries for the given selected text.
 * Define only appears for a single short word/phrase; Translate only for text short enough to
 * send to the translate endpoint; Wikipedia is always offered.
 */
export function buildLookupActions(
  selectedText: string,
  callbacks: LookupActionCallbacks,
): LookupSelectionAction[] {
  const actions: LookupSelectionAction[] = [];
  if (isDefinableSelection(selectedText)) {
    actions.push({ id: "define", label: "Define", icon: BookOpenText, run: callbacks.onDefine });
  }
  if (isTranslatableSelection(selectedText)) {
    actions.push({ id: "translate", label: "Translate", icon: Languages, run: callbacks.onTranslate });
  }
  actions.push({ id: "wikipedia", label: "Wikipedia", icon: Globe, run: callbacks.onWikipedia });
  return actions;
}

/** Builds the DuckDuckGo search URL for the "Search web" selection-menu action. */
export function searchWebUrl(text: string): string {
  return `https://duckduckgo.com/?q=${encodeURIComponent(text.trim())}`;
}
