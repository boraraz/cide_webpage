import { Button, TextField } from '@commerce/ui';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useParams } from 'react-router';
import { orderStatusLabel, reservationCopy } from '@/features/orders/order-copy';
import { commerce } from '@/lib/api';
import { formatTry } from '@/lib/money';
import { problemMessage } from '@/lib/problem';

export default function OrderDetailPage() {
  const { orderId = '' } = useParams();
  const queryClient = useQueryClient();
  const [message, setMessage] = useState<string>();
  const order = useQuery({
    queryKey: ['admin-order', orderId],
    queryFn: () => commerce.adminOrder(orderId),
    enabled: orderId.length > 0,
  });
  const cancel = useMutation({
    mutationFn: (reason: string) => commerce.cancelOrder(orderId, reason),
    onSuccess: async (next) => {
      setMessage('The order was cancelled and the reservation was released.');
      queryClient.setQueryData(['admin-order', orderId], next);
      await queryClient.invalidateQueries({ queryKey: ['admin-orders'] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });
  if (!order.data) return <p>Loading order</p>;
  const current = order.data;
  return (
    <div className="flex max-w-3xl flex-col gap-4">
      <h1 className="text-xl font-semibold sm:text-2xl">{current.reference}</h1>
      <p>{orderStatusLabel(current.status)}</p>
      <p>
        {current.paymentCollected
          ? 'Payment has been collected.'
          : 'No payment has been collected.'}
      </p>
      <p>{reservationCopy(current.reservationMinutes, current.expiresAt)}</p>
      <p>
        Merchandise {formatTry(current.merchandiseTotal)} · Shipping{' '}
        {formatTry(current.shippingTotal)} · Total {formatTry(current.grandTotal)}
      </p>
      <ul>
        {current.items.map((item) => (
          <li key={item.variantId}>
            {item.productName} · {item.sku} × {item.quantity} · {formatTry(item.lineTotal)}
          </li>
        ))}
      </ul>
      <section>
        <h2 className="text-lg font-semibold">Status history</h2>
        <ol className="mt-2 flex flex-col gap-2">
          {current.history.map((entry) => (
            <li key={`${entry.createdAt}-${entry.toStatus}`}>
              {entry.fromStatus ? `${orderStatusLabel(entry.fromStatus)} → ` : ''}
              {orderStatusLabel(entry.toStatus)}
              {entry.reason ? ` · ${entry.reason}` : ''}
            </li>
          ))}
        </ol>
      </section>
      {message ? <p role="status">{message}</p> : null}
      {current.status === 'paymentpending' ? (
        <form
          className="flex flex-col gap-3 border border-border p-4"
          onSubmit={(event) => {
            event.preventDefault();
            const data = new FormData(event.currentTarget);
            cancel.mutate(String(data.get('reason') ?? ''));
          }}
        >
          <TextField name="reason" label="Cancellation reason" required />
          <Button type="submit" loading={cancel.isPending}>
            Cancel order
          </Button>
        </form>
      ) : null}
    </div>
  );
}
