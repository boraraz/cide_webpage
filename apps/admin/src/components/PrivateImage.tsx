import { useEffect, useState } from 'react';

export function PrivateImage({
  mediaId,
  alt,
  load,
}: {
  mediaId: string;
  alt: string;
  load: (id: string) => Promise<Blob>;
}) {
  const [url, setUrl] = useState<string>();

  useEffect(() => {
    let objectUrl: string | undefined;
    let active = true;
    void load(mediaId).then((blob) => {
      if (!active) return;
      objectUrl = URL.createObjectURL(blob);
      setUrl(objectUrl);
    });
    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [load, mediaId]);

  if (!url) return <div className="size-16 bg-muted" aria-hidden="true" />;
  return <img src={url} alt={alt} className="size-16 object-cover" />;
}
