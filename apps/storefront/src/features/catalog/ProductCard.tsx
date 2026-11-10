import type { PublicCatalogCard } from '@commerce/api-client';
import { Link } from 'react-router';
import { discountLabel, formatTry } from '@/lib/money';
import { CatalogImage } from './CatalogImage';

export function ProductCard({ product }: { product: PublicCatalogCard }) {
  const discount = discountLabel(product.fromPrice, product.compareAtPrice);
  return (
    <article>
      <Link to={`/product/${product.slug}`} className="group block">
        <div className="aspect-[3/4] overflow-hidden bg-surface">
          <CatalogImage
            sources={product.primarySources}
            alt={product.primaryAltText ?? product.name}
            className="h-full w-full object-cover transition duration-300 group-hover:scale-[1.02] motion-reduce:transition-none motion-reduce:group-hover:scale-100"
          />
        </div>
        <div className="mt-3 flex items-start justify-between gap-3">
          <h2 className="text-sm">{product.name}</h2>
          {product.isFeatured ? (
            <span className="text-[11px] tracking-editorial text-muted-foreground uppercase">
              Featured
            </span>
          ) : null}
        </div>
        <p className="mt-1 text-sm">
          <span>{formatTry(product.fromPrice)}</span>
          {product.compareAtPrice ? (
            <span className="ml-2 text-muted-foreground line-through">
              {formatTry(product.compareAtPrice)}
            </span>
          ) : null}
          {discount ? <span className="ml-2 text-muted-foreground">{discount}</span> : null}
        </p>
        {product.colors.length > 0 ? (
          <p className="mt-1 text-xs text-muted-foreground">{product.colors.join(' · ')}</p>
        ) : null}
        {product.available ? null : (
          <p className="mt-1 text-xs tracking-editorial uppercase">Sold out</p>
        )}
      </Link>
    </article>
  );
}
