import { StatusMessage } from '@commerce/ui';
import { isRouteErrorResponse, useRouteError } from 'react-router';

export function RouteErrorPage() {
  const error = useRouteError();
  const status = isRouteErrorResponse(error) ? error.status : undefined;

  return (
    <StatusMessage
      tone="danger"
      eyebrow={status ? String(status) : 'Error'}
      title="Something went wrong"
      description="Please try again. If the problem persists, come back later."
      action={
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="bg-accent px-5 py-2.5 text-xs tracking-editorial text-accent-foreground uppercase"
        >
          Reload
        </button>
      }
      className="min-h-dvh justify-center"
    />
  );
}
