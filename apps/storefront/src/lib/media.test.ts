import { describe, expect, it } from 'vitest';
import { absoluteMediaUrl, pictureFromSources } from './media';

describe('absoluteMediaUrl', () => {
  it('resolves API paths against the API origin and leaves absolute URLs unchanged', () => {
    expect(absoluteMediaUrl('/api/v1/media/1', 'http://localhost:5080')).toBe(
      'http://localhost:5080/api/v1/media/1',
    );
    expect(absoluteMediaUrl('http://cdn.example/a.webp', 'http://localhost:5080')).toBe(
      'http://cdn.example/a.webp',
    );
  });
});

describe('pictureFromSources', () => {
  it('does not emit srcset for a single or widthless source', () => {
    expect(pictureFromSources([{ url: '/original', width: null }])).toEqual({ src: '/original' });
    expect(pictureFromSources([{ url: '', width: 480 }])).toEqual({ src: '' });
  });

  it('emits srcset only for sources with a positive width', () => {
    const picture = pictureFromSources([
      { url: '/original', width: 1200 },
      { url: '/w480', width: 480 },
      { url: '/missing', width: 0 },
    ]);
    expect(picture.srcSet).toBe('/original 1200w, /w480 480w');
    expect(picture.srcSet).not.toContain('/missing');
  });
});
