import { Button, StatusMessage } from '@commerce/ui';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useAdminSessionCache } from './useAdminSession';

export function AccessDeniedPage() {
  const cache = useAdminSessionCache();
  const navigate = useNavigate();
  const [error, setError] = useState<string>();

  return (
    <StatusMessage
      eyebrow="403"
      title="Access denied"
      description="This account cannot open the admin. Sign out, then sign in with an administrator account."
      action={
        <div className="flex flex-col items-center gap-3">
          {error ? <p className="text-sm text-danger">{error}</p> : null}
          <Button
            onClick={() => {
              setError(undefined);
              void cache.signOut().then(
                () => navigate('/login', { replace: true }),
                () => setError('The API could not be reached.'),
              );
            }}
          >
            Sign out
          </Button>
        </div>
      }
    />
  );
}
