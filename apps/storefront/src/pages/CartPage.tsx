import { isApiError } from '@commerce/api-client';
import { Button, Container } from '@commerce/ui';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router';
import { commerce } from '@/lib/api';
import { formatTry } from '@/lib/money';

export default function CartPage() {
  const queryClient = useQueryClient();
  const cart = useQuery({ queryKey: ['cart'], queryFn: () => commerce.cart() });
  const change = useMutation({
    mutationFn: (input: { id: string; quantity: number } | { id: string; remove: true }) =>
      'remove' in input
        ? commerce.removeItem(input.id)
        : commerce.updateItem(input.id, input.quantity),
    onSuccess: async (next) => {
      queryClient.setQueryData(['cart'], next);
    },
  });

  if (cart.isPending) return <Container className="py-16">Loading bag</Container>;
  if (cart.isError) {
    return (
      <Container className="py-16">
        <p>{isApiError(cart.error) ? cart.error.title : 'The bag could not be loaded.'}</p>
      </Container>
    );
  }

  const items = cart.data.items;
  return (
    <Container className="py-10">
      <h1 className="font-display text-4xl font-light">Bag</h1>
      {items.length === 0 ? (
        <div className="mt-8">
          <p>Your bag is empty.</p>
          <Link to="/shop" className="mt-4 inline-flex min-h-11 items-center underline">
            Continue shopping
          </Link>
        </div>
      ) : (
        <div className="mt-8 grid gap-10 lg:grid-cols-[minmax(0,1fr)_18rem]">
          <ul className="flex flex-col gap-6">
            {items.map((item) => (
              <li key={item.id} className="border-b border-border pb-6">
                <p className="text-lg">{item.productName}</p>
                <p className="text-sm text-muted-foreground">
                  {item.options.map((option) => option.value).join(' / ')} · {item.sku}
                </p>
                <p className="mt-2">{formatTry(item.lineTotal)}</p>
                {item.unavailableReason ? (
                  <p className="mt-2 text-sm">{item.unavailableReason}</p>
                ) : null}
                {item.quantityLimited ? (
                  <p className="mt-2 text-sm">Quantity was limited to the available amount.</p>
                ) : null}
                <div className="mt-3 flex flex-wrap items-center gap-3">
                  <label className="text-sm" htmlFor={`qty-${item.id}`}>
                    Quantity
                  </label>
                  <input
                    id={`qty-${item.id}`}
                    className="min-h-11 w-20 border border-border px-3"
                    inputMode="numeric"
                    defaultValue={item.quantity}
                    key={`${item.id}-${item.quantity}`}
                    onBlur={(event) => {
                      const quantity = Number(event.target.value);
                      if (quantity !== item.quantity && quantity >= 1) {
                        change.mutate({ id: item.id, quantity });
                      }
                    }}
                  />
                  <Button
                    variant="secondary"
                    type="button"
                    onClick={() => change.mutate({ id: item.id, remove: true })}
                  >
                    Remove
                  </Button>
                </div>
              </li>
            ))}
          </ul>
          <aside className="border border-border p-4">
            <p>{cart.data.itemCount} items</p>
            <p className="mt-2 text-sm text-muted-foreground">
              Shipping is calculated at checkout.
            </p>
            <Link
              to="/checkout"
              className="mt-4 inline-flex min-h-11 items-center bg-foreground px-4 text-background"
            >
              Checkout
            </Link>
          </aside>
        </div>
      )}
      {change.isError ? (
        <p className="mt-4" role="alert">
          {isApiError(change.error)
            ? (change.error.detail ?? change.error.title)
            : 'The bag could not be updated.'}
        </p>
      ) : null}
    </Container>
  );
}
