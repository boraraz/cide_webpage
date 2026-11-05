import { z } from 'zod';

const email = z
  .string()
  .trim()
  .min(1, 'Enter your email.')
  .refine((value) => z.email().safeParse(value).success, 'Enter a valid email.');

const password = z
  .string()
  .min(10, 'Use at least 10 characters.')
  .max(128, 'Use at most 128 characters.')
  .refine(
    (value) => /\p{L}/u.test(value) && /\d/.test(value),
    'Include at least one letter and one number.',
  );

function personName(label: string) {
  return z
    .string()
    .trim()
    .min(1, `Enter a ${label} of 1 to 100 characters.`)
    .max(100, `Enter a ${label} of 1 to 100 characters.`);
}

function passwordsMatch<
  T extends { password: string; passwordConfirmation: string; email?: string },
>(value: T, context: z.RefinementCtx) {
  if (value.email && value.password.toLowerCase() === value.email.trim().toLowerCase()) {
    context.addIssue({
      code: 'custom',
      path: ['password'],
      message: 'The password must not match the email address.',
    });
  }
  if (value.password !== value.passwordConfirmation) {
    context.addIssue({
      code: 'custom',
      path: ['passwordConfirmation'],
      message: 'The passwords do not match.',
    });
  }
}

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Enter your password.'),
});

export const registerSchema = z
  .object({
    firstName: personName('first name'),
    lastName: personName('last name'),
    email,
    password,
    passwordConfirmation: z.string(),
  })
  .superRefine(passwordsMatch);

export const forgotPasswordSchema = z.object({ email });

export const resetPasswordSchema = z
  .object({
    password,
    passwordConfirmation: z.string(),
  })
  .superRefine(passwordsMatch);

export const profileSchema = z.object({
  firstName: personName('first name'),
  lastName: personName('last name'),
  phoneNumber: z.string().refine((value) => {
    const trimmed = value.trim();
    return trimmed.length === 0 || isTurkishPhone(trimmed);
  }, 'Enter a Türkiye phone number, for example +90 532 000 00 00.'),
});

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.'),
    password,
    passwordConfirmation: z.string(),
  })
  .superRefine(passwordsMatch);

function isTurkishPhone(value: string): boolean {
  const compact = value.replace(/[^\d+]/g, '');
  if (compact.startsWith('+90')) return /^\d{10}$/.test(compact.slice(3));
  if (compact.startsWith('90') && compact.length === 12) return true;
  if (compact.startsWith('0') && compact.length === 11) return true;
  return /^\d{10}$/.test(compact);
}
