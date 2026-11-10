import { fireEvent, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { routes } from '@/routes/routes';
import { AppProviders } from '@/app/providers';
import { matrixPreview } from '@/lib/variants';
import { useIdempotencyKey } from '@/lib/idempotency';

const administrator = {
  id: '018f0000-0000-7000-8000-000000000002',
  email: 'admin@example.com',
  firstName: 'Admin',
  lastName: 'User',
  phoneNumber: null,
  emailVerified: true,
  roles: ['admin'],
};

const product = {
  id: '11111111-1111-7111-8111-111111111111',
  name: 'Training tee',
  slug: 'training-tee',
  shortDescription: 'Short',
  description: 'Long description',
  brand: null,
  isFeatured: false,
  status: 'draft',
  publishedAt: null,
  createdAt: '2026-11-10T00:00:00Z',
  updatedAt: '2026-11-10T00:00:00Z',
  version: 4,
  categories: [],
  collections: [],
  options: [
    {
      id: 'opt-color',
      name: 'Color',
      position: 1,
      values: [
        { id: 'black', value: 'Black', code: null, position: 1 },
        { id: 'white', value: 'White', code: null, position: 2 },
      ],
    },
    {
      id: 'opt-size',
      name: 'Size',
      position: 2,
      values: [{ id: 'm', value: 'M', code: null, position: 1 }],
    },
  ],
  variants: [],
  media: [
    {
      id: 'media-1',
      variantId: null,
      altText: 'Front',
      displayOrder: 0,
      isPrimary: true,
      contentType: 'image/png',
      fileSize: 3,
      path: '/api/v1/admin/media/media-1',
      width: null,
      height: null,
      sources: [],
    },
  ],
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

describe('catalogue administration', () => {
  beforeEach(() => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);
      const method = init?.method ?? 'GET';
      if (url.includes('/api/v1/auth/csrf')) return json({ token: 'csrf-token' });
      if (url.includes('/api/v1/admin/auth/me')) return json(administrator);
      if (url.includes('/api/v1/health/ready')) return json({ status: 'Healthy', checks: [] });
      if (url.includes('/api/v1/admin/categories')) return json([]);
      if (url.includes('/api/v1/admin/collections') && method === 'POST') {
        return json(
          { title: 'Conflict', status: 409, detail: 'That slug is already in use.' },
          409,
        );
      }
      if (url.includes('/api/v1/admin/collections')) return json([]);
      if (url.includes('/api/v1/admin/products?') || url.endsWith('/api/v1/admin/products')) {
        if (method === 'GET') return json({ items: [], page: 1, pageSize: 24, total: 0 });
      }
      if (url.includes('/api/v1/admin/media/') && method === 'GET') {
        return new Response(new Uint8Array([137, 80, 78, 71]), {
          status: 200,
          headers: { 'Content-Type': 'image/png' },
        });
      }
      if (url.includes('/api/v1/admin/media/') && method === 'PATCH') {
        const body = JSON.parse(String(init?.body ?? '{}')) as {
          altText?: string;
          expectedVersion?: number;
        };
        (globalThis as { lastAlt?: { altText?: string; expectedVersion?: number } }).lastAlt = body;
        return json({ ...product.media[0], altText: body.altText });
      }
      if (url.includes('/media') && method === 'POST') {
        return json(
          {
            title: 'Invalid',
            status: 400,
            errors: { file: ['Upload a JPEG, PNG or WebP image up to 5 MB.'] },
          },
          400,
        );
      }
      if (url.includes('/api/v1/admin/products/11111111')) {
        if (method === 'PATCH') {
          return json(
            {
              title: 'Conflict',
              status: 409,
              currentVersion: 5,
              errors: {
                name: [
                  'This product was saved in another session. Reload to see the latest version.',
                ],
              },
            },
            409,
          );
        }
        return json(product);
      }
      if (url.includes('/api/v1/admin/content/homepage/revisions') && method === 'GET') {
        return json([
          {
            id: 'rev-1',
            version: 1,
            createdAt: '2026-11-10T00:00:00Z',
            createdByUserId: administrator.id,
          },
        ]);
      }
      if (url.includes('/api/v1/admin/content/homepage') && method === 'GET') {
        return json({
          draftVersion: 2,
          draftRevisionId: null,
          publishedRevisionId: null,
          publishedAt: null,
          configuration: {
            blocks: [
              { id: 'a', type: 'spacer', visible: true, config: { size: 'sm' } },
              { id: 'b', type: 'spacer', visible: true, config: { size: 'lg' } },
            ],
          },
        });
      }
      return json({ title: 'Not found', status: 404 }, 404);
    });
  });

  it('shows an empty product list', async () => {
    renderAt('/products');
    expect(await screen.findByText('No products match this search.')).toBeInTheDocument();
  });

  it('shows a stale version instead of retrying the write', async () => {
    renderAt('/products/11111111-1111-7111-8111-111111111111');
    expect(await screen.findByText('Version 4')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save product' }));
    expect(await screen.findByRole('status')).toHaveTextContent(/another session/i);
  });

  it('previews the variant matrix before creating rows', async () => {
    renderAt('/products/11111111-1111-7111-8111-111111111111');
    expect(await screen.findByTestId('matrix-preview')).toHaveTextContent(
      '2 combinations: Black / M, White / M',
    );
    expect(
      matrixPreview([
        [
          { id: 'black', label: 'Black' },
          { id: 'white', label: 'White' },
        ],
        [{ id: 'm', label: 'M' }],
      ]),
    ).toHaveLength(2);
  });

  it('saves edited image alt text with the loaded product version', async () => {
    renderAt('/products/11111111-1111-7111-8111-111111111111');
    fireEvent.change(await screen.findByLabelText('Alt text'), {
      target: { value: 'Studio front' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Save alt text' }));
    expect(await screen.findByRole('status')).toHaveTextContent('Alt text saved.');
    expect(
      (globalThis as { lastAlt?: { altText?: string; expectedVersion?: number } }).lastAlt,
    ).toMatchObject({ altText: 'Studio front', expectedVersion: 4 });
  });

  it('reports an upload failure', async () => {
    renderAt('/products/11111111-1111-7111-8111-111111111111');
    const input = await screen.findByLabelText('Product image');
    const file = new File([new Uint8Array([1, 2, 3])], 'notes.txt', { type: 'text/plain' });
    fireEvent.change(input, { target: { files: [file] } });
    expect(await screen.findByRole('status')).toHaveTextContent(/jpeg, png or webp/i);
  });

  it('asks before leaving a dirty product', async () => {
    renderAt('/products/11111111-1111-7111-8111-111111111111');
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Changed tee' } });
    fireEvent.click(screen.getByRole('link', { name: 'Products' }));
    expect(
      await screen.findByRole('dialog', { name: 'Leave without saving?' }),
    ).toBeInTheDocument();
  });

  it('shows a collection conflict from the API', async () => {
    renderAt('/collections');
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Drop' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save collection' }));
    expect(await screen.findByRole('status')).toHaveTextContent(/already in use/i);
  });

  it('reorders homepage blocks and confirms publication', async () => {
    renderAt('/content/homepage');
    fireEvent.click(await screen.findByRole('button', { name: 'Move block 2 up' }));
    fireEvent.click(screen.getByRole('button', { name: 'Publish' }));
    expect(await screen.findByRole('dialog', { name: 'Publish homepage?' })).toBeInTheDocument();
  });

  it('asks before leaving a dirty homepage', async () => {
    renderAt('/content/homepage');
    fireEvent.click(await screen.findByRole('button', { name: 'Add spacer' }));
    fireEvent.click(screen.getByRole('link', { name: 'Products' }));
    expect(
      await screen.findByRole('dialog', { name: 'Leave without saving?' }),
    ).toBeInTheDocument();
  });
});

function KeyProbe() {
  const key = useIdempotencyKey();
  return (
    <button
      type="button"
      onClick={() => {
        const first = key.current();
        const second = key.current();
        const element = document.getElementById('keys');
        if (element) element.textContent = `${first}|${second}`;
      }}
    >
      Use key
    </button>
  );
}

describe('idempotency keys', () => {
  it('reuses the key until the action is cleared', () => {
    const client = new QueryClient();
    render(
      <QueryClientProvider client={client}>
        <p id="keys" />
        <KeyProbe />
      </QueryClientProvider>,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Use key' }));
    const [first, second] = screen.getByText(/idem-/).textContent?.split('|') ?? [];
    expect(first).toBe(second);
    expect(first?.length).toBeGreaterThan(16);
  });
});
