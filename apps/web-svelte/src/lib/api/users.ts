// Admin user-management wrappers over the generated client: account CRUD, password
// resets, and per-user library access.

import {
  createUser as createUserGenerated,
  deleteUser as deleteUserGenerated,
  listUsers,
  replaceUserLibraryAccess,
  setUserPassword,
  updateUser as updateUserGenerated,
} from "$lib/api/generated/prismedia";
import type {
  UserCreateRequest,
  UserResponse,
  UsersResponse,
  UserUpdateRequest,
} from "$lib/api/generated/model";
import { unwrapGenerated } from "$lib/api/generated-response";

export async function fetchUsers(): Promise<UserResponse[]> {
  const response = unwrapGenerated<UsersResponse>(await listUsers(), "Failed to load users");
  return [...response.items];
}

export async function createUser(request: UserCreateRequest): Promise<UserResponse> {
  return unwrapGenerated<UserResponse>(
    await createUserGenerated(request),
    "Failed to create user",
  );
}

export async function updateUser(userId: string, request: UserUpdateRequest): Promise<UserResponse> {
  return unwrapGenerated<UserResponse>(
    await updateUserGenerated(userId, request),
    "Failed to update user",
  );
}

export async function resetUserPassword(userId: string, newPassword: string): Promise<void> {
  unwrapGenerated<void>(
    await setUserPassword(userId, { newPassword }),
    "Failed to reset password",
    [204],
  );
}

export async function deleteUser(userId: string): Promise<void> {
  unwrapGenerated<void>(await deleteUserGenerated(userId), "Failed to delete user", [204]);
}

export async function replaceLibraryAccessForUser(
  userId: string,
  libraryRootIds: string[],
): Promise<void> {
  unwrapGenerated<void>(
    await replaceUserLibraryAccess(userId, { libraryRootIds }),
    "Failed to update library access",
    [204],
  );
}
