import { Spinner } from '@commerce/ui';

export function PageLoading() {
  return (
    <div className="flex min-h-[40vh] items-center justify-center">
      <Spinner label="Loading page" />
    </div>
  );
}
