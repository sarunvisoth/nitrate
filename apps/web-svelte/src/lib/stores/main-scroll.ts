export const MAIN_SCROLL_TOP_EVENT = "prismedia:main-scroll-top";

export function requestMainScrollTop(): void {
  if (typeof window === "undefined") return;
  window.dispatchEvent(new CustomEvent(MAIN_SCROLL_TOP_EVENT));
}
