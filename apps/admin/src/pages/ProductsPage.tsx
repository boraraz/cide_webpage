import { useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router';
import { Button } from '@commerce/ui';
import { catalog } from '@/lib/api';
import { formatTry } from '@/lib/money';

export default function ProductsPage() {
  const [params, setParams] = useSearchParams();
  const q = params.get('q') ?? '';
  const status = params.get('status') ?? '';
  const products = useQuery({
    queryKey: ['products', q, status],
    queryFn: () =>
      catalog.adminProducts({
        q: q || undefined,
        status: status || undefined,
        pageSize: 24,
      }),
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold sm:text-2xl">Products</h1>
          <p className="text-sm text-muted-foreground">
            Drafts stay off the public catalogue until you publish them.
          </p>
        </div>
        <Link
          to="/products/new"
          className="inline-flex min-h-11 items-center bg-accent px-4 text-sm text-accent-foreground"
        >
          New product
        </Link>
      </div>
      <form
        className="flex flex-wrap gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          const data = new FormData(event.currentTarget);
          setParams({ q: String(data.get('q') ?? ''), status: String(data.get('status') ?? '') });
        }}
      >
        <input
          name="q"
          defaultValue={q}
          aria-label="Search products"
          className="min-h-11 flex-1 border border-border px-3"
        />
        <select
          name="status"
          defaultValue={status}
          aria-label="Status"
          className="min-h-11 border border-border px-3"
        >
          <option value="">All statuses</option>
          <option value="draft">Draft</option>
          <option value="active">Published</option>
          <option value="archived">Archived</option>
        </select>
        <Button type="submit">Search</Button>
      </form>
      {products.isError ? <p role="alert">Products could not be loaded.</p> : null}
      {products.data && products.data.items.length === 0 ? (
        <p>No products match this search.</p>
      ) : null}
      <div className="overflow-x-auto border border-border">
        <table className="w-full min-w-[48rem] text-left text-sm">
          <thead className="bg-surface">
            <tr>
              <th className="p-3">Name</th>
              <th className="p-3">SKU</th>
              <th className="p-3">Price</th>
              <th className="p-3">Available</th>
              <th className="p-3">Status</th>
            </tr>
          </thead>
          <tbody>
            {products.data?.items.map((item) => {
              const card = item as typeof item & {
                skuSummary?: string | null;
                minPrice?: string | null;
                available?: number;
                version: number;
              };
              return (
                <tr key={card.id} className="border-t border-border">
                  <td className="p-3">
                    <Link to={`/products/${card.id}`}>{card.name}</Link>
                  </td>
                  <td className="p-3">{card.skuSummary ?? '—'}</td>
                  <td className="p-3">{card.minPrice ? formatTry(card.minPrice) : '—'}</td>
                  <td className="p-3">{card.available ?? '—'}</td>
                  <td className="p-3">{card.status}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
