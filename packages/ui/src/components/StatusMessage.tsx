import type { ReactNode } from 'react';
import { cx } from '../lib/cx';

export interface StatusMessageProps {
  title: string;
  description?: ReactNode;
  eyebrow?: string;
  action?: ReactNode;
  tone?: 'neutral' | 'danger';
  className?: string;
}

/** Centered message used for loading, error, empty and not-found states. */
export function StatusMessage({
  title,
  description,
  eyebrow,
  action,
  tone = 'neutral',
  className,
}: StatusMessageProps) {
  return (
    <section
      className={cx(
        'mx-auto flex max-w-md flex-col items-center gap-3 px-6 py-16 text-center',
        className,
      )}
    >
      {eyebrow ? (
        <p className="text-xs tracking-editorial text-muted-foreground uppercase">{eyebrow}</p>
      ) : null}
      <h1
        className={cx(
          'text-2xl font-medium',
          tone === 'danger' ? 'text-danger' : 'text-foreground',
        )}
      >
        {title}
      </h1>
      {description ? <div className="text-sm text-muted-foreground">{description}</div> : null}
      {action ? <div className="mt-4">{action}</div> : null}
    </section>
  );
}
