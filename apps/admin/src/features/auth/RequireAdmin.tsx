import { Navigate, Outlet } from 'react-router';
import { PageLoading } from '@/components/PageLoading';
import { AccessDeniedPage } from './AccessDeniedPage';
import { adminFailureStatus, useAdminSession } from './useAdminSession';

export function RequireAdmin() {
  const session = useAdminSession();

  if (session.isPending) return <PageLoading />;
  if (adminFailureStatus(session.error) === 403) return <AccessDeniedPage />;
  if (!session.data) return <Navigate to="/login" replace />;
  return <Outlet />;
}
