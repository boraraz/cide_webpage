export interface MediaSource {
  url: string;
  width?: number | null;
  contentType?: string | null;
  mimeType?: string | null;
}

export interface HomepageBlockView {
  id: string;
  type: string;
  config: Record<string, unknown>;
}

export interface HomepageLinks {
  product?: (slug: string) => string;
  collection?: (slug: string) => string;
}

export interface HomepageViewProps {
  blocks: HomepageBlockView[];
  issues?: Array<{ blockId: string; message: string }>;
  links?: HomepageLinks;
  resolveSrc?: (url: string) => string;
}

export function HomepageView({ blocks, issues = [], links, resolveSrc }: HomepageViewProps) {
  return (
    <div className="flex flex-col">
      {blocks.map((block) => (
        <section key={block.id} aria-label={label(block)} className="border-border border-b">
          <Block block={block} links={links} resolveSrc={resolveSrc} />
          {issues
            .filter((issue) => issue.blockId === block.id)
            .map((issue) => (
              <p key={issue.message} role="alert" className="text-danger px-4 py-2 text-sm">
                {issue.message}
              </p>
            ))}
        </section>
      ))}
    </div>
  );
}

function Block({
  block,
  links,
  resolveSrc,
}: {
  block: HomepageBlockView;
  links?: HomepageLinks;
  resolveSrc?: (url: string) => string;
}) {
  const heading = text(block.config.heading);
  const body = text(block.config.body);
  if (block.type === 'spacer') {
    const size = block.config.size === 'lg' ? 'h-24' : block.config.size === 'sm' ? 'h-8' : 'h-16';
    return <div className={size} />;
  }

  const image = (
    <BlockImage config={block.config} priority={block.type === 'hero'} resolveSrc={resolveSrc} />
  );
  const destination = text(block.config.destination);
  if (block.type === 'hero') {
    return (
      <div className="bg-surface relative min-h-[70svh]">
        <div className="absolute inset-0">{image}</div>
        <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/55 to-transparent p-6 text-white sm:p-12">
          {heading ? (
            <h1 className="font-display max-w-3xl text-4xl font-light text-balance sm:text-6xl">
              {heading}
            </h1>
          ) : null}
          {body ? <p className="mt-4 max-w-xl text-sm sm:text-base">{body}</p> : null}
          {destination ? (
            <a
              className="tracking-editorial mt-6 inline-flex min-h-11 items-center text-sm uppercase"
              href={destination}
            >
              Continue
            </a>
          ) : null}
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-12 sm:px-6 lg:px-8">
      {heading ? <h2 className="font-display text-3xl font-light sm:text-4xl">{heading}</h2> : null}
      {body ? (
        <p className="text-muted-foreground max-w-2xl text-sm leading-6 sm:text-base">{body}</p>
      ) : null}
      {image}
      <ProductList config={block.config} links={links} />
      <CollectionLine config={block.config} links={links} />
    </div>
  );
}

function BlockImage({
  config,
  priority,
  resolveSrc,
}: {
  config: Record<string, unknown>;
  priority: boolean;
  resolveSrc?: (url: string) => string;
}) {
  const sources = Array.isArray(config.sources) ? (config.sources as MediaSource[]) : [];
  const usable = sources.filter((source) => source.url && (source.width ?? 0) > 0);
  const first = sources.find((source) => source.url);
  if (!first) return null;
  const shared = {
    src: resolveSrc ? resolveSrc(first.url) : first.url,
    alt: text(config.altText) ?? '',
    width: first.width ?? undefined,
    height: undefined,
    loading: priority ? ('eager' as const) : ('lazy' as const),
    className: 'h-full w-full object-cover',
  };
  if (usable.length < 2) return <img {...shared} />;
  return (
    <img
      {...shared}
      srcSet={usable
        .map((source) => `${resolveSrc ? resolveSrc(source.url) : source.url} ${source.width}w`)
        .join(', ')}
      sizes={priority ? '100vw' : '(min-width: 1024px) 960px, 100vw'}
    />
  );
}

function ProductList({
  config,
  links,
}: {
  config: Record<string, unknown>;
  links?: HomepageLinks;
}) {
  const products = Array.isArray(config.products) ? config.products : [];
  if (products.length === 0) return null;
  return (
    <ul className="grid grid-cols-2 gap-x-4 gap-y-8 md:grid-cols-3 xl:grid-cols-4">
      {products.map((product) => {
        const item = product as { id?: string; name?: string; slug?: string };
        const href = item.slug && links?.product ? links.product(item.slug) : undefined;
        return (
          <li key={item.id}>
            {href ? (
              <a className="inline-flex min-h-11 items-center text-sm" href={href}>
                {item.name}
              </a>
            ) : (
              <span className="text-sm">{item.name}</span>
            )}
          </li>
        );
      })}
    </ul>
  );
}

function CollectionLine({
  config,
  links,
}: {
  config: Record<string, unknown>;
  links?: HomepageLinks;
}) {
  const collection = config.collection as { name?: string; slug?: string } | undefined;
  if (!collection?.name) return null;
  const href = collection.slug && links?.collection ? links.collection(collection.slug) : undefined;
  return href ? (
    <a
      className="tracking-editorial inline-flex min-h-11 items-center text-sm uppercase"
      href={href}
    >
      {collection.name}
    </a>
  ) : (
    <p className="tracking-editorial text-sm uppercase">{collection.name}</p>
  );
}

function label(block: HomepageBlockView) {
  return text(block.config.heading) ?? block.type;
}

function text(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}
