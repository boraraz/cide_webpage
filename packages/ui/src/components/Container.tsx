import type { ReactNode } from 'react';
import { cx } from '../lib/cx';

export interface ContainerProps {
  children: ReactNode;
  className?: string;
}

/** Page-width wrapper with mobile-first responsive gutters. */
export function Container({ children, className }: ContainerProps) {
  return (
    <div className={cx('mx-auto w-full max-w-screen-2xl px-4 sm:px-6 lg:px-8', className)}>
      {children}
    </div>
  );
}
