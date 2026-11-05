import { zodResolver } from '@hookform/resolvers/zod';
import { Button, TextField } from '@commerce/ui';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router';
import type { z } from 'zod';
import { AuthPanel } from '@/features/auth/AuthPanel';
import { FormAlert } from '@/features/auth/FormAlert';
import { applyFieldErrors } from '@/features/auth/form-errors';
import { forgotPasswordSchema } from '@/features/auth/schemas';
import { auth } from '@/lib/api';

type ForgotValues = z.infer<typeof forgotPasswordSchema>;

export default function ForgotPasswordPage() {
  const [formError, setFormError] = useState<string>();
  const [sent, setSent] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ForgotValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
  });

  return (
    <AuthPanel
      title="Reset password"
      description="If an account exists for that email, we will send a link. The response is the same either way."
    >
      {sent ? (
        <p role="status" className="text-sm">
          If an account exists for that email, a reset link is on its way.
        </p>
      ) : (
        <form
          className="flex flex-col gap-4"
          noValidate
          onSubmit={handleSubmit(async (values) => {
            setFormError(undefined);
            try {
              await auth.requestPasswordReset(values.email);
              setSent(true);
            } catch (error) {
              applyFieldErrors(error, setError, setFormError);
            }
          })}
        >
          <FormAlert message={formError} />
          <TextField
            label="Email"
            type="email"
            autoComplete="email"
            {...register('email')}
            error={errors.email?.message}
          />
          <Button type="submit" fullWidth loading={isSubmitting}>
            Send reset link
          </Button>
        </form>
      )}
      <p className="mt-6 text-sm">
        <Link to="/login" className="underline underline-offset-4">
          Back to sign in
        </Link>
      </p>
    </AuthPanel>
  );
}
