import { createReadStream, existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const clientDir = join(root, 'dist/client');
const api = (process.env.API_INTERNAL_URL ?? 'http://127.0.0.1:5080').replace(/\/$/, '');
const origin = (process.env.STOREFRONT_ORIGIN ?? 'http://127.0.0.1:4173').replace(/\/$/, '');
const apiPublic = (process.env.PUBLIC_API_ORIGIN ?? '').replace(/\/$/, '');
const port = Number(process.env.PORT ?? 4173);
const { renderDocument } = await import(join(root, 'dist/server/entry-server.js'));

const publicPaths = new Set(['/', '/shop']);

function isPublic(pathname) {
  return publicPaths.has(pathname) || /^\/(product|category|collection)\//.test(pathname);
}

function headers(extra = {}) {
  const remote = apiPublic && apiPublic !== origin ? ` ${apiPublic}` : '';
  return {
    'Cache-Control': 'private, no-store',
    'X-Content-Type-Options': 'nosniff',
    'X-Frame-Options': 'DENY',
    'Referrer-Policy': 'strict-origin-when-cross-origin',
    'Permissions-Policy': 'camera=(), microphone=(), geolocation=()',
    'Content-Security-Policy': [
      "default-src 'self'",
      "base-uri 'self'",
      "object-src 'none'",
      "frame-ancestors 'none'",
      "form-action 'self'",
      "script-src 'self'",
      "style-src 'self' 'unsafe-inline'",
      "style-src-attr 'unsafe-inline'",
      `img-src 'self' data:${remote}`,
      `connect-src 'self'${remote}`,
      "font-src 'self'",
    ].join('; '),
    ...extra,
  };
}

async function loadPublic(pathname) {
  const path = pathname === '/' ? '/api/v1/content/homepage' : apiPath(pathname);
  const response = await fetch(`${api}${path}`, { headers: { accept: 'application/json' } });
  if (response.status === 404) return { status: 404 };
  if (!response.ok) return { status: 503 };
  const body = await response.json();
  return { status: 200, payload: toPayload(pathname, body) };
}

function apiPath(pathname) {
  if (pathname === '/shop') return '/api/v1/products?page=1&pageSize=1';
  const [, kind, slug] = pathname.split('/');
  if (kind === 'product') return `/api/v1/products/${encodeURIComponent(slug)}`;
  if (kind === 'category') return `/api/v1/categories/${encodeURIComponent(slug)}`;
  return `/api/v1/collections/${encodeURIComponent(slug)}`;
}

function toPayload(pathname, body) {
  if (pathname.startsWith('/product/')) {
    const requested = decodeURIComponent(pathname.split('/')[2] ?? '');
    return {
      redirect: body.slug && body.slug !== requested ? `/product/${body.slug}` : null,
      kind: 'product',
      title: body.name,
      description: body.shortDescription ?? '',
      canonical: `${origin}/product/${body.slug}`,
      price: body.fromPrice ?? null,
      availability: body.variants?.some((item) => item.available)
        ? 'https://schema.org/InStock'
        : 'https://schema.org/OutOfStock',
      image: absoluteUrl(body.media?.[0]?.sources?.find((source) => source.url)?.url ?? null),
      crumbs: productCrumbs(body),
    };
  }
  const title = body.name ?? (pathname === '/' ? 'Sportswear' : 'Shop');
  return {
    redirect: null,
    kind: pathname === '/' ? 'home' : pathname === '/shop' ? 'shop' : pathname.split('/')[1],
    title,
    description: body.description ?? body.shortDescription ?? 'Premium sportswear.',
    canonical: `${origin}${pathname === '/' ? '/' : pathname}`,
    price: null,
    availability: null,
    image: null,
    crumbs: pathname.startsWith('/category/')
      ? [
          { name: 'Home', path: '/' },
          { name: title, path: pathname },
        ]
      : [],
  };
}

function absoluteUrl(url) {
  if (!url) return null;
  if (url.startsWith('http://') || url.startsWith('https://')) return url;
  return `${origin}${url.startsWith('/') ? '' : '/'}${url}`;
}

function productCrumbs(body) {
  const items = [{ name: 'Home', path: '/' }];
  const category = body.categories?.[0];
  if (category?.slug) items.push({ name: category.name, path: `/category/${category.slug}` });
  items.push({ name: body.name, path: `/product/${body.slug}` });
  return items;
}

function page(shell, payload, body) {
  const canonical = `<link rel="canonical" href="${escapeAttr(payload.canonical)}">`;
  const robots = `<meta name="robots" content="index,follow">`;
  const jsonLd = payload.data ? `<script type="application/ld+json">${payload.data}</script>` : '';
  return shell
    .replace('<meta name="robots" content="noindex" />', robots)
    .replace(
      '</head>',
      `${canonical}<title>${escapeText(payload.title)}</title><meta name="description" content="${escapeAttr(payload.description)}">${jsonLd}<script type="application/json" id="ssr-data">${payload.serialized}</script></head>`,
    )
    .replace('<div id="root"></div>', `<div id="root">${body}</div>`);
}

function escapeText(value) {
  return String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

function escapeAttr(value) {
  return escapeText(value).replaceAll('"', '&quot;');
}

const server = createServer(async (request, response) => {
  const url = new URL(request.url ?? '/', `http://127.0.0.1:${port}`);
  if (url.pathname === '/health/ready') {
    try {
      const ready = await fetch(`${api}/api/v1/health/ready`);
      response.writeHead(ready.ok ? 200 : 503, headers({ 'content-type': 'text/plain' }));
      response.end(ready.ok ? 'ready' : 'not-ready');
    } catch {
      response.writeHead(503, headers({ 'content-type': 'text/plain' }));
      response.end('not-ready');
    }
    return;
  }

  const asset = normalize(url.pathname).replace(/^(\.\.[/\\])+/, '');
  const file = join(clientDir, asset);
  if (asset !== '/' && existsSync(file) && extname(file)) {
    response.writeHead(200, {
      'Cache-Control': 'public, max-age=31536000, immutable',
      'content-type': contentType(file),
    });
    createReadStream(file).pipe(response);
    return;
  }

  const shell = await readFile(join(clientDir, 'index.html'), 'utf8');
  if (!isPublic(url.pathname)) {
    response.writeHead(200, headers({ 'content-type': 'text/html; charset=utf-8' }));
    response.end(shell);
    return;
  }

  let loaded;
  try {
    loaded = await loadPublic(url.pathname);
  } catch {
    loaded = { status: 503 };
  }
  if (loaded.status === 503) {
    response.writeHead(503, headers({ 'content-type': 'text/plain; charset=utf-8' }));
    response.end('The catalog is unavailable.');
    return;
  }
  if (loaded.status === 404) {
    response.writeHead(404, headers({ 'content-type': 'text/html; charset=utf-8' }));
    response.end(shell);
    return;
  }
  if (loaded.payload.redirect) {
    response.writeHead(301, headers({ location: loaded.payload.redirect }));
    response.end();
    return;
  }
  const rendered = renderDocument(loaded.payload);
  const jsonLd = structuredData(loaded.payload);
  response.writeHead(200, headers({ 'content-type': 'text/html; charset=utf-8' }));
  response.end(
    page(shell, { ...loaded.payload, serialized: rendered.data, data: jsonLd }, rendered.body),
  );
});

server.listen(port, '0.0.0.0');

function structuredData(payload) {
  const graph = [];
  if (payload.kind === 'product') {
    const product = {
      '@type': 'Product',
      name: payload.title,
      description: payload.description,
      offers: {
        '@type': 'Offer',
        price: payload.price,
        priceCurrency: 'TRY',
        availability: payload.availability,
        url: payload.canonical,
      },
    };
    if (payload.image) product.image = payload.image;
    graph.push(product);
  }
  if (payload.crumbs?.length) {
    graph.push({
      '@type': 'BreadcrumbList',
      itemListElement: payload.crumbs.map((crumb, index) => ({
        '@type': 'ListItem',
        position: index + 1,
        name: crumb.name,
        item: `${origin}${crumb.path === '/' ? '/' : crumb.path}`,
      })),
    });
  }
  if (graph.length === 0) return '';
  return JSON.stringify({ '@context': 'https://schema.org', '@graph': graph }).replaceAll(
    '<',
    '\\u003c',
  );
}

function contentType(file) {
  const extension = extname(file);
  if (extension === '.js') return 'text/javascript';
  if (extension === '.css') return 'text/css';
  if (extension === '.svg') return 'image/svg+xml';
  return 'application/octet-stream';
}
