import { HomepageView } from '@commerce/content';
import { Container } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { content } from '@/lib/api';
import { env } from '@/lib/env';
import { absoluteMediaUrl } from '@/lib/media';
import { pageMetadata } from '@/seo/metadata';
import { usePageMetadata } from '@/seo/usePageMetadata';

export default function HomePage() {
  const homepage = useQuery({ queryKey: ['homepage'], queryFn: () => content.publicHomepage() });
  const blocks = homepage.data?.blocks ?? [];
  usePageMetadata(
    pageMetadata({
      title: 'Sportswear',
      description: 'Published homepage.',
      canonicalPath: '/',
    }),
  );
  if (!homepage.data && homepage.isPending) {
    return <Container className="py-16">Loading</Container>;
  }
  if (homepage.isError) {
    return (
      <Container className="py-16">
        <p role="alert">The homepage could not be loaded.</p>
      </Container>
    );
  }
  if (homepage.data && blocks.length === 0) {
    return (
      <Container className="flex min-h-[60svh] flex-col justify-end py-16">
        <p className="text-xs tracking-editorial text-muted-foreground uppercase">Homepage</p>
        <h1 className="mt-3 max-w-3xl font-display text-4xl font-light sm:text-6xl">
          Nothing is published yet.
        </h1>
        <p className="mt-4 max-w-xl text-sm text-muted-foreground">
          Published homepage blocks will appear here.
        </p>
      </Container>
    );
  }
  return (
    <HomepageView
      blocks={blocks}
      links={{ product: (slug) => `/product/${slug}`, collection: (slug) => `/collection/${slug}` }}
      resolveSrc={(url) => absoluteMediaUrl(url, env.VITE_API_BASE_URL)}
    />
  );
}
