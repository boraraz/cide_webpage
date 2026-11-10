import { HomepageView } from '@commerce/content';
import { Button } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { content } from '@/lib/api';
import { env } from '@/lib/env';

export default function HomepagePreviewPage() {
  const preview = useQuery({ queryKey: ['homepage-preview'], queryFn: () => content.preview() });
  const [width, setWidth] = useState<'375px' | '1024px'>('1024px');

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold sm:text-2xl">Homepage preview</h1>
        <div className="flex gap-2">
          <Button
            type="button"
            variant={width === '375px' ? 'primary' : 'secondary'}
            onClick={() => setWidth('375px')}
          >
            Mobile
          </Button>
          <Button
            type="button"
            variant={width === '1024px' ? 'primary' : 'secondary'}
            onClick={() => setWidth('1024px')}
          >
            Desktop
          </Button>
        </div>
      </div>
      <div className="overflow-x-auto border border-border bg-background p-4">
        <div style={{ width }} className="mx-auto">
          {preview.data ? (
            <HomepageView
              blocks={preview.data.blocks}
              issues={preview.data.issues}
              resolveSrc={(url) => new URL(url, env.VITE_API_BASE_URL).toString()}
            />
          ) : null}
        </div>
      </div>
    </div>
  );
}
