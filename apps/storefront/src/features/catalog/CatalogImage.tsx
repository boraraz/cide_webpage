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
  onFailure,
}: {
  sources: ImageSource[];
  alt: string;
  priority?: boolean;
  sizes?: string;
  className?: string;
  onFailure?: () => void;
}) {
  const [failedSrc, setFailedSrc] = useState('');
  const picture = pictureFromSources(
    sources.map((source) => ({
      ...source,
      url: absoluteMediaUrl(source.url, env.VITE_API_BASE_URL),
    })),
  );
  if (!picture.src || failedSrc === picture.src) {
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
      onError={() => {
        setFailedSrc(picture.src);
        onFailure?.();
      }}
    />
  );
}
