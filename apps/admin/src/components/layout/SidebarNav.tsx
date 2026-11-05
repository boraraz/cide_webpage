import { cx } from '@commerce/ui';
import { NavLink } from 'react-router';
import { navigation } from '@/routes/navigation';

const itemClass = 'flex min-h-11 items-center gap-3 rounded-md px-3 text-sm';

export function SidebarNav({
  label = 'Admin',
  onNavigate,
}: {
  label?: string;
  onNavigate?: () => void;
}) {
  return (
    <nav aria-label={label}>
      <ul className="flex flex-col gap-0.5 p-3">
        {navigation.map((item) => {
          const Icon = item.icon;
          return (
            <li key={item.label}>
              {item.status === 'available' ? (
                <NavLink
                  to={item.to}
                  end
                  onClick={onNavigate}
                  className={({ isActive }) =>
                    cx(
                      itemClass,
                      isActive
                        ? 'bg-accent text-accent-foreground'
                        : 'text-foreground hover:bg-muted',
                    )
                  }
                >
                  <Icon aria-hidden="true" className="size-4 shrink-0" />
                  {item.label}
                </NavLink>
              ) : (
                <span aria-disabled="true" className={cx(itemClass, 'text-muted-foreground')}>
                  <Icon aria-hidden="true" className="size-4 shrink-0" />
                  <span className="flex-1">{item.label}</span>
                  <span className="text-[0.6875rem] tracking-wide uppercase">Planned</span>
                </span>
              )}
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
