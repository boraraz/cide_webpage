import type { CategoryNode } from '@commerce/api-client';
import { Container, cx, Drawer } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { Menu, Search, ShoppingBag, User } from 'lucide-react';
import { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router';
import { useSession } from '@/features/auth/useSession';
import { catalog } from '@/lib/api';

const iconButton = 'inline-flex size-11 items-center justify-center';

export function Header() {
  const [menuOpen, setMenuOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const session = useSession();
  const signedIn = Boolean(session.data);
  const categories = useQuery({ queryKey: ['categories'], queryFn: () => catalog.categories() });
  const navigate = useNavigate();
  const top = Array.isArray(categories.data) ? categories.data : [];

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
          className="inline-flex min-h-11 items-center font-display text-base tracking-editorial uppercase sm:text-lg"
          aria-label="Sportswear home"
        >
          Sportswear
        </Link>
        <nav aria-label="Primary" className="hidden lg:block">
          <ul className="flex items-center gap-6 text-sm">
            <li>
              <ShopLink />
            </li>
            {top.map((category) => (
              <li key={category.id}>
                <NavLink
                  to={`/category/${category.slug}`}
                  className={({ isActive }) =>
                    cx(
                      'inline-flex min-h-11 items-center',
                      isActive && 'underline underline-offset-8',
                    )
                  }
                >
                  {category.name}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
        <ul className="-mr-3 ml-auto flex items-center" aria-label="Store actions">
          <li>
            <button
              type="button"
              className={iconButton}
              aria-expanded={searchOpen}
              aria-controls="storefront-search"
              onClick={() => setSearchOpen((open) => !open)}
            >
              <Search aria-hidden="true" className="size-5" strokeWidth={1.5} />
              <span className="sr-only">Search</span>
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
      {searchOpen ? (
        <Container className="pb-3">
          <form
            id="storefront-search"
            role="search"
            onSubmit={(event) => {
              event.preventDefault();
              const data = new FormData(event.currentTarget);
              const q = String(data.get('q') ?? '').trim();
              setSearchOpen(false);
              navigate(q ? `/search?q=${encodeURIComponent(q)}` : '/search');
            }}
          >
            <label className="sr-only" htmlFor="header-search">
              Search products
            </label>
            <input
              id="header-search"
              name="q"
              className="min-h-11 w-full border border-border px-3"
              placeholder="Search"
            />
          </form>
        </Container>
      ) : null}
      <Drawer open={menuOpen} onClose={() => setMenuOpen(false)} title="Menu">
        <nav id="storefront-menu" aria-label="Mobile">
          <ul className="flex flex-col py-2">
            <li>
              <NavLink
                to="/shop"
                onClick={() => setMenuOpen(false)}
                className="flex min-h-11 items-center px-4 text-base"
              >
                Shop
              </NavLink>
            </li>
            {top.map((category) => (
              <CategoryLinks
                key={category.id}
                category={category}
                depth={0}
                onNavigate={() => setMenuOpen(false)}
              />
            ))}
          </ul>
        </nav>
      </Drawer>
    </header>
  );
}

function ShopLink() {
  return (
    <NavLink
      to="/shop"
      className={({ isActive }) =>
        cx('inline-flex min-h-11 items-center', isActive && 'underline underline-offset-8')
      }
    >
      Shop
    </NavLink>
  );
}

function CategoryLinks({
  category,
  depth,
  onNavigate,
}: {
  category: CategoryNode;
  depth: number;
  onNavigate: () => void;
}) {
  return (
    <li>
      <NavLink
        to={`/category/${category.slug}`}
        onClick={onNavigate}
        className="flex min-h-11 items-center px-4 text-base"
        style={{ paddingLeft: `${1 + depth}rem` }}
      >
        {category.name}
      </NavLink>
      {category.children.length > 0 ? (
        <ul>
          {category.children.map((child) => (
            <CategoryLinks
              key={child.id}
              category={child}
              depth={depth + 1}
              onNavigate={onNavigate}
            />
          ))}
        </ul>
      ) : null}
    </li>
  );
}
