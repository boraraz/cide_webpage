import { fireEvent, render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { routes } from '@/routes/routes';
import { AppProviders } from '../app/providers';

const emptyCart = { id: null, itemCount: 0, currency: 'TRY', items: [], expiresAt: null };
const cart = {
  id: '018f0000-0000-7000-8000-000000000010',
  itemCount: 1,
  currency: 'TRY',
  expiresAt: '2026-12-01T00:00:00Z',
  items: [
    {
      id: '018f0000-0000-7000-8000-000000000011',
      variantId: '018f0000-0000-7000-8000-000000000012',
      productName: 'Training jacket',
      sku: 'JACKET-1',
      options: [],
      unitPrice: '1000.00',
      quantity: 1,
      lineTotal: '1000.00',
      availableQuantity: 4,
      purchasable: true,
      unavailableReason: null,
      quantityLimited: false,
    },
  ],
};
const quote = {
  currency: 'TRY',
  merchandiseTotal: '1000.00',
  shippingTotal: '149.00',
  grandTotal: '1149.00',
  reservationExpiresInMinutes: 30,
  shippingFee: '149.00',
  freeShippingThreshold: '2500.00',
  purchasable: true,
  lines: [],
};
const order = {
  id: '018f0000-0000-7000-8000-000000000020',
  reference: 'SW-TEST1234',
  status: 'paymentpending',
  email: 'ada@example.com',
  phone: '+905551112233',
  currency: 'TRY',
  merchandiseTotal: '1000.00',
  shippingTotal: '149.00',
  grandTotal: '1149.00',
  expiresAt: '2026-11-13T00:30:00Z',
  createdAt: '2026-11-13T00:00:00Z',
  paymentCollected: false,
  reservationMinutes: 30,
  items: [
    {
      variantId: '018f0000-0000-7000-8000-000000000012',
      productName: 'Training jacket',
      sku: 'JACKET-1',
      options: [],
      unitPrice: '1000.00',
      quantity: 1,
      lineTotal: '1000.00',
    },
  ],
  addresses: [],
  history: [],
};

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  return render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );
}

describe('storefront commerce', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    window.history.replaceState(null, '', '/');
  });

  it('shows an empty bag', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);
      if (url.includes('/api/v1/cart')) return json(emptyCart);
      if (url.includes('/api/v1/categories')) return json([]);
      if (url.includes('/api/v1/auth/me')) {
        return json({ title: 'Unauthorized', status: 401 }, 401);
      }
      return json({ status: 'Healthy', checks: [] });
    });
    renderAt('/cart');
    expect(await screen.findByText('Your bag is empty.')).toBeInTheDocument();
  });

  it('requires checkout details before placing an order', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);
      if (url.includes('/api/v1/checkout/quote')) return json(quote);
      if (url.includes('/api/v1/cart')) return json(cart);
      if (url.includes('/api/v1/categories')) return json([]);
      if (url.includes('/api/v1/auth/me')) {
        return json({ title: 'Unauthorized', status: 401 }, 401);
      }
      if (url.includes('/api/v1/auth/csrf')) return json({ token: 'csrf' });
      return json({ status: 'Healthy', checks: [] });
    });
    renderAt('/checkout');
    expect(await screen.findByText(/reserves stock for 30 minutes/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Place order' }));
    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
  });

  it('reads a guest confirmation token from the fragment and does not store it', async () => {
    window.history.replaceState(null, '', '/order/confirmation#capability-token');
    const seen: string[] = [];
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);
      if (url.includes('/api/v1/orders/access')) {
        seen.push(new Headers(init?.headers).get('X-Order-Access') ?? '');
        return json(order);
      }
      if (url.includes('/api/v1/cart')) return json(emptyCart);
      if (url.includes('/api/v1/categories')) return json([]);
      if (url.includes('/api/v1/auth/me')) {
        return json({ title: 'Unauthorized', status: 401 }, 401);
      }
      return json({ status: 'Healthy', checks: [] });
    });
    renderAt('/order/confirmation');
    expect(await screen.findByText(/SW-TEST1234/)).toBeInTheDocument();
    expect(screen.getByText('No payment has been collected.')).toBeInTheDocument();
    expect(screen.getByText(/30 minutes/)).toBeInTheDocument();
    expect(seen).toEqual(['capability-token']);
    expect(window.location.hash).toBe('');
    expect(window.location.search).not.toContain('capability-token');
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
});
