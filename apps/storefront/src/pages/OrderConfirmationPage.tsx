import { isApiError, type Order } from '@commerce/api-client';
import { Button, Container } from '@commerce/ui';
import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { orderStatusLabel, reservationCopy } from '@/features/orders/order-copy';
import { commerce } from '@/lib/api';
import { formatTry } from '@/lib/money';

export default function OrderConfirmationPage() {
  const [orders, setOrders] = useState<Order[]>([]);
  const [recoveryHours, setRecoveryHours] = useState(2);
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const meta = document.querySelector('meta[name="referrer"]');
    const previous = meta?.getAttribute('content') ?? null;
    if (meta) meta.setAttribute('content', 'no-referrer');
    else {
      const created = document.createElement('meta');
      created.name = 'referrer';
      created.content = 'no-referrer';
      document.head.append(created);
    }
    if (window.location.hash) {
      window.history.replaceState(
        window.history.state,
        '',
        `${window.location.pathname}${window.location.search}`,
      );
    }
    return () => {
      const current = document.querySelector('meta[name="referrer"]');
      if (!current) return;
      if (previous) current.setAttribute('content', previous);
      else if (!meta) current.remove();
    };
  }, []);

  useEffect(() => {
    void load(setOrders, setRecoveryHours, setError, setLoading);
  }, []);

  return (
    <Container className="py-10">
      <h1 className="font-display text-4xl font-light">Order confirmation</h1>
      <p className="mt-4 max-w-prose text-sm">
        This browser can reopen {orders.length === 1 ? 'this order' : 'these orders'} for{' '}
        {recoveryHours} hours, including after a refresh or in another tab. After that, the recovery
        link is gone. Signing in does not attach a guest order by email.
      </p>
      {loading ? <p className="mt-6">Loading order</p> : null}
      {error ? <p className="mt-6">{error}</p> : null}
      {orders.map((order) => (
        <OrderView
          key={order.id}
          order={order}
          onRefresh={() => {
            setLoading(true);
            void load(setOrders, setRecoveryHours, setError, setLoading);
          }}
        />
      ))}
      <p className="mt-8 text-sm">
        <Link to="/account/orders" className="underline">
          Order history
        </Link>{' '}
        is available for orders placed while signed in.
      </p>
    </Container>
  );
}

function OrderView({ order, onRefresh }: { order: Order; onRefresh: () => void }) {
  return (
    <div className="mt-8 max-w-prose">
      <p>Reference {order.reference}</p>
      <p className="mt-2">{orderStatusLabel(order.status)}</p>
      <p className="mt-2">
        {order.paymentCollected ? 'Payment has been collected.' : 'No payment has been collected.'}
      </p>
      <p className="mt-2">{reservationCopy(order.reservationMinutes, order.expiresAt)}</p>
      <p className="mt-4">Total {formatTry(order.grandTotal)}</p>
      <ul className="mt-4 flex flex-col gap-2">
        {order.items.map((item) => (
          <li key={item.variantId}>
            {item.productName} × {item.quantity} · {formatTry(item.lineTotal)}
          </li>
        ))}
      </ul>
      <Button className="mt-6" type="button" variant="secondary" onClick={onRefresh}>
        Check reservation
      </Button>
    </div>
  );
}

async function load(
  setOrders: (orders: Order[]) => void,
  setRecoveryHours: (hours: number) => void,
  setError: (message: string | undefined) => void,
  setLoading: (loading: boolean) => void,
) {
  try {
    const page = await commerce.confirmation();
    setOrders(page.orders);
    setRecoveryHours(page.recoveryHours);
    setError(page.orders.length === 0 ? 'This browser cannot reopen an order.' : undefined);
  } catch (error) {
    setOrders([]);
    setError(
      isApiError(error)
        ? 'This browser cannot reopen an order. The 2-hour recovery window may have ended.'
        : 'The order could not be loaded.',
    );
  } finally {
    setLoading(false);
  }
}
