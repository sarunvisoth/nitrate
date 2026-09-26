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
