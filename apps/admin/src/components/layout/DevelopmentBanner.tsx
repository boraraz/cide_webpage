import { TriangleAlert } from 'lucide-react';

/** Admin authentication arrives in Phase 2; until then this app must never be deployed. */
export function DevelopmentBanner() {
  return (
    <div
      role="note"
      className="flex items-start gap-2 bg-warning px-4 py-2 text-xs text-white sm:items-center sm:text-sm"
    >
      <TriangleAlert aria-hidden="true" className="size-4 shrink-0" />
      <p>Development only: the admin has no authentication yet and must not be deployed.</p>
    </div>
  );
}
