import { lazy } from 'react';
import type { RouteObject } from 'react-router';
import { PublicLayout } from '@/components/layout/PublicLayout';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { RouteErrorPage } from '@/pages/RouteErrorPage';

const HomePage = lazy(() => import('@/pages/HomePage'));

/*
 * Future routes (product listing, product detail, cart, checkout, account) are added
 * as children of the public layout.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    element: <PublicLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
