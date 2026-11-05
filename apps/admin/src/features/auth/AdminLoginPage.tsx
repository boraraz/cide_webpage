import { zodResolver } from '@hookform/resolvers/zod';
import { Button, TextField } from '@commerce/ui';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate } from 'react-router';
import { z } from 'zod';
import { PageLoading } from '@/components/PageLoading';
import { applyFieldErrors } from './form-errors';
import { useAdminSession, useAdminSessionCache } from './useAdminSession';
import { auth } from '@/lib/api';

const loginSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, 'Enter your email.')
    .refine((value) => z.email().safeParse(value).success, 'Enter a valid email.'),
  password: z.string().min(1, 'Enter your password.'),
});

type LoginValues = z.infer<typeof loginSchema>;

export function AdminLoginPage() {
  const session = useAdminSession();
  const cache = useAdminSessionCache();
  const [formError, setFormError] = useState<string>();
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
  if (session.data) return <Navigate to="/" replace />;

  return (
    <main
      id="main"
      className="mx-auto flex min-h-dvh w-full max-w-md flex-col justify-center px-4 py-8"
    >
      <p className="text-xs tracking-editorial text-muted-foreground uppercase">Admin</p>
      <h1 className="mt-3 font-display text-3xl font-light">Sign in</h1>
      <p className="mt-2 text-sm text-muted-foreground">
        Development only. This admin is not a public site.
      </p>
      <form
        className="mt-8 flex flex-col gap-4"
        noValidate
        onSubmit={handleSubmit(async (values) => {
          setFormError(undefined);
          try {
            cache.remember(await auth.adminLogin(values));
          } catch (error) {
            applyFieldErrors(error, setError, setFormError);
          }
        })}
      >
        {formError ? (
          <p role="alert" className="text-sm text-danger">
            {formError}
          </p>
        ) : null}
        <TextField
          label="Email"
          type="email"
          autoComplete="username"
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
    </main>
  );
}
