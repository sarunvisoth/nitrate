/**
 * Initials for the user avatar disc: first letters of up to two display-name words,
 * falling back to the username, then "?" for degenerate input.
 */
export function initials(displayName: string | null | undefined, username?: string | null): string {
  const source = displayName?.trim() || username?.trim() || "";
  if (!source) return "?";

  const words = source.split(/\s+/).filter(Boolean);
  if (words.length >= 2) {
    return `${words[0][0]}${words[1][0]}`.toUpperCase();
  }

  return source.slice(0, 2).toUpperCase();
}
