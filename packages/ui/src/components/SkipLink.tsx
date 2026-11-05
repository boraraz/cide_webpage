export interface SkipLinkProps {
  href?: string;
  label?: string;
}

export function SkipLink({ href = '#main', label = 'Skip to content' }: SkipLinkProps) {
  return (
    <a
      href={href}
      className="sr-only z-50 bg-accent px-4 py-2 text-sm text-accent-foreground focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
    >
      {label}
    </a>
  );
}
