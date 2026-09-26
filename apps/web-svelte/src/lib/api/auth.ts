// Authentication wrappers over the generated client: setup gate, login/logout, the
// current user, and self-service password/profile/session management. Bootstrap-time
// calls pass `on401: "ignore"` so the global login redirect never fires while the app
// is still deciding whether the user is signed in.

import {
  changeOwnPassword,
  completeSetup,
  getCurrentUser,
  getSetupStatus,
  listOwnSessions,
  login as loginGenerated,
  logout as logoutGenerated,
  revokeOwnSession,
  updateCurrentUser,
} from "$lib/api/generated/prismedia";
import type {
  ChangeOwnPasswordRequest,
  CreateFirstAdminRequest,
  LoginRequest,
  LoginResponse,
  SetupStatusResponse,
  UserResponse,
  UserSessionsResponse,
} from "$lib/api/generated/model";
import { unwrapGenerated } from "$lib/api/generated-response";
import { ApiError, IGNORE_401 } from "$lib/api/orval-fetch";

export type AuthUser = UserResponse;

export async function fetchSetupStatus(): Promise<SetupStatusResponse> {
  return unwrapGenerated<SetupStatusResponse>(
    await getSetupStatus(IGNORE_401),
    "Failed to load setup status",
  );
}

/**
 * Setup status with a short retry, for boot paths: a fresh install must never be
 * misread as "setup done" because of one transient failure while the server (or a
 * proxy in front of it) is still settling. Null when every attempt failed — callers
 * treat that as unknown, not as "no setup needed".
 */
export async function fetchSetupStatusWithRetry(
  attempts = 3,
  delayMs = 400,
): Promise<SetupStatusResponse | null> {
  for (let attempt = 0; attempt < attempts; attempt++) {
    try {
      return await fetchSetupStatus();
    } catch {
      if (attempt < attempts - 1) {
        await new Promise((resolve) => setTimeout(resolve, delayMs * (attempt + 1)));
      }
    }
  }

  return null;
}

export async function submitSetup(request: CreateFirstAdminRequest): Promise<LoginResponse> {
  return unwrapGenerated<LoginResponse>(
    await completeSetup(request, IGNORE_401),
    "Failed to complete setup",
  );
}

export async function login(request: LoginRequest): Promise<LoginResponse> {
  return unwrapGenerated<LoginResponse>(
    await loginGenerated(request, IGNORE_401),
    "Failed to sign in",
  );
}

export async function logout(): Promise<void> {
  await logoutGenerated();
}

/** The signed-in user, or null when the session is missing or expired. */
export async function fetchCurrentUser(): Promise<AuthUser | null> {
  try {
    return unwrapGenerated<AuthUser>(
      await getCurrentUser(IGNORE_401),
      "Failed to load the signed-in user",
    );
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null;
    }

    throw error;
  }
}

export async function updateProfile(displayName: string): Promise<AuthUser> {
  return unwrapGenerated<AuthUser>(
    await updateCurrentUser({ displayName }),
    "Failed to update profile",
  );
}

export async function changePassword(request: ChangeOwnPasswordRequest): Promise<void> {
  unwrapGenerated<void>(await changeOwnPassword(request), "Failed to change password", [204]);
}

export async function fetchOwnSessions(): Promise<UserSessionsResponse> {
  return unwrapGenerated<UserSessionsResponse>(
    await listOwnSessions(),
    "Failed to load sessions",
  );
}

export async function revokeSession(sessionId: string): Promise<void> {
  unwrapGenerated<void>(await revokeOwnSession(sessionId), "Failed to revoke session", [204]);
}
