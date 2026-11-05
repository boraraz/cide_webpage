import { StatusMessage } from '@commerce/ui';
import { Link } from 'react-router';

export function NotFoundPage() {
  return (
    <StatusMessage
      eyebrow="404"
      title="Page not found"
      description="The page you are looking for does not exist or has moved."
      action={
        <Link to="/" className="text-sm underline underline-offset-4">
          Back to home
        </Link>
      }
    />
  );
}
