import { zodResolver } from '@hookform/resolvers/zod';
import { Button, TextField } from '@commerce/ui';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, Navigate, useLocation, useNavigate } from 'react-router';
import type { z } from 'zod';
import { AuthPanel } from '@/features/auth/AuthPanel';
import { FormAlert } from '@/features/auth/FormAlert';
import { applyFieldErrors } from '@/features/auth/form-errors';
import { loginSchema } from '@/features/auth/schemas';
import { useSession, useSessionCache } from '@/features/auth/useSession';
import { auth } from '@/lib/api';
import { PageLoading } from '@/components/PageLoading';

type LoginValues = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const session = useSession();
  const cache = useSessionCache();
  const navigate = useNavigate();
  const location = useLocation();
  const [formError, setFormError] = useState<string>();
  const notice = readNotice(location.state);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  if (session.isPending) return <PageLoading />;
  if (session.data) return <Navigate to="/account" replace />;

  return (
    <AuthPanel title="Sign in" description="Use the email and password for your account.">
      <form
        className="flex flex-col gap-4"
        noValidate
        onSubmit={handleSubmit(async (values) => {
          setFormError(undefined);
          try {
            const user = await auth.login(values);
            cache.remember(user);
            const from = readFrom(location.state);
            await navigate(from ?? '/account', { replace: true });
          } catch (error) {
            applyFieldErrors(error, setError, setFormError);
          }
        })}
      >
        {notice ? <p className="text-sm text-muted-foreground">{notice}</p> : null}
        <FormAlert message={formError} />
        <TextField
          label="Email"
          type="email"
          autoComplete="email"
          {...register('email')}
          error={errors.email?.message}
        />
        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          {...register('password')}
          error={errors.password?.message}
        />
        <Button type="submit" fullWidth loading={isSubmitting}>
          Sign in
        </Button>
      </form>
      <p className="mt-6 text-sm">
        <Link to="/forgot-password" className="underline underline-offset-4">
          Forgot password
        </Link>
      </p>
      <p className="mt-3 text-sm text-muted-foreground">
        New here?{' '}
        <Link to="/register" className="text-foreground underline underline-offset-4">
          Create an account
        </Link>
      </p>
    </AuthPanel>
  );
}

function readNotice(state: unknown): string | undefined {
  if (typeof state !== 'object' || state === null || !('notice' in state)) return undefined;
  return typeof state.notice === 'string' ? state.notice : undefined;
}

function readFrom(state: unknown): string | undefined {
  if (typeof state !== 'object' || state === null || !('from' in state)) return undefined;
  return typeof state.from === 'string' ? state.from : undefined;
}
