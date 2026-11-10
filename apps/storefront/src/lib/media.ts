export function absoluteMediaUrl(url: string, base: string) {
  if (!url || /^https?:\/\//i.test(url)) return url;
  return new URL(url, base).toString();
}

export interface ImageSource {
  url: string;
  width?: number | string | null;
}

function positiveWidth(width: ImageSource['width']) {
  const value = typeof width === 'number' ? width : Number(width);
  return Number.isFinite(value) && value > 0 ? value : 0;
}

export function pictureFromSources(sources: ImageSource[]): { src: string; srcSet?: string } {
  const usable = sources.flatMap((source) => {
    const width = positiveWidth(source.width);
    return source.url && width > 0 ? [{ url: source.url, width }] : [];
  });
  const first = sources.find((source) => source.url);
  if (!first) return { src: '' };
  if (usable.length < 2) return { src: first.url };
  return {
    src: usable[usable.length - 1]?.url ?? first.url,
    srcSet: usable.map((source) => `${source.url} ${source.width}w`).join(', '),
  };
}
