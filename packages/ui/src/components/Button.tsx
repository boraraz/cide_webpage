import { forwardRef, type ButtonHTMLAttributes } from 'react';
import { cx } from '../lib/cx';

type ButtonVariant = 'primary' | 'secondary' | 'ghost';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  fullWidth?: boolean;
  loading?: boolean;
}

const variants: Record<ButtonVariant, string> = {
  primary:
    'bg-accent text-accent-foreground hover:opacity-90 disabled:bg-muted disabled:text-muted-foreground',
  secondary: 'border border-border bg-background hover:bg-surface disabled:text-muted-foreground',
  ghost: 'bg-transparent hover:bg-surface disabled:text-muted-foreground',
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  {
    variant = 'primary',
    fullWidth = false,
    loading = false,
    className,
    children,
    disabled,
    type = 'button',
    ...rest
  },
  ref,
) {
  return (
    <button
      ref={ref}
      type={type}
      aria-busy={loading || undefined}
      disabled={disabled || loading}
      className={cx(
        'inline-flex min-h-11 items-center justify-center px-4 text-sm font-medium',
        variants[variant],
        fullWidth && 'w-full',
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  );
});
