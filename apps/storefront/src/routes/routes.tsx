import { lazy } from 'react';
import type { RouteObject } from 'react-router';
import { PublicLayout } from '@/components/layout/PublicLayout';
import { NotFoundPage } from '@/pages/NotFoundPage';
import { RouteErrorPage } from '@/pages/RouteErrorPage';

const HomePage = lazy(() => import('@/pages/HomePage'));
const ShopPage = lazy(() => import('@/pages/ShopPage'));
const CategoryPage = lazy(() => import('@/pages/CategoryPage'));
const CollectionPage = lazy(() => import('@/pages/CollectionPage'));
const ProductPage = lazy(() => import('@/pages/ProductPage'));
const SearchPage = lazy(() => import('@/pages/SearchPage'));
const LoginPage = lazy(() => import('@/pages/LoginPage'));
const RegisterPage = lazy(() => import('@/pages/RegisterPage'));
const ForgotPasswordPage = lazy(() => import('@/pages/ForgotPasswordPage'));
const ResetPasswordPage = lazy(() => import('@/pages/ResetPasswordPage'));
const VerifyEmailPage = lazy(() => import('@/pages/VerifyEmailPage'));
const AccountPage = lazy(() => import('@/pages/AccountPage'));
const CartPage = lazy(() => import('@/pages/CartPage'));
const CheckoutPage = lazy(() => import('@/pages/CheckoutPage'));
const OrderConfirmationPage = lazy(() => import('@/pages/OrderConfirmationPage'));
const OrdersPage = lazy(() => import('@/pages/OrdersPage'));
const OrderDetailPage = lazy(() => import('@/pages/OrderDetailPage'));

/*
 * Future routes (product listing, product detail, cart, checkout) are added
 * as children of the public layout.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    element: <PublicLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'shop', element: <ShopPage /> },
      { path: 'category/:slug', element: <CategoryPage /> },
      { path: 'collection/:slug', element: <CollectionPage /> },
      { path: 'product/:slug', element: <ProductPage /> },
      { path: 'search', element: <SearchPage /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      { path: 'forgot-password', element: <ForgotPasswordPage /> },
      { path: 'reset-password', element: <ResetPasswordPage /> },
      { path: 'verify-email', element: <VerifyEmailPage /> },
      { path: 'account', element: <AccountPage /> },
      { path: 'account/orders', element: <OrdersPage /> },
      { path: 'account/orders/:reference', element: <OrderDetailPage /> },
      { path: 'cart', element: <CartPage /> },
      { path: 'checkout', element: <CheckoutPage /> },
      { path: 'order/confirmation', element: <OrderConfirmationPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
