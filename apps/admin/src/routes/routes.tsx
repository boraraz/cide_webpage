import { lazy } from 'react';
import type { RouteObject } from 'react-router';
import { AdminLayout } from '@/components/layout/AdminLayout';
import { AdminLoginPage } from '@/features/auth/AdminLoginPage';
import { RequireAdmin } from '@/features/auth/RequireAdmin';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { RouteErrorPage } from '@/pages/RouteErrorPage';

const DashboardPage = lazy(() => import('@/pages/DashboardPage'));
const ProductsPage = lazy(() => import('@/pages/ProductsPage'));
const ProductEditorPage = lazy(() => import('@/pages/ProductEditorPage'));
const CategoriesPage = lazy(() => import('@/pages/CategoriesPage'));
const CollectionsPage = lazy(() => import('@/pages/CollectionsPage'));
const InventoryPage = lazy(() => import('@/pages/InventoryPage'));
const MovementsPage = lazy(() => import('@/pages/MovementsPage'));
const HomepagePage = lazy(() => import('@/pages/HomepagePage'));
const HomepagePreviewPage = lazy(() => import('@/pages/HomepagePreviewPage'));
const OrdersPage = lazy(() => import('@/pages/OrdersPage'));
const OrderDetailPage = lazy(() => import('@/pages/OrderDetailPage'));

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
          { path: 'products', element: <ProductsPage /> },
          { path: 'products/:id', element: <ProductEditorPage /> },
          { path: 'categories', element: <CategoriesPage /> },
          { path: 'collections', element: <CollectionsPage /> },
          { path: 'inventory', element: <InventoryPage /> },
          { path: 'inventory/movements', element: <MovementsPage /> },
          { path: 'content/homepage', element: <HomepagePage /> },
          { path: 'content/homepage/preview', element: <HomepagePreviewPage /> },
          { path: 'orders', element: <OrdersPage /> },
          { path: 'orders/:orderId', element: <OrderDetailPage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
];
