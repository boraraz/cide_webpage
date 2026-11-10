/** Converts `1299.00` or `1.299,00` into the API decimal string. */
export function toApiMoney(input: string): string | null {
  const value = input.trim();
  if (value.includes(',') && value.includes('.')) {
    if (!/^\d{1,3}(?:\.\d{3})+,\d{1,2}$/.test(value)) return null;
    return toApiMoney(value.replaceAll('.', '').replace(',', '.'));
  }
  if (value.includes(',')) {
    if (!/^\d+(?:,\d{1,2})$/.test(value)) return null;
    return toApiMoney(value.replace(',', '.'));
  }
  if (!/^\d+(?:\.\d{1,2})?$/.test(value)) return null;
  const [whole, fraction = ''] = value.split('.');
  if (!whole || whole.length > 10) return null;
  return `${whole}.${fraction.padEnd(2, '0')}`;
}

export function formatTry(apiAmount: string): string {
  const formatted = new Intl.NumberFormat('tr-TR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(Number(apiAmount));
  return `${formatted} TL`;
}

export function discountPreview(price: string, compareAt: string): string | null {
  const current = Number(price);
  const previous = Number(compareAt);
  if (!Number.isFinite(current) || !Number.isFinite(previous) || previous <= current) return null;
  const percent = Math.round(((previous - current) / previous) * 100);
  return `${percent}% off ${formatTry(compareAt)}`;
}
