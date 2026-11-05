import { cx } from '@commerce/ui';
import { useApiReadiness } from './useApiReadiness';

/** Development-only indicator backed by the real readiness endpoint. */
export function ApiStatusIndicator() {
  const { data, isPending, isError } = useApiReadiness();

  const state = isPending ? 'checking' : isError ? 'unreachable' : data.status.toLowerCase();
  const tone = state === 'healthy' ? 'bg-success' : state === 'checking' ? 'bg-muted' : 'bg-danger';

  return (
    <p className="inline-flex items-center gap-2" aria-live="polite">
      <span aria-hidden="true" className={cx('size-2 rounded-full', tone)} />
      <span>API {state}</span>
    </p>
  );
}
