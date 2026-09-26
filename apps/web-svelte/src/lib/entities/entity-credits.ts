import { CREDIT_ROLE } from "./entity-codes";

export interface EntityCredit {
  character: string | null;
  person: {
    id: string;
    kind: string;
    title: string;
    thumbnailUrl?: string | null;
  };
  role: string | null;
}

/** Returns the user-facing subtitle for an entity credit thumbnail. */
export function creditSubtitle(credit: EntityCredit): string | undefined {
  return creditRoleCharacterSubtitle(credit.role, credit.character);
}

/**
 * Returns the user-facing subtitle for a credited person: the character when known,
 * otherwise the humanized role (suppressed for the generic person role).
 */
export function creditRoleCharacterSubtitle(
  role: string | null | undefined,
  character: string | null | undefined,
): string | undefined {
  const trimmedCharacter = character?.trim();
  if (trimmedCharacter) return trimmedCharacter;
  const normalizedRole = (role ?? "").trim().toLowerCase();
  if (!normalizedRole || normalizedRole === CREDIT_ROLE.person) return undefined;
  return creditRoleLabel(role);
}

/** Humanizes a credit role code for display (e.g. "director" → "Director"). */
export function creditRoleLabel(role: string | null | undefined): string {
  const normalized = (role ?? "").trim();
  if (!normalized) return creditRoleLabel(CREDIT_ROLE.person);
  return normalized
    .replaceAll("-", " ")
    .replaceAll("_", " ")
    .replace(/\b\w/g, (value) => value.toUpperCase());
}
