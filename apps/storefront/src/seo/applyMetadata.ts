import type { PageMetadata } from './metadata';

const MARK = 'data-storefront-meta';

export function applyMetadata(metadata: PageMetadata, origin: string): void {
  document.title = metadata.title;
  setMeta('description', metadata.description);
  setMeta('og:title', metadata.title, 'property');
  setMeta('og:description', metadata.description, 'property');
  setMeta('og:type', metadata.jsonLd ? 'product' : 'website', 'property');
  const canonical = new URL(metadata.canonicalPath, origin).toString();
  setLink('canonical', canonical);
  setMeta('og:url', canonical, 'property');
  if (metadata.image) setMeta('og:image', new URL(metadata.image, origin).toString(), 'property');
  else removeMeta('og:image', 'property');
  const existing = document.head.querySelector('script[data-storefront-jsonld]');
  if (!metadata.jsonLd) {
    existing?.remove();
    return;
  }
  const script = existing ?? document.createElement('script');
  script.setAttribute('type', 'application/ld+json');
  script.setAttribute('data-storefront-jsonld', '');
  script.textContent = JSON.stringify(metadata.jsonLd);
  if (!existing) document.head.append(script);
}

function setMeta(name: string, content: string, attribute: 'name' | 'property' = 'name') {
  const selector = `meta[${attribute}="${name}"][${MARK}]`;
  const element = document.head.querySelector(selector) ?? document.createElement('meta');
  element.setAttribute(attribute, name);
  element.setAttribute('content', content);
  element.setAttribute(MARK, '');
  if (!element.parentElement) document.head.append(element);
}

function removeMeta(name: string, attribute: 'name' | 'property') {
  document.head.querySelector(`meta[${attribute}="${name}"][${MARK}]`)?.remove();
}

function setLink(rel: string, href: string) {
  const selector = `link[rel="${rel}"][${MARK}]`;
  const element = document.head.querySelector(selector) ?? document.createElement('link');
  element.setAttribute('rel', rel);
  element.setAttribute('href', href);
  element.setAttribute(MARK, '');
  if (!element.parentElement) document.head.append(element);
}
