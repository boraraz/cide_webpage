import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { HomepageView } from './HomepageView';

describe('HomepageView', () => {
  it('keeps a single image when only the original source exists', () => {
    const html = renderToStaticMarkup(
      <HomepageView
        blocks={[
          {
            id: 'hero',
            type: 'hero',
            config: { heading: 'Train', sources: [{ url: '/api/v1/content/media/1' }] },
          },
        ]}
      />,
    );
    expect(html).toContain('src="/api/v1/content/media/1"');
    expect(html).not.toContain('srcset');
  });
});
