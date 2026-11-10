export interface MediaSource {
  url: string;
  width?: number | null;
  contentType?: string | null;
}

export interface HomepageBlockView {
  id: string;
  type: string;
  config: Record<string, unknown>;
}

export interface HomepageViewProps {
  blocks: HomepageBlockView[];
  issues?: Array<{ blockId: string; message: string }>;
}

export function HomepageView({ blocks, issues = [] }: HomepageViewProps) {
  return (
    <div className="flex flex-col gap-8">
      {blocks.map((block) => (
        <section key={block.id} aria-label={block.type}>
          <Block block={block} />
          {issues
            .filter((issue) => issue.blockId === block.id)
            .map((issue) => (
              <p key={issue.message} role="alert" className="mt-2 text-sm text-red-700">
                {issue.message}
              </p>
            ))}
        </section>
      ))}
    </div>
  );
}

function Block({ block }: { block: HomepageBlockView }) {
  const heading = text(block.config.heading);
  const body = text(block.config.body);
  if (block.type === 'spacer') {
    const size = block.config.size === 'lg' ? 'h-24' : block.config.size === 'sm' ? 'h-6' : 'h-12';
    return <div className={size} />;
  }

  return (
    <div className="flex flex-col gap-3">
      {heading ? <h2 className="text-2xl font-semibold">{heading}</h2> : null}
      {body ? <p>{body}</p> : null}
      <BlockImage config={block.config} />
      <ProductList config={block.config} />
      <CollectionLine config={block.config} />
    </div>
  );
}

function BlockImage({ config }: { config: Record<string, unknown> }) {
  const sources = Array.isArray(config.sources) ? (config.sources as MediaSource[]) : [];
  const first = sources[0];
  if (!first) return null;
  if (sources.length > 1) {
    return (
      <img
        src={first.url}
        alt={text(config.altText) ?? ''}
        srcSet={sources.map((source) => `${source.url} ${source.width ?? ''}w`).join(', ')}
      />
    );
  }
  return <img src={first.url} alt={text(config.altText) ?? ''} />;
}

function ProductList({ config }: { config: Record<string, unknown> }) {
  const products = Array.isArray(config.products) ? config.products : [];
  if (products.length === 0) return null;
  return (
    <ul>
      {products.map((product) => {
        const item = product as { id?: string; name?: string; slug?: string };
        return <li key={item.id}>{item.name}</li>;
      })}
    </ul>
  );
}

function CollectionLine({ config }: { config: Record<string, unknown> }) {
  const collection = config.collection as { name?: string } | undefined;
  return collection?.name ? <p>{collection.name}</p> : null;
}

function text(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}
