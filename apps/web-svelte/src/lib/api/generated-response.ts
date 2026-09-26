export interface RequestOptions {
  signal?: AbortSignal;
}

export function requestInit(options?: RequestOptions): RequestInit | undefined {
  return options?.signal ? { signal: options.signal } : undefined;
}

export function problemMessage(data: unknown): string | null {
  if (data && typeof data === "object") {
    const record = data as Record<string, unknown>;
    if (typeof record.message === "string") return record.message;
    if (typeof record.error === "string") return record.error;
    if (typeof record.detail === "string") return record.detail;
    if (typeof record.title === "string") return record.title;
  }

  if (typeof data === "string" && data.trim()) return data;
  return null;
}

export function unwrapGenerated<T>(
  response: { data: unknown; status: number },
  fallback: string,
  okStatuses: readonly number[] = [200],
): T {
  if (!okStatuses.includes(response.status)) {
    throw new Error(problemMessage(response.data) ?? fallback);
  }

  return response.data as T;
}
