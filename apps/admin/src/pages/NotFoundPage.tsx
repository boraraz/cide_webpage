import { StatusMessage } from '@commerce/ui';
import { Link } from 'react-router';

export function NotFoundPage() {
  return (
    <StatusMessage
      eyebrow="404"
      title="Page not found"
      description="This admin page does not exist."
      action={
        <Link
          to="/"
          className="inline-flex min-h-11 items-center text-sm underline underline-offset-4"
        >
          Back to dashboard
        </Link>
      }
    />
  );
}
