import { cx, Spinner } from '@commerce/ui';
import { useApiReadiness } from './useApiReadiness';

function toneFor(status: string) {
  return status.toLowerCase() === 'healthy' ? 'bg-success' : 'bg-danger';
}

/** Live API and dependency readiness, read from GET /api/v1/health/ready. */
export function SystemStatusCard() {
  const { data, isPending, isError } = useApiReadiness();

  return (
    <section
      aria-labelledby="system-status-title"
      className="min-w-0 rounded-lg border border-border bg-background p-4 shadow-sm sm:p-5"
    >
      <h2 id="system-status-title" className="text-sm font-medium">
        System status
      </h2>

      <div className="mt-4" aria-live="polite">
        {isPending ? (
          <Spinner label="Checking API status" />
        ) : isError ? (
          <p className="text-sm text-danger">API unreachable. Is the backend running?</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            <li className="flex items-center justify-between gap-4">
              <span>API</span>
              <StatusBadge status={data.status} />
            </li>
            {data.checks.map((check) => (
              <li key={check.name} className="flex items-center justify-between gap-4">
                <span className="truncate">{check.name}</span>
                <StatusBadge status={check.status} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

function StatusBadge({ status }: { status: string }) {
  return (
    <span className="inline-flex items-center gap-2 text-muted-foreground">
      <span aria-hidden="true" className={cx('size-2 rounded-full', toneFor(status))} />
      {status}
    </span>
  );
}
