import { isApiError, type Order } from '@commerce/api-client';
import { Button, Container } from '@commerce/ui';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import { orderStatusLabel, reservationCopy } from '@/features/orders/order-copy';
import { commerce } from '@/lib/api';
import { formatTry } from '@/lib/money';

export default function OrderConfirmationPage() {
  const token = useRef<string | null>(null);
  const [order, setOrder] = useState<Order | null>(null);
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
    return () => {
      const current = document.querySelector('meta[name="referrer"]');
      if (!current) return;
      if (previous) current.setAttribute('content', previous);
      else if (!meta) current.remove();
    };
  }, []);

  useEffect(() => {
    const fragment = window.location.hash.replace(/^#/, '');
    if (fragment) {
      token.current = decodeURIComponent(fragment);
      const path = `${window.location.pathname}${window.location.search}`;
      window.history.replaceState(window.history.state, '', path);
    }
    void load(token.current, setOrder, setError, setLoading);
  }, []);

  return (
    <Container className="py-10">
      <h1 className="font-display text-4xl font-light">Order confirmation</h1>
      {loading ? <p className="mt-6">Loading order</p> : null}
      {error ? <p className="mt-6">{error}</p> : null}
      {order ? (
        <OrderView
          order={order}
          onRefresh={() => {
            setLoading(true);
            void load(token.current, setOrder, setError, setLoading);
          }}
        />
      ) : null}
      <p className="mt-8 text-sm">
        <Link to="/account/orders" className="underline">
          Order history
        </Link>{' '}
        is available after you sign in with the same account.
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
  accessToken: string | null,
  setOrder: (order: Order | null) => void,
  setError: (message: string | undefined) => void,
  setLoading: (loading: boolean) => void,
) {
  if (!accessToken) {
    setOrder(null);
    setError(
      'Open the confirmation from the checkout page. The access link is not stored in this browser.',
    );
    setLoading(false);
    return;
  }
  try {
    setOrder(await commerce.orderByAccess(accessToken));
    setError(undefined);
  } catch (error) {
    setOrder(null);
    setError(
      isApiError(error) ? 'This order link is not valid.' : 'The order could not be loaded.',
    );
  } finally {
    setLoading(false);
  }
}
