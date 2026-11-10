import { useQuery } from '@tanstack/react-query';
import { catalog } from '@/lib/api';
import { SystemStatusCard } from '@/features/system-status/SystemStatusCard';

const cards = [
  ['Total products', 'totalProducts'],
  ['Published', 'publishedProducts'],
  ['Drafts', 'draftProducts'],
  ['Archived', 'archivedProducts'],
  ['Active variants', 'activeVariants'],
  ['Out of stock', 'outOfStockVariants'],
  ['Low stock', 'lowStockVariants'],
  ['Categories', 'categories'],
  ['Collections', 'collections'],
] as const;

export default function DashboardPage() {
  const summary = useQuery({ queryKey: ['dashboard'], queryFn: () => catalog.dashboard() });

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold sm:text-2xl">Dashboard</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Catalogue counts from the database. Low stock is available quantity from 1 through{' '}
          {summary.data?.lowStockThreshold ?? 5}.
        </p>
      </div>
      {summary.isError ? <p role="alert">The catalogue summary could not be loaded.</p> : null}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {cards.map(([label, key]) => (
          <article key={key} className="border border-border bg-background p-4">
            <h2 className="text-sm text-muted-foreground">{label}</h2>
            <p className="mt-2 text-2xl font-semibold">{summary.data ? summary.data[key] : '—'}</p>
          </article>
        ))}
        <SystemStatusCard />
      </div>
    </div>
  );
}
