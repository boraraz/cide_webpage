import { fireEvent, render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { routes } from '@/routes/routes';
import { AppProviders } from './providers';

const customer = {
  id: '018f0000-0000-7000-8000-000000000001',
  email: 'ada@example.com',
  firstName: 'Ada',
  lastName: 'Lovelace',
  phoneNumber: null,
  emailVerified: false,
  roles: ['customer'],
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

function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  return render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );
}

function installFetch(
  resolve: (url: string, init: RequestInit | undefined) => Response | Promise<Response>,
) {
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) =>
    resolve(String(input), init),
  );
}

describe('storefront account', () => {
  beforeEach(() => {
    installFetch((url) => {
      if (url.includes('/api/v1/auth/me')) return problem(401, 'Unauthorized');
      if (url.includes('/api/v1/auth/csrf')) return json({ token: 'csrf' });
      if (url.includes('/api/v1/categories')) return json([]);
      return json({ status: 'Healthy', checks: [] });
    });
  });

  it('rejects an incomplete registration before calling the API', async () => {
    renderAt('/register');

    fireEvent.click(await screen.findByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('Enter your email.')).toBeInTheDocument();
    expect(screen.getByText('Use at least 10 characters.')).toBeInTheDocument();
    expect(calls()).not.toContainEqual(expect.stringContaining('/api/v1/auth/register'));
  });

  it('rejects a login with an empty password before calling the API', async () => {
    renderAt('/login');

    fireEvent.change(await screen.findByLabelText('Email'), {
      target: { value: 'ada@example.com' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Enter your password.')).toBeInTheDocument();
    expect(calls()).not.toContainEqual(expect.stringContaining('/api/v1/auth/login'));
  });

  it('shows a loading state while sign-in is in progress', async () => {
    let release: (response: Response) => void = () => undefined;
    const pending = new Promise<Response>((resolve) => {
      release = resolve;
    });
    installFetch((url) => {
      if (url.includes('/api/v1/auth/me')) return problem(401, 'Unauthorized');
      if (url.includes('/api/v1/auth/csrf')) return json({ token: 'csrf' });
      if (url.includes('/api/v1/auth/login')) return pending;
      if (url.includes('/api/v1/categories')) return json([]);
      return json({ status: 'Healthy', checks: [] });
    });
    renderAt('/login');

    fireEvent.change(await screen.findByLabelText('Email'), {
      target: { value: 'ada@example.com' },
    });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'correct-horse-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeDisabled();
    release(json(customer));
    expect(await screen.findByRole('heading', { name: 'Account' })).toBeInTheDocument();
  });

  it('sends an anonymous visitor from the account page to sign in', async () => {
    renderAt('/account');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('rejects an empty profile name before saving', async () => {
    installFetch((url) => {
      if (url.includes('/api/v1/auth/me')) return json(customer);
      if (url.includes('/api/v1/auth/csrf')) return json({ token: 'csrf' });
      if (url.includes('/api/v1/categories')) return json([]);
      return json({ status: 'Healthy', checks: [] });
    });
    renderAt('/account');

    const firstName = await screen.findByLabelText('First name');
    fireEvent.change(firstName, { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save profile' }));

    expect(
      await screen.findByText('Enter a first name of 1 to 100 characters.'),
    ).toBeInTheDocument();
    expect(calls()).not.toContainEqual(expect.stringContaining('/api/v1/account/profile'));
  });

  it('signs out and drops the cached session', async () => {
    let signedIn = true;
    installFetch((url) => {
      if (url.includes('/api/v1/auth/logout')) {
        signedIn = false;
        return new Response(null, { status: 204 });
      }
      if (url.includes('/api/v1/auth/csrf')) return json({ token: 'csrf' });
      if (url.includes('/api/v1/auth/me'))
        return signedIn ? json(customer) : problem(401, 'Unauthorized');
      if (url.includes('/api/v1/categories')) return json([]);
      return json({ status: 'Healthy', checks: [] });
    });
    renderAt('/account');

    fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }));

    expect(await screen.findAllByRole('link', { name: 'Sign in' })).not.toHaveLength(0);
    expect(calls()).toContainEqual(expect.stringContaining('/api/v1/auth/logout'));
  });
});

function calls() {
  return vi.mocked(globalThis.fetch).mock.calls.map((call) => String(call[0]));
}
