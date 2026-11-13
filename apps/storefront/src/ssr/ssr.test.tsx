import { hydrateRoot } from 'react-dom/client';
import { describe, expect, it, vi } from 'vitest';
import { PublicDocument, type SsrPayload } from './PublicDocument';
import { renderDocument } from './entry-server';
import { serializeSsrData } from './serialize';

const product: SsrPayload = {
  kind: 'product',
  title: 'Training tee </script><b>',
  description: 'A "quoted" line & more \u2603',
  canonical: 'https://shop.example/product/training-tee',
  price: '1299.00',
  availability: 'https://schema.org/InStock',
};

describe('ssr serialization', () => {
  it('escapes markup so the payload cannot close the script tag', () => {
    const json = serializeSsrData(product);
    expect(json).not.toContain('</script>');
    expect(json).toContain('\\u003c');
    expect(JSON.parse(json).title).toBe(product.title);
    expect(JSON.parse(json).description).toContain('\u2603');
  });

  it('hydrates the server markup without a mismatch and keeps the captured price', () => {
    const errors: unknown[] = [];
    vi.spyOn(console, 'error').mockImplementation((error) => {
      errors.push(error);
    });
    const first = renderDocument(product);
    const second = renderDocument(product);
    expect(second.body).toBe(first.body);
    const container = document.createElement('div');
    container.innerHTML = first.body;
    hydrateRoot(container, <PublicDocument data={product} />);
    expect(container.textContent).toContain('1299.00');
    expect(container.textContent).not.toContain('1500.00');
    expect(errors.join('\n')).not.toMatch(/did not match|hydration/i);
  });
});
