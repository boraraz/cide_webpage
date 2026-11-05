import { zodResolver } from '@hookform/resolvers/zod';
import { Button, TextField } from '@commerce/ui';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, Navigate, useNavigate } from 'react-router';
import type { z } from 'zod';
import { PageLoading } from '@/components/PageLoading';
import { AuthPanel } from '@/features/auth/AuthPanel';
import { FormAlert } from '@/features/auth/FormAlert';
import { applyFieldErrors } from '@/features/auth/form-errors';
import { registerSchema } from '@/features/auth/schemas';
import { useSession, useSessionCache } from '@/features/auth/useSession';
import { auth } from '@/lib/api';

type RegisterValues = z.infer<typeof registerSchema>;

export default function RegisterPage() {
  const session = useSession();
  const cache = useSessionCache();
  const navigate = useNavigate();
  const [formError, setFormError] = useState<string>();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      firstName: '',
      lastName: '',
      email: '',
      password: '',
      passwordConfirmation: '',
    },
  });

  if (session.isPending) return <PageLoading />;
  if (session.data) return <Navigate to="/account" replace />;

  return (
    <AuthPanel
      title="Create account"
      description="Registration is for customers. It does not grant admin access."
    >
      <form
        className="flex flex-col gap-4"
        noValidate
        onSubmit={handleSubmit(async (values) => {
          setFormError(undefined);
          try {
            const user = await auth.register(values);
            cache.remember(user);
            await navigate('/account', { replace: true });
          } catch (error) {
            applyFieldErrors(error, setError, setFormError);
          }
        })}
      >
        <FormAlert message={formError} />
        <TextField
          label="First name"
          autoComplete="given-name"
          {...register('firstName')}
          error={errors.firstName?.message}
        />
        <TextField
          label="Last name"
          autoComplete="family-name"
          {...register('lastName')}
          error={errors.lastName?.message}
        />
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
          autoComplete="new-password"
          hint="At least 10 characters, with a letter and a number."
          {...register('password')}
          error={errors.password?.message}
        />
        <TextField
          label="Confirm password"
          type="password"
          autoComplete="new-password"
          {...register('passwordConfirmation')}
          error={errors.passwordConfirmation?.message}
        />
        <Button type="submit" fullWidth loading={isSubmitting}>
          Create account
        </Button>
      </form>
      <p className="mt-6 text-sm text-muted-foreground">
        Already registered?{' '}
        <Link to="/login" className="text-foreground underline underline-offset-4">
          Sign in
        </Link>
      </p>
    </AuthPanel>
  );
}
