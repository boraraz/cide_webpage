import { SystemStatusCard } from '@/features/system-status/SystemStatusCard';

export default function DashboardPage() {
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold sm:text-2xl">Dashboard</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Sales and catalogue metrics will appear here once the commerce modules exist.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
        <SystemStatusCard />
      </div>
    </div>
  );
}
