export function formatTry(value: string): string {
  const amount = Number(value);
  if (!Number.isFinite(amount)) return value;
  return `${new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount)} TL`;
}

export function discountLabel(price: string, compareAt: string | null | undefined): string | null {
  if (!compareAt) return null;
  const current = Number(price);
  const previous = Number(compareAt);
  if (!Number.isFinite(current) || !Number.isFinite(previous) || previous <= current) return null;
  return `${Math.round((1 - current / previous) * 100)}%`;
}
