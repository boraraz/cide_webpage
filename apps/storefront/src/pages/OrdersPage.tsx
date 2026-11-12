import { Container } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { Link, Navigate } from 'react-router';
import { PageLoading } from '@/components/PageLoading';
import { orderStatusLabel } from '@/features/orders/order-copy';
import { useSession } from '@/features/auth/useSession';
import { commerce } from '@/lib/api';
import { formatTry } from '@/lib/money';

export default function OrdersPage() {
  const session = useSession();
  const orders = useQuery({
    queryKey: ['orders'],
    queryFn: () => commerce.orders(),
    enabled: Boolean(session.data),
  });
  if (session.isPending) return <PageLoading />;
  if (!session.data) return <Navigate to="/login" replace state={{ from: '/account/orders' }} />;
  return (
    <Container className="py-10">
      <h1 className="font-display text-4xl font-light">Orders</h1>
      {orders.isPending ? <p className="mt-6">Loading orders</p> : null}
      {orders.data && orders.data.length === 0 ? (
        <p className="mt-6">You have no orders yet.</p>
      ) : null}
      <ul className="mt-8 flex flex-col gap-4">
        {orders.data?.map((order) => (
          <li key={order.id} className="border border-border p-4">
            <Link to={`/account/orders/${order.reference}`} className="underline">
              {order.reference}
            </Link>
            <p className="mt-2 text-sm">
              {orderStatusLabel(order.status)} · {formatTry(order.grandTotal)}
            </p>
          </li>
        ))}
      </ul>
    </Container>
  );
}
