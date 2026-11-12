import { isApiError } from '@commerce/api-client';
import { Container } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { Navigate, useParams } from 'react-router';
import { PageLoading } from '@/components/PageLoading';
import { orderStatusLabel, reservationCopy } from '@/features/orders/order-copy';
import { useSession } from '@/features/auth/useSession';
import { commerce } from '@/lib/api';
import { formatTry } from '@/lib/money';

export default function OrderDetailPage() {
  const { reference = '' } = useParams();
  const session = useSession();
  const order = useQuery({
    queryKey: ['order', reference],
    queryFn: () => commerce.order(reference),
    enabled: Boolean(session.data) && reference.length > 0,
    retry: false,
  });
  if (session.isPending) return <PageLoading />;
  if (!session.data)
    return <Navigate to="/login" replace state={{ from: `/account/orders/${reference}` }} />;
  if (isApiError(order.error) && order.error.status === 404) {
    return (
      <Container className="py-16">
        <h1 className="font-display text-4xl font-light">Order</h1>
        <p className="mt-6">That order was not found.</p>
      </Container>
    );
  }
  if (!order.data) return <Container className="py-16">Loading order</Container>;
  const current = order.data;
  return (
    <Container className="py-10">
      <h1 className="font-display text-4xl font-light">{current.reference}</h1>
      <p className="mt-4">{orderStatusLabel(current.status)}</p>
      <p className="mt-2">
        {current.paymentCollected
          ? 'Payment has been collected.'
          : 'No payment has been collected.'}
      </p>
      <p className="mt-2">{reservationCopy(current.reservationMinutes, current.expiresAt)}</p>
      <p className="mt-4">Total {formatTry(current.grandTotal)}</p>
      <ul className="mt-6 flex flex-col gap-2">
        {current.items.map((item) => (
          <li key={item.variantId}>
            {item.productName} × {item.quantity}
          </li>
        ))}
      </ul>
    </Container>
  );
}
