import { browser } from "$app/environment";
import { invalidateAll } from "$app/navigation";
import { createContext } from "$lib/utils/context";
import { writeCookie as setCookie } from "$lib/utils/cookie";
import { isModShiftZ } from "./hotkey";
import { type NsfwMode } from "./cookie";

const COOKIE_NAME = "prismedia-nsfw-mode";
const ctx = createContext<NsfwStore>("Nsfw");

function writeNsfwCookie(mode: NsfwMode) {
  setCookie(COOKIE_NAME, mode);
}

/**
 * Tell SvelteKit to re-run every `+page.server.ts` / `+layout.server.ts`
 * loader. The NSFW cookie is read by those loaders to build API
 * query-strings, so any mode change must
 * refresh the data the API returned with the old value. Without this,
 * grids keep displaying the pre-toggle filtering until a hard reload.
 */
function refetchServerData() {
  if (!browser) return;
  void invalidateAll();
}

/**
 * Device-local NSFW visibility toggle, capped by the signed-in user's server-side
 * permission: when the account is not allowed NSFW content, the mode is pinned to
 * "off" and every toggle surface (including ⌘⇧Z) is inert — the server filters
 * regardless, so an active-looking toggle would be a lie.
 */
export class NsfwStore {
  mode = $state<NsfwMode>("off");
  /** Server-side cap: false means this account never sees NSFW content. */
  readonly allowed: boolean;
  private keydownAttached = false;

  constructor(opts: { initialMode: NsfwMode; allowed: boolean }) {
    this.allowed = opts.allowed;
    this.mode = opts.allowed ? opts.initialMode : "off";

    if (!browser) return;

    // Global keydown for ⌘⇧Z / Ctrl+Shift+Z
    $effect.root(() => {
      if (this.keydownAttached || !this.allowed) return;
      this.keydownAttached = true;
      const handler = (e: KeyboardEvent) => {
        if (!isModShiftZ(e)) return;
        e.preventDefault();
        e.stopPropagation();
        this.toggleShowOff();
      };
      window.addEventListener("keydown", handler, true);
      return () => {
        window.removeEventListener("keydown", handler, true);
        this.keydownAttached = false;
      };
    });
  }

  setMode(next: NsfwMode) {
    if (!this.allowed || this.mode === next) return;
    this.mode = next;
    writeNsfwCookie(next);
    refetchServerData();
  }

  toggleShowOff() {
    const next = this.mode === "show" ? "off" : "show";
    this.setMode(next);
  }
}

export function provideNsfw(getOpts: () => { initialMode: NsfwMode; allowed: boolean }) {
  return ctx.provide(new NsfwStore(getOpts()));
}

export const useNsfw = ctx.use;
