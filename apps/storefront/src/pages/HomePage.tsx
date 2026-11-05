import { Container } from '@commerce/ui';

export default function HomePage() {
  return (
    <Container className="flex min-h-[60svh] flex-col justify-end pt-12 pb-12 md:min-h-[70svh] md:pb-16">
      <p className="text-xs tracking-editorial text-muted-foreground uppercase">Storefront</p>
      <h1 className="mt-3 max-w-3xl font-display text-3xl font-light text-balance sm:text-4xl lg:text-6xl">
        The home page is built from configurable content blocks.
      </h1>
      <p className="mt-4 max-w-xl text-sm text-muted-foreground sm:text-base">
        This placeholder will be replaced by the block renderer once the homepage editor exists.
      </p>
    </Container>
  );
}
