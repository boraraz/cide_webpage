import { zodResolver } from '@hookform/resolvers/zod';
import { Button, TextField } from '@commerce/ui';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate, useSearchParams } from 'react-router';
import type { z } from 'zod';
import { AuthPanel } from '@/features/auth/AuthPanel';
import { FormAlert } from '@/features/auth/FormAlert';
import { applyFieldErrors } from '@/features/auth/form-errors';
import { resetPasswordSchema } from '@/features/auth/schemas';
import { useSessionCache } from '@/features/auth/useSession';
import { auth } from '@/lib/api';

type ResetValues = z.infer<typeof resetPasswordSchema>;

export default function ResetPasswordPage() {
  const [params] = useSearchParams();
  const token = params.get('token');
  const navigate = useNavigate();
  const cache = useSessionCache();
  const [formError, setFormError] = useState<string>();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ResetValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { password: '', passwordConfirmation: '' },
  });

  return (
    <AuthPanel title="Choose a new password">
      {token ? (
        <form
          className="flex flex-col gap-4"
          noValidate
          onSubmit={handleSubmit(async (values) => {
            setFormError(undefined);
            try {
              await auth.confirmPasswordReset({
                token,
                password: values.password,
                passwordConfirmation: values.passwordConfirmation,
              });
              cache.clear();
              await navigate('/login', {
                replace: true,
                state: { notice: 'Password updated. Sign in with the new password.' },
              });
            } catch (error) {
              applyFieldErrors(error, setError, setFormError);
            }
          })}
        >
          <FormAlert message={formError} />
          <TextField
            label="New password"
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
            Update password
          </Button>
        </form>
      ) : (
        <p className="text-sm text-muted-foreground">This reset link is missing a token.</p>
      )}
      <p className="mt-6 text-sm">
        <Link to="/login" className="underline underline-offset-4">
          Back to sign in
        </Link>
      </p>
    </AuthPanel>
  );
}
