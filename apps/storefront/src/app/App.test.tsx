import { fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { routes } from '@/routes/routes';
import { AppProviders } from './providers';

function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  return render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );
}

describe('storefront shell', () => {
  beforeEach(() => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ status: 'Healthy', checks: [] }), {
        headers: { 'Content-Type': 'application/json' },
      }),
    );
  });

  it('renders the home page inside the public layout', async () => {
    renderAt('/');

    expect(
      await screen.findByRole('heading', { level: 1, name: /configurable content blocks/i }),
    ).toBeInTheDocument();
    expect(screen.getByRole('banner')).toHaveTextContent(/sportswear/i);
    expect(screen.getByRole('contentinfo')).toBeInTheDocument();
  });

  it('shows the API status from the readiness endpoint', async () => {
    renderAt('/');

    expect(await screen.findByText('API healthy')).toBeInTheDocument();
    expect(globalThis.fetch).toHaveBeenCalledWith(
      'http://api.test/api/v1/health/ready',
      expect.objectContaining({ credentials: 'include' }),
    );
  });

  it('opens an accessible mobile menu', async () => {
    renderAt('/');

    fireEvent.click(await screen.findByRole('button', { name: 'Open menu' }));

    const menu = await screen.findByRole('dialog', { name: 'Menu' });
    expect(within(menu).getByRole('link', { name: 'Home' })).toBeInTheDocument();

    fireEvent.click(within(menu).getByRole('button', { name: 'Close' }));
    expect(screen.queryByRole('dialog', { name: 'Menu' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Open menu' }));
    fireEvent.keyDown(await screen.findByRole('dialog', { name: 'Menu' }), { key: 'Escape' });
    expect(screen.queryByRole('dialog', { name: 'Menu' })).not.toBeInTheDocument();
  });

  it('renders the not-found page for unknown routes', async () => {
    renderAt('/does-not-exist');

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
  });
});
