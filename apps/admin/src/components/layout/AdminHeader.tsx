import { Menu } from 'lucide-react';

export function AdminHeader({
  menuOpen,
  onOpenMenu,
}: {
  menuOpen: boolean;
  onOpenMenu: () => void;
}) {
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
    </header>
  );
}
