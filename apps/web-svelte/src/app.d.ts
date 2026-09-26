// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
import "vidstack/svelte";
import type { NsfwMode } from "$lib/nsfw/cookie";
import type { AuthUser } from "$lib/api/auth";

declare global {
  namespace App {
    // interface Error {}
    // interface Locals {}
    interface PageData {
      initialCollapsed?: boolean;
      initialNsfwMode?: NsfwMode;
      user?: AuthUser | null;
      needsSetup?: boolean;
    }
    // interface PageState {}
    // interface Platform {}
  }
}

export {};
