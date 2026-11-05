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
      description="An unexpected error occurred while rendering this page."
      action={
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="min-h-11 rounded-md bg-accent px-5 text-sm text-accent-foreground"
        >
          Reload
        </button>
      }
      className="min-h-dvh justify-center"
    />
  );
}
