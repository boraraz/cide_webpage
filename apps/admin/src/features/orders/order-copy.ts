export function orderStatusLabel(status: string): string {
  switch (status) {
    case 'paymentpending':
      return 'Payment pending';
    case 'cancelled':
      return 'Cancelled';
    case 'expired':
      return 'Expired';
    case 'paid':
      return 'Paid';
    default:
      return status;
  }
}

export function reservationCopy(minutes: number, expiresAt: string): string {
  const when = new Date(expiresAt).toLocaleString('tr-TR', {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
  return `Reservation ends at ${when} (${minutes} minutes after placement).`;
}
