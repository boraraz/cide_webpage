import { fireEvent, render, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { navigation } from '@/routes/navigation';
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

const administrator = {
  id: '018f0000-0000-7000-8000-000000000002',
  email: 'admin@example.com',
  firstName: 'Admin',
  lastName: 'User',
  phoneNumber: null,
  emailVerified: true,
  roles: ['admin'],
};

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function problem(status: number, title: string) {
  return new Response(JSON.stringify({ title, status }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

describe('admin shell', () => {
  beforeEach(() => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);
      if (url.includes('/api/v1/admin/auth/me')) return json(administrator);
      if (url.includes('/api/v1/health/ready')) {
        return json(
          {
            status: 'Unhealthy',
            checks: [
              { name: 'database', status: 'Healthy' },
              { name: 'object-storage', status: 'Unhealthy' },
            ],
          },
          503,
        );
      }
      return problem(404, 'Not found');
    });
  });

  it('renders the dashboard with the development-only warning', async () => {
    renderAt('/');

    expect(await screen.findByRole('heading', { level: 1, name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.getByRole('note')).toHaveTextContent(/development only/i);
  });

  it('lists every admin section but only links implemented ones', async () => {
    renderAt('/');

    const nav = await screen.findByRole('navigation', { name: 'Admin' });
    for (const item of navigation) {
      expect(within(nav).getByText(item.label)).toBeInTheDocument();
    }
    const links = within(nav).getAllByRole('link');
    expect(links.map((link) => link.textContent)).toEqual([
      'Dashboard',
      'Products',
      'Categories',
      'Collections',
      'Stock',
      'Movements',
      'Homepage',
      'Orders',
    ]);
  });

  it('shows dependency readiness reported by the API, including 503 reports', async () => {
    renderAt('/');

    const card = await screen.findByRole('region', { name: 'System status' });
    expect(await within(card).findByText('object-storage')).toBeInTheDocument();
    expect(within(card).getByText('database').nextSibling).toHaveTextContent('Healthy');
  });

  it('opens the mobile navigation drawer and closes it after navigating', async () => {
    renderAt('/');

    fireEvent.click(await screen.findByRole('button', { name: 'Open navigation' }));

    const drawer = await screen.findByRole('dialog', { name: 'Navigation' });
    const mobileNav = within(drawer).getByRole('navigation', { name: 'Admin (mobile)' });
    fireEvent.click(within(mobileNav).getByRole('link', { name: 'Dashboard' }));

    expect(screen.queryByRole('dialog', { name: 'Navigation' })).not.toBeInTheDocument();
  });

  it('renders the not-found page for unknown routes', async () => {
    renderAt('/does-not-exist');

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
  });
});
