import { Button, Select, TextField } from '@commerce/ui';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { catalog } from '@/lib/api';
import { useIdempotencyKey } from '@/lib/idempotency';
import { problemMessage } from '@/lib/problem';

export default function InventoryPage() {
  const queryClient = useQueryClient();
  const [stock, setStock] = useState<'all' | 'in_stock' | 'low' | 'out'>('all');
  const [q, setQ] = useState('');
  const [message, setMessage] = useState<string>();
  const key = useIdempotencyKey();
  const inventory = useQuery({
    queryKey: ['inventory', q, stock],
    queryFn: () => catalog.inventory({ q: q || undefined, stock, pageSize: 48 }),
  });
  const adjust = useMutation({
    mutationFn: (input: { variantId: string; quantity: number; movementType: string }) =>
      catalog.adjustInventory(input.variantId, key.current(), {
        movementType: input.movementType,
        quantity: input.quantity,
        note: null,
      }),
    onSuccess: async () => {
      key.clear();
      setMessage('Stock updated.');
      await queryClient.invalidateQueries({ queryKey: ['inventory'] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold sm:text-2xl">Stock</h1>
      {message ? <p role="status">{message}</p> : null}
      <form
        className="flex flex-wrap gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          const data = new FormData(event.currentTarget);
          setQ(String(data.get('q') ?? ''));
          setStock(String(data.get('stock') ?? 'all') as typeof stock);
        }}
      >
        <input name="q" aria-label="Search stock" className="min-h-11 border border-border px-3" />
        <select
          name="stock"
          aria-label="Stock level"
          defaultValue="all"
          className="min-h-11 border border-border px-3"
        >
          <option value="all">All</option>
          <option value="in_stock">In stock</option>
          <option value="low">Low</option>
          <option value="out">Out</option>
        </select>
        <Button type="submit">Filter</Button>
      </form>
      <div className="overflow-x-auto border border-border">
        <table className="w-full min-w-[48rem] text-left text-sm">
          <thead>
            <tr>
              <th className="p-3">SKU</th>
              <th className="p-3">Options</th>
              <th className="p-3">Available</th>
              <th className="p-3">Adjust</th>
            </tr>
          </thead>
          <tbody>
            {inventory.data?.items.map((row) => {
              const item = row as typeof row & { optionSummary?: string };
              return (
                <tr key={item.variantId} className="border-t border-border">
                  <td className="p-3">{item.sku}</td>
                  <td className="p-3">{item.optionSummary}</td>
                  <td className="p-3">{item.available}</td>
                  <td className="p-3">
                    <form
                      className="flex gap-2"
                      onSubmit={(event) => {
                        event.preventDefault();
                        const data = new FormData(event.currentTarget);
                        adjust.mutate({
                          variantId: item.variantId,
                          quantity: Number(data.get('quantity')),
                          movementType: String(data.get('movementType')),
                        });
                      }}
                    >
                      <Select name="movementType" label="Type" defaultValue="manualIncrease">
                        <option value="manualIncrease">Increase</option>
                        <option value="manualDecrease">Decrease</option>
                        <option value="correction">Correction</option>
                      </Select>
                      <TextField name="quantity" label="Quantity" inputMode="numeric" required />
                      <Button type="submit">Apply</Button>
                    </form>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
