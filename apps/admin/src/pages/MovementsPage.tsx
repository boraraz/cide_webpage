import { useQuery } from '@tanstack/react-query';
import { catalog } from '@/lib/api';

export default function MovementsPage() {
  const movements = useQuery({
    queryKey: ['movements'],
    queryFn: () => catalog.movementHistory({ pageSize: 48 }),
  });

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold sm:text-2xl">Movements</h1>
      <p className="text-sm text-muted-foreground">
        Previous on-hand is the quantity after the movement minus the change.
      </p>
      <div className="overflow-x-auto border border-border">
        <table className="w-full min-w-[56rem] text-left text-sm">
          <thead>
            <tr>
              <th className="p-3">When</th>
              <th className="p-3">Product</th>
              <th className="p-3">SKU</th>
              <th className="p-3">Change</th>
              <th className="p-3">Previous</th>
              <th className="p-3">On hand</th>
              <th className="p-3">Actor</th>
            </tr>
          </thead>
          <tbody>
            {movements.data?.items.map((item) => (
              <tr key={item.id} className="border-t border-border">
                <td className="p-3">{new Date(item.createdAt).toLocaleString('tr-TR')}</td>
                <td className="p-3">{item.productName}</td>
                <td className="p-3">{item.sku}</td>
                <td className="p-3">{item.quantityDelta}</td>
                <td className="p-3">{Number(item.onHandAfter) - Number(item.quantityDelta)}</td>
                <td className="p-3">{item.onHandAfter}</td>
                <td className="p-3">{item.actorEmail ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
