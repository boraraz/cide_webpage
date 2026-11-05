import { Container, cx, Drawer } from '@commerce/ui';
import { Menu, Search, ShoppingBag, User } from 'lucide-react';
import { useState } from 'react';
import { Link, NavLink } from 'react-router';
import { useSession } from '@/features/auth/useSession';
import { primaryNavigation } from '@/routes/navigation';

const iconButton = 'inline-flex size-11 items-center justify-center';

export function Header() {
  const [menuOpen, setMenuOpen] = useState(false);
  const session = useSession();
  const signedIn = Boolean(session.data);

  return (
    <header className="sticky top-0 z-40 border-b border-border bg-background/95 pt-[env(safe-area-inset-top)] backdrop-blur">
      <Container className="flex h-14 items-center gap-2 lg:h-16 lg:gap-8">
        <button
          type="button"
          onClick={() => setMenuOpen(true)}
          aria-expanded={menuOpen}
          aria-controls="storefront-menu"
          className={cx(iconButton, '-ml-3 lg:hidden')}
        >
          <Menu aria-hidden="true" className="size-6" strokeWidth={1.5} />
          <span className="sr-only">Open menu</span>
        </button>

        <Link
          to="/"
          className="inline-flex min-h-11 items-center font-display text-base font-semibold tracking-editorial uppercase sm:text-lg"
          aria-label="Sportswear home"
        >
          Sportswear
        </Link>

        <nav aria-label="Primary" className="hidden lg:block">
          <ul className="flex items-center gap-6 text-sm">
            {primaryNavigation.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  end
                  className={({ isActive }) =>
                    cx(
                      'inline-flex min-h-11 items-center underline-offset-8',
                      isActive && 'underline',
                    )
                  }
                >
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <ul className="-mr-3 ml-auto flex items-center" aria-label="Store actions">
          <li className="hidden sm:block">
            <button
              type="button"
              disabled
              title="Search (coming soon)"
              className={cx(iconButton, 'text-muted-foreground disabled:cursor-not-allowed')}
            >
              <Search aria-hidden="true" className="size-5" strokeWidth={1.5} />
              <span className="sr-only">Search (coming soon)</span>
            </button>
          </li>
          <li>
            <Link
              to={signedIn ? '/account' : '/login'}
              className={iconButton}
              aria-label={signedIn ? 'Account' : 'Sign in'}
            >
              <User aria-hidden="true" className="size-5" strokeWidth={1.5} />
            </Link>
          </li>
          <li>
            <button
              type="button"
              disabled
              title="Bag (coming soon)"
              className={cx(iconButton, 'text-muted-foreground disabled:cursor-not-allowed')}
            >
              <ShoppingBag aria-hidden="true" className="size-5" strokeWidth={1.5} />
              <span className="sr-only">Bag (coming soon)</span>
            </button>
          </li>
        </ul>
      </Container>

      <Drawer open={menuOpen} onClose={() => setMenuOpen(false)} title="Menu">
        <nav id="storefront-menu" aria-label="Mobile">
          <ul className="flex flex-col py-2">
            {primaryNavigation.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  end
                  onClick={() => setMenuOpen(false)}
                  className={({ isActive }) =>
                    cx('flex min-h-11 items-center px-4 text-base', isActive && 'font-medium')
                  }
                >
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </Drawer>
    </header>
  );
}
