import { isApiError } from '@commerce/api-client';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

export function applyFieldErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  setFormError: (message: string) => void,
) {
  if (!isApiError(error)) {
    setFormError('The API could not be reached.');
    return;
  }

  const entries = Object.entries(error.validationErrors ?? {});
  if (entries.length > 0) {
    for (const [field, messages] of entries) {
      const message = messages[0];
      if (message) setError(field as Path<T>, { message });
    }
    return;
  }

  setFormError(error.detail ?? error.title);
}
