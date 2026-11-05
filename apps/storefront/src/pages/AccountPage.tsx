import { zodResolver } from '@hookform/resolvers/zod';
import { Button, TextField } from '@commerce/ui';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate, useNavigate } from 'react-router';
import type { z } from 'zod';
import { PageLoading } from '@/components/PageLoading';
import { AuthPanel } from '@/features/auth/AuthPanel';
import { FormAlert } from '@/features/auth/FormAlert';
import { applyFieldErrors } from '@/features/auth/form-errors';
import { changePasswordSchema, profileSchema } from '@/features/auth/schemas';
import { useSession, useSessionCache } from '@/features/auth/useSession';
import { auth } from '@/lib/api';

type ProfileValues = z.infer<typeof profileSchema>;
type PasswordValues = z.infer<typeof changePasswordSchema>;

export default function AccountPage() {
  const session = useSession();
  if (session.isPending) return <PageLoading />;
  if (!session.data) return <Navigate to="/login" replace state={{ from: '/account' }} />;
  return <AccountForm user={session.data} />;
}

function AccountForm({ user }: { user: NonNullable<ReturnType<typeof useSession>['data']> }) {
  const cache = useSessionCache();
  const navigate = useNavigate();
  const [profileError, setProfileError] = useState<string>();
  const [profileSaved, setProfileSaved] = useState(false);
  const [passwordError, setPasswordError] = useState<string>();
  const [signOutError, setSignOutError] = useState<string>();
  const [verificationNote, setVerificationNote] = useState<string>();
  const [resending, setResending] = useState(false);
  const profileForm = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      firstName: user.firstName,
      lastName: user.lastName,
      phoneNumber: user.phoneNumber ?? '',
    },
  });
  const passwordForm = useForm<PasswordValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', password: '', passwordConfirmation: '' },
  });

  return (
    <AuthPanel title="Account" description={user.email}>
      {user.emailVerified ? (
        <p className="mb-6 text-sm text-muted-foreground">Email verified.</p>
      ) : (
        <div className="mb-6 border border-border bg-surface p-4 text-sm">
          <p>Your email is not verified yet. You can still use your account.</p>
          <Button
            variant="secondary"
            className="mt-3"
            loading={resending}
            onClick={() => {
              setResending(true);
              setVerificationNote(undefined);
              void auth
                .requestEmailVerification()
                .then(() => {
                  setVerificationNote('If the address can receive mail, a new link is on its way.');
                })
                .catch((error: unknown) => {
                  applyFieldErrors(error, profileForm.setError, setVerificationNote);
                })
                .finally(() => setResending(false));
            }}
          >
            Resend verification email
          </Button>
          {verificationNote ? (
            <p className="mt-3 text-muted-foreground">{verificationNote}</p>
          ) : null}
        </div>
      )}

      <form
        className="flex flex-col gap-4"
        noValidate
        onSubmit={profileForm.handleSubmit(async (values) => {
          setProfileError(undefined);
          setProfileSaved(false);
          try {
            const updated = await auth.updateProfile({
              firstName: values.firstName,
              lastName: values.lastName,
              phoneNumber: values.phoneNumber.trim() === '' ? null : values.phoneNumber.trim(),
            });
            cache.remember(updated);
            setProfileSaved(true);
          } catch (error) {
            applyFieldErrors(error, profileForm.setError, setProfileError);
          }
        })}
      >
        <h2 className="text-lg font-medium">Profile</h2>
        <FormAlert message={profileError} />
        {profileSaved ? (
          <p role="status" className="text-sm text-muted-foreground">
            Profile saved.
          </p>
        ) : null}
        <TextField
          label="First name"
          autoComplete="given-name"
          {...profileForm.register('firstName')}
          error={profileForm.formState.errors.firstName?.message}
        />
        <TextField
          label="Last name"
          autoComplete="family-name"
          {...profileForm.register('lastName')}
          error={profileForm.formState.errors.lastName?.message}
        />
        <TextField
          label="Phone"
          type="tel"
          autoComplete="tel"
          hint="Optional. A Türkiye number, or leave blank to clear it."
          {...profileForm.register('phoneNumber')}
          error={profileForm.formState.errors.phoneNumber?.message}
        />
        <p className="text-sm text-muted-foreground">Email cannot be changed here.</p>
        <Button type="submit" loading={profileForm.formState.isSubmitting}>
          Save profile
        </Button>
      </form>

      <form
        className="mt-10 flex flex-col gap-4 border-t border-border pt-8"
        noValidate
        onSubmit={passwordForm.handleSubmit(async (values) => {
          setPasswordError(undefined);
          try {
            await auth.changePassword({
              currentPassword: values.currentPassword,
              newPassword: values.password,
              passwordConfirmation: values.passwordConfirmation,
            });
            cache.clear();
            await navigate('/login', {
              replace: true,
              state: { notice: 'Password updated. Sign in again.' },
            });
          } catch (error) {
            applyFieldErrors(error, passwordForm.setError, setPasswordError);
          }
        })}
      >
        <h2 className="text-lg font-medium">Password</h2>
        <FormAlert message={passwordError} />
        <TextField
          label="Current password"
          type="password"
          autoComplete="current-password"
          {...passwordForm.register('currentPassword')}
          error={passwordForm.formState.errors.currentPassword?.message}
        />
        <TextField
          label="New password"
          type="password"
          autoComplete="new-password"
          {...passwordForm.register('password')}
          error={passwordForm.formState.errors.password?.message}
        />
        <TextField
          label="Confirm password"
          type="password"
          autoComplete="new-password"
          {...passwordForm.register('passwordConfirmation')}
          error={passwordForm.formState.errors.passwordConfirmation?.message}
        />
        <Button type="submit" variant="secondary" loading={passwordForm.formState.isSubmitting}>
          Change password
        </Button>
      </form>

      <div className="mt-10 border-t border-border pt-8">
        <FormAlert message={signOutError} />
        <Button
          variant="ghost"
          onClick={() => {
            setSignOutError(undefined);
            void cache.signOut().then(
              () => navigate('/', { replace: true }),
              () => setSignOutError('The API could not be reached.'),
            );
          }}
        >
          Sign out
        </Button>
      </div>
    </AuthPanel>
  );
}
