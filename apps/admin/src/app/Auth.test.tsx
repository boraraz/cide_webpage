import { fireEvent, render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { routes } from '@/routes/routes';
import { AppProviders } from './providers';

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

function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  return render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );
}

describe('admin authentication', () => {
  beforeEach(() => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);
      if (url.includes('/api/v1/admin/auth/me')) return problem(401, 'Unauthorized');
      if (url.includes('/api/v1/auth/csrf')) return json({ token: 'csrf' });
      if (url.includes('/api/v1/auth/logout')) return new Response(null, { status: 204 });
      return problem(404, 'Not found');
    });
  });

  it('sends an anonymous visitor to the login page', async () => {
    renderAt('/');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Dashboard' })).not.toBeInTheDocument();
  });

  it('shows access denied for a signed-in customer and signs them out', async () => {
    let signedIn = true;
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);
      if (url.includes('/api/v1/auth/logout')) {
        signedIn = false;
        return new Response(null, { status: 204 });
      }
      if (url.includes('/api/v1/admin/auth/me')) {
        return signedIn ? problem(403, 'Forbidden') : problem(401, 'Unauthorized');
      }
      if (url.includes('/api/v1/auth/csrf')) return json({ token: 'csrf' });
      return problem(401, 'Unauthorized');
    });
    renderAt('/');

    expect(await screen.findByRole('heading', { name: 'Access denied' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    const urls = vi.mocked(globalThis.fetch).mock.calls.map((call) => String(call[0]));
    expect(urls).toContainEqual(expect.stringContaining('/api/v1/auth/logout'));
    expect(urls.filter((url) => url.includes('/api/v1/admin/auth/me')).length).toBeGreaterThan(1);
  });
});
