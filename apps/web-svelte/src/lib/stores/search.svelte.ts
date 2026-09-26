import { browser } from "$app/environment";
import { createContext } from "$lib/utils/context";
import { isModK } from "../nsfw/hotkey";

const ctx = createContext<SearchStore>("Search");

export class SearchStore {
  open = $state(false);
  private attached = false;

  constructor() {
    if (!browser) return;
    $effect.root(() => {
      if (this.attached) return;
      this.attached = true;
      const handler = (e: KeyboardEvent) => {
        if (isModK(e)) {
          e.preventDefault();
          e.stopPropagation();
          this.open = true;
          return;
        }
        if (e.key === "Escape" && this.open) {
          e.preventDefault();
          e.stopPropagation();
          this.open = false;
        }
      };
      window.addEventListener("keydown", handler, true);
      return () => {
        window.removeEventListener("keydown", handler, true);
        this.attached = false;
      };
    });
  }

  openPalette() {
    this.open = true;
  }

  closePalette() {
    this.open = false;
  }
}

export function provideSearch() {
  return ctx.provide(new SearchStore());
}

export const useSearch = ctx.use;
