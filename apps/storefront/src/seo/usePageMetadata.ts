import { useEffect } from 'react';
import { applyMetadata } from './applyMetadata';
import type { PageMetadata } from './metadata';

export function usePageMetadata(metadata: PageMetadata | null): void {
  useEffect(() => {
    if (!metadata) return;
    applyMetadata(metadata, window.location.origin);
  }, [metadata]);
}
