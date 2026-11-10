import { useRef } from 'react';

export function createIdempotencyKey(): string {
  return `idem-${crypto.randomUUID().replaceAll('-', '')}`;
}

/** Keeps one key for an action until that action succeeds. */
export function useIdempotencyKey() {
  const key = useRef<string | null>(null);
  return {
    current: () => {
      key.current ??= createIdempotencyKey();
      return key.current;
    },
    clear: () => {
      key.current = null;
    },
  };
}
