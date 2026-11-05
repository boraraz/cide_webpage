import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { AuthPanel } from '@/features/auth/AuthPanel';
import { sessionQueryKey } from '@/features/auth/useSession';
import { auth } from '@/lib/api';
import { isApiError } from '@commerce/api-client';
import { PageLoading } from '@/components/PageLoading';

export default function VerifyEmailPage() {
  const [params] = useSearchParams();
  const token = params.get('token');
  const queryClient = useQueryClient();
  const started = useRef(false);
  const [status, setStatus] = useState<'pending' | 'done' | 'error'>(token ? 'pending' : 'error');
  const [message, setMessage] = useState(token ? '' : 'This verification link is missing a token.');

  useEffect(() => {
    if (!token || started.current) return;
    started.current = true;
    void auth
      .confirmEmailVerification(token)
      .then(async () => {
        await queryClient.invalidateQueries({ queryKey: sessionQueryKey });
        setStatus('done');
      })
      .catch((error: unknown) => {
        setStatus('error');
        setMessage(
          isApiError(error) ? (error.detail ?? error.title) : 'The API could not be reached.',
        );
      });
  }, [queryClient, token]);

  return (
    <AuthPanel title="Verify email">
      {status === 'pending' ? <PageLoading /> : null}
      {status === 'done' ? (
        <p role="status" className="text-sm">
          Email verified.
        </p>
      ) : null}
      {status === 'error' ? <p className="text-sm text-danger">{message}</p> : null}
      <p className="mt-6 text-sm">
        <Link to="/account" className="underline underline-offset-4">
          Go to your account
        </Link>
      </p>
    </AuthPanel>
  );
}
