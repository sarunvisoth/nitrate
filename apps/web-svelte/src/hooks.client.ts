import type { HandleClientError } from "@sveltejs/kit";

/**
 * Unexpected client-side errors would otherwise vanish into the console; surface them
 * on the window for diagnostics tooling and keep the default error page behavior.
 */
export const handleError: HandleClientError = ({ error }) => {
  const detail = error instanceof Error ? (error.stack ?? error.message) : String(error);
  (window as Window & { __prismediaLastError?: string }).__prismediaLastError = detail;
  console.error("[prismedia] unexpected client error:", error);
};
