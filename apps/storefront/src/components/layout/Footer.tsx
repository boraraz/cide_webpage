import { Container } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { ApiStatusIndicator } from '@/features/system-status/ApiStatusIndicator';
import { catalog } from '@/lib/api';

export function Footer() {
  const categories = useQuery({ queryKey: ['categories'], queryFn: () => catalog.categories() });
  return (
    <footer className="border-t border-border pb-[env(safe-area-inset-bottom)]">
      <Container className="grid gap-8 py-10 text-sm md:grid-cols-4">
        <div>
          <p className="font-display text-lg tracking-editorial uppercase">Sportswear</p>
          <p className="mt-3 text-muted-foreground">© {new Date().getFullYear()} Sportswear</p>
        </div>
        <nav aria-label="Footer">
          <p className="text-xs tracking-editorial text-muted-foreground uppercase">Shop</p>
          <ul className="mt-3 flex flex-col">
            <li>
              <Link className="inline-flex min-h-11 items-center" to="/shop">
                All products
              </Link>
            </li>
            {(Array.isArray(categories.data) ? categories.data : []).map((category) => (
              <li key={category.id}>
                <Link
                  className="inline-flex min-h-11 items-center"
                  to={`/category/${category.slug}`}
                >
                  {category.name}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
        <div>
          <p className="text-xs tracking-editorial text-muted-foreground uppercase">Account</p>
          <ul className="mt-3">
            <li>
              <Link className="inline-flex min-h-11 items-center" to="/account">
                Account
              </Link>
            </li>
            <li>
              <Link className="inline-flex min-h-11 items-center" to="/login">
                Sign in
              </Link>
            </li>
          </ul>
        </div>
        <div className="text-muted-foreground">
          <p className="text-xs tracking-editorial uppercase">Support, legal, and social</p>
          <p className="mt-3">
            These sections are not available yet. No contact details or policies are published.
          </p>
          {import.meta.env.DEV ? (
            <div className="mt-4">
              <ApiStatusIndicator />
            </div>
          ) : null}
        </div>
      </Container>
    </footer>
  );
}
