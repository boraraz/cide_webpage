import { Menu } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useAdminSessionCache } from '@/features/auth/useAdminSession';

export function AdminHeader({
  menuOpen,
  onOpenMenu,
}: {
  menuOpen: boolean;
  onOpenMenu: () => void;
}) {
  const cache = useAdminSessionCache();
  const navigate = useNavigate();
  const [error, setError] = useState<string>();

  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b border-border bg-background px-2 sm:px-4 lg:px-8">
      <button
        type="button"
        onClick={onOpenMenu}
        aria-expanded={menuOpen}
        aria-controls="admin-mobile-nav"
        className="inline-flex size-11 items-center justify-center lg:hidden"
      >
        <Menu aria-hidden="true" className="size-5" />
        <span className="sr-only">Open navigation</span>
      </button>
      <p className="truncate text-sm font-semibold tracking-editorial uppercase">Admin</p>
      <button
        type="button"
        className="ml-auto inline-flex min-h-11 items-center px-3 text-sm"
        onClick={() => {
          setError(undefined);
          void cache.signOut().then(
            () => navigate('/login', { replace: true }),
            () => setError('The API could not be reached.'),
          );
        }}
      >
        Sign out
      </button>
      {error ? <p className="text-xs text-danger">{error}</p> : null}
    </header>
  );
}
