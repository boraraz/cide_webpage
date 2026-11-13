import { StrictMode } from 'react';
import { createRoot, hydrateRoot } from 'react-dom/client';
import { App } from '@/app/App';
import { PublicDocument, type SsrPayload } from '@/ssr/PublicDocument';
import '@/styles/index.css';

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found.');

const ssrData = document.getElementById('ssr-data')?.textContent;
if (ssrData) {
  hydrateRoot(
    rootElement,
    <StrictMode>
      <PublicDocument data={JSON.parse(ssrData) as SsrPayload} />
    </StrictMode>,
  );
} else {
  createRoot(rootElement).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
}
