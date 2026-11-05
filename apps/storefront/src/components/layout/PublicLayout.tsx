import { SkipLink } from '@commerce/ui';
import { Suspense } from 'react';
import { Outlet, ScrollRestoration } from 'react-router';
import { PageLoading } from '@/components/PageLoading';
import { Footer } from './Footer';
import { Header } from './Header';

export function PublicLayout() {
  return (
    <div className="flex min-h-dvh flex-col">
      <SkipLink />
      <Header />
      <main id="main" className="min-w-0 flex-1">
        <Suspense fallback={<PageLoading />}>
          <Outlet />
        </Suspense>
      </main>
      <Footer />
      <ScrollRestoration />
    </div>
  );
}
