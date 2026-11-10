import { cx } from '@commerce/ui';
import { useState } from 'react';
import { env } from '@/lib/env';
import { absoluteMediaUrl, pictureFromSources, type ImageSource } from '@/lib/media';

export function CatalogImage({
  sources,
  alt,
  priority = false,
  sizes = '(min-width: 1280px) 25vw, (min-width: 768px) 33vw, 50vw',
  className,
}: {
  sources: ImageSource[];
  alt: string;
  priority?: boolean;
  sizes?: string;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);
  const picture = pictureFromSources(
    sources.map((source) => ({
      ...source,
      url: absoluteMediaUrl(source.url, env.VITE_API_BASE_URL),
    })),
  );
  if (!picture.src || failed) {
    return (
      <div
        role="img"
        aria-label={alt || 'Image unavailable'}
        className={cx('bg-surface', className)}
      />
    );
  }

  return (
    <img
      src={picture.src}
      srcSet={picture.srcSet}
      sizes={picture.srcSet ? sizes : undefined}
      alt={alt}
      loading={priority ? 'eager' : 'lazy'}
      className={className}
      onError={() => setFailed(true)}
    />
  );
}
