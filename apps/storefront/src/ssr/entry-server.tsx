import { renderToString } from 'react-dom/server';
import { PublicDocument, type SsrPayload } from './PublicDocument';
import { serializeSsrData } from './serialize';

export function renderDocument(data: SsrPayload) {
  return {
    body: renderToString(<PublicDocument data={data} />),
    data: serializeSsrData(data),
  };
}
