import { lazy } from 'react';
import type { RouteObject } from 'react-router';
import { AdminLayout } from '@/components/layout/AdminLayout';
import { AdminLoginPage } from '@/features/auth/AdminLoginPage';
import { RequireAdmin } from '@/features/auth/RequireAdmin';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { RouteErrorPage } from '@/pages/RouteErrorPage';

const DashboardPage = lazy(() => import('@/pages/DashboardPage'));

/* Module routes are added here, and to `navigation.ts`, as each admin module is implemented. */
export const routes: RouteObject[] = [
  { path: '/login', element: <AdminLoginPage /> },
  {
    element: <RequireAdmin />,
    errorElement: <RouteErrorPage />,
    children: [
      {
        path: '/',
        element: <AdminLayout />,
        children: [
          { index: true, element: <DashboardPage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
];
