import { Drawer, SkipLink } from '@commerce/ui';
import { Suspense, useState } from 'react';
import { Outlet, ScrollRestoration } from 'react-router';
import { PageLoading } from '@/components/PageLoading';
import { AdminHeader } from './AdminHeader';
import { DevelopmentBanner } from './DevelopmentBanner';
import { SidebarNav } from './SidebarNav';

export function AdminLayout() {
  const [navOpen, setNavOpen] = useState(false);

  return (
    <div className="flex min-h-dvh flex-col">
      <SkipLink />
      <DevelopmentBanner />
      <div className="flex min-w-0 flex-1">
        <aside className="hidden w-64 shrink-0 border-r border-border bg-surface lg:block">
          <div className="sticky top-0 max-h-dvh overflow-y-auto">
            <p className="flex h-14 items-center border-b border-border px-6 text-sm font-semibold tracking-editorial uppercase">
              Sportswear
            </p>
            <SidebarNav />
          </div>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          <AdminHeader menuOpen={navOpen} onOpenMenu={() => setNavOpen(true)} />
          <main id="main" className="min-w-0 flex-1 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
            <Suspense fallback={<PageLoading />}>
              <Outlet />
            </Suspense>
          </main>
        </div>
      </div>

      <Drawer open={navOpen} onClose={() => setNavOpen(false)} title="Navigation">
        <div id="admin-mobile-nav">
          <SidebarNav label="Admin (mobile)" onNavigate={() => setNavOpen(false)} />
        </div>
      </Drawer>
      <ScrollRestoration />
    </div>
  );
}
