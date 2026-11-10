export interface PageMetadata {
  title: string;
  description: string;
  canonicalPath: string;
  image?: string | null;
  jsonLd?: Record<string, unknown> | null;
}

export function pageMetadata(input: PageMetadata): PageMetadata {
  return {
    title: input.title,
    description: input.description,
    canonicalPath: input.canonicalPath,
    image: input.image ?? null,
    jsonLd: input.jsonLd ?? null,
  };
}

export function productJsonLd(input: {
  name: string;
  description: string;
  sku: string;
  price: string;
  currency: string;
  path: string;
  image?: string | null;
  available: boolean;
}): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: input.name,
    description: input.description,
    sku: input.sku,
    image: input.image ?? undefined,
    offers: {
      '@type': 'Offer',
      price: input.price,
      priceCurrency: input.currency,
      availability: input.available
        ? 'https://schema.org/InStock'
        : 'https://schema.org/OutOfStock',
      url: input.path,
    },
  };
}
