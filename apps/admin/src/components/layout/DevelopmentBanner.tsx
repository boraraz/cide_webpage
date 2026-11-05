import { TriangleAlert } from 'lucide-react';

/** Authentication exists. The app is still a development tool and must not be deployed publicly. */
export function DevelopmentBanner() {
  return (
    <div
      role="note"
      className="flex items-start gap-2 bg-warning px-4 py-2 text-xs text-white sm:items-center sm:text-sm"
    >
      <TriangleAlert aria-hidden="true" className="size-4 shrink-0" />
      <p>
        Development only: sign-in is required, and this admin must not be deployed as a public site.
      </p>
    </div>
  );
}
