import { getContext, setContext } from "svelte";

/**
 * Create a typed Svelte context pair. Returns `provide` (stores a value) and
 * `use` (retrieves it, throwing if missing).
 */
export function createContext<T>(name: string): {
  provide: (value: T) => T;
  use: () => T;
} {
  const key = Symbol(name);
  return {
    provide(value: T): T {
      setContext(key, value);
      return value;
    },
    use(): T {
      const ctx = getContext<T | undefined>(key);
      if (!ctx) throw new Error(`${name} context is missing — call provide() in a parent component`);
      return ctx;
    },
  };
}

/**
 * Variant that returns a fallback instead of throwing when context is absent.
 */
export function createOptionalContext<T>(name: string, fallback: T): {
  provide: (value: T) => T;
  use: () => T;
} {
  const key = Symbol(name);
  return {
    provide(value: T): T {
      setContext(key, value);
      return value;
    },
    use(): T {
      return getContext<T | undefined>(key) ?? fallback;
    },
  };
}
