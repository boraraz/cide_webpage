import { cx } from '../lib/cx';

export interface SpinnerProps {
  label?: string;
  className?: string;
}

export function Spinner({ label = 'Loading', className }: SpinnerProps) {
  return (
    <span role="status" className={cx('inline-flex items-center gap-2', className)}>
      <svg
        aria-hidden="true"
        viewBox="0 0 24 24"
        className="size-5 animate-spin text-muted-foreground"
        fill="none"
      >
        <circle cx="12" cy="12" r="10" stroke="currentColor" strokeOpacity="0.25" strokeWidth="2" />
        <path d="M22 12a10 10 0 0 0-10-10" stroke="currentColor" strokeWidth="2" />
      </svg>
      <span className="sr-only">{label}</span>
    </span>
  );
}
