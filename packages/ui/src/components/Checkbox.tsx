import { forwardRef, useId, type InputHTMLAttributes } from 'react';
import { cx } from '../lib/cx';

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: string;
}

export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(function Checkbox(
  { label, className, id, ...rest },
  ref,
) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;
  return (
    <label htmlFor={fieldId} className={cx('flex min-h-11 items-center gap-2 text-sm', className)}>
      <input ref={ref} id={fieldId} type="checkbox" className="size-4" {...rest} />
      {label}
    </label>
  );
});
