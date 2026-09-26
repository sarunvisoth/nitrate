/** Any record synced with last-write-wins on `updatedAt` and tombstoned via `deletedAt`. */
export interface SyncedItem {
  id: string;
  updatedAt: string;
  deletedAt?: string | null;
}

/** Server operations for one collection (e.g. one Book's annotations or bookmarks). */
export interface SyncedItemApi<T extends SyncedItem> {
  list(): Promise<readonly T[]>;
  save(item: T): Promise<T>;
  remove(id: string, updatedAt: string): Promise<T>;
}

/** Keeps whichever version of the same item has the later `updatedAt` (ties keep `incoming`). */
export function mergeNewest<T extends SyncedItem>(current: T | undefined, incoming: T): T {
  if (!current) return incoming;
  return Date.parse(incoming.updatedAt) >= Date.parse(current.updatedAt) ? incoming : current;
}

/**
 * Reader-side copy of one synced collection. Writes apply locally first (optimistic), are sent to the server one
 * at a time, and the server's answer is merged back (last write wins on `updatedAt`, so a newer edit from another
 * device replaces ours). Writes that fail stay pending and are resent on the next `refresh()`, in memory only.
 */
export class SyncedItemStore<T extends SyncedItem> {
  #items = $state.raw(new Map<string, T>());
  readonly #pending = new Map<string, T>();
  #queue: Promise<void> = Promise.resolve();

  constructor(
    private readonly api: SyncedItemApi<T>,
    protected readonly now: () => string = () => new Date().toISOString(),
  ) {}

  /** Every known item, tombstones included. */
  get all(): T[] {
    return [...this.#items.values()];
  }

  /** Pulls the server list, merges it, and resends local writes the server has not seen. */
  async refresh(): Promise<void> {
    const server = await this.api.list();
    this.#mergeIn(server);
    for (const local of this.#pending.values()) {
      const stored = server.find((item) => item.id === local.id);
      if (!stored || Date.parse(local.updatedAt) > Date.parse(stored.updatedAt)) this.#send(local);
      else this.#pending.delete(local.id);
    }
  }

  /** Resolves once every queued write has been attempted. */
  idle(): Promise<void> {
    return this.#queue;
  }

  /** Applies a write locally and queues it for the server. */
  protected write(item: T): void {
    this.#items = new Map(this.#items).set(item.id, item);
    this.#pending.set(item.id, item);
    this.#send(item);
  }

  #mergeIn(items: readonly T[]) {
    const next = new Map(this.#items);
    for (const item of items) next.set(item.id, mergeNewest(next.get(item.id), item));
    this.#items = next;
  }

  #send(item: T) {
    this.#queue = this.#queue.then(async () => {
      try {
        const stored = item.deletedAt
          ? await this.api.remove(item.id, item.updatedAt)
          : await this.api.save(item);
        this.#mergeIn([stored]);
        if (this.#pending.get(item.id) === item) this.#pending.delete(item.id);
      } catch {
        // Keep it pending; the next refresh() resends it.
      }
    });
  }
}
