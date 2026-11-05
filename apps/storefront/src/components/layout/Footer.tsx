import { Container } from '@commerce/ui';
import { ApiStatusIndicator } from '@/features/system-status/ApiStatusIndicator';

export function Footer() {
  return (
    <footer className="border-t border-border pb-[env(safe-area-inset-bottom)]">
      <Container className="flex flex-col gap-4 py-8 text-sm text-muted-foreground md:flex-row md:items-center md:justify-between">
        <p>&copy; {new Date().getFullYear()} Sportswear</p>
        {import.meta.env.DEV ? <ApiStatusIndicator /> : null}
      </Container>
    </footer>
  );
}
