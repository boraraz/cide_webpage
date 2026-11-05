import type { ReactNode } from 'react';
import { Container } from '@commerce/ui';

export function AuthPanel({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <Container className="py-8 sm:py-12">
      <div className="mx-auto w-full max-w-md">
        <h1 className="font-display text-3xl font-light">{title}</h1>
        {description ? <p className="mt-2 text-sm text-muted-foreground">{description}</p> : null}
        <div className="mt-8">{children}</div>
      </div>
    </Container>
  );
}
