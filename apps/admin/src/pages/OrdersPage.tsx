import { Button } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { orderStatusLabel } from '@/features/orders/order-copy';
import { commerce } from '@/lib/api';
import { formatTry } from '@/lib/money';

export default function OrdersPage() {
  const [status, setStatus] = useState('');
  const [reference, setReference] = useState('');
  const orders = useQuery({
    queryKey: ['admin-orders', status, reference],
    queryFn: () =>
      commerce.adminOrders({
        status: status || undefined,
        reference: reference || undefined,
      }),
  });

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold sm:text-2xl">Orders</h1>
      {orders.data ? (
        <section className="border border-border p-4 text-sm" aria-label="Shipping">
          <p>Shipping fee {formatTry(orders.data.shippingFee)}</p>
          <p>Free shipping from {formatTry(orders.data.freeShippingThreshold)}</p>
          <p>Pending payment reservation {orders.data.pendingPaymentMinutes} minutes</p>
          <p className="mt-2 text-muted-foreground">
            These values are read from the server. This screen cannot change them or mark an order
            paid.
          </p>
        </section>
      ) : null}
      <form
        className="flex flex-wrap gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          const data = new FormData(event.currentTarget);
          setStatus(String(data.get('status') ?? ''));
          setReference(String(data.get('reference') ?? '').trim());
        }}
      >
        <label className="flex flex-col gap-1 text-sm">
          Status
          <select name="status" className="min-h-11 border border-border px-3" defaultValue="">
            <option value="">All</option>
            <option value="paymentpending">Payment pending</option>
            <option value="cancelled">Cancelled</option>
            <option value="expired">Expired</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Reference
          <input name="reference" className="min-h-11 border border-border px-3" />
        </label>
        <Button type="submit" className="self-end">
          Filter
        </Button>
      </form>
      <div className="overflow-x-auto border border-border">
        <table className="w-full min-w-[40rem] text-left text-sm">
          <thead>
            <tr>
              <th className="p-3">Reference</th>
              <th className="p-3">Status</th>
              <th className="p-3">Email</th>
              <th className="p-3">Total</th>
            </tr>
          </thead>
          <tbody>
            {orders.data?.items.map((order) => (
              <tr key={order.id} className="border-t border-border">
                <td className="p-3">
                  <Link to={`/orders/${order.id}`} className="underline">
                    {order.reference}
                  </Link>
                </td>
                <td className="p-3">{orderStatusLabel(order.status)}</td>
                <td className="p-3">{order.email}</td>
                <td className="p-3">{formatTry(order.grandTotal)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {orders.data && orders.data.items.length === 0 ? <p>No orders match.</p> : null}
    </div>
  );
}
