import { useEffect, useState, type ComponentType } from 'react';

export interface SsrPayload {
  kind: 'home' | 'shop' | 'category' | 'collection' | 'product';
  title: string;
  description: string;
  canonical: string;
  price?: string | null;
  availability?: string | null;
  image?: string | null;
}

export function PublicDocument({ data }: { data: SsrPayload }) {
  const [AppView, setAppView] = useState<ComponentType | null>(null);
  useEffect(() => {
    if (import.meta.env.VITEST) return;
    void import('@/app/App').then((module) => setAppView(() => module.App));
  }, []);
  if (!AppView) {
    return (
      <main>
        <h1>{data.title}</h1>
        <p>{data.description}</p>
        {data.price ? <p>{data.price}</p> : null}
        {data.availability ? <p>{data.availability}</p> : null}
      </main>
    );
  }
  return <AppView />;
}
