import type { ApiClient } from './client';
import type { CsrfMemory } from './csrf';
import { isApiError } from './errors';

export interface HomepageBlock {
  id: string;
  type: 'hero' | 'featuredProducts' | 'featuredCollection' | 'editorial' | 'banner' | 'spacer';
  visible: boolean;
  config: Record<string, unknown>;
}

export interface HomepageDraft {
  draftVersion: number;
  draftRevisionId: string | null;
  publishedRevisionId: string | null;
  publishedAt: string | null;
  configuration: { blocks: HomepageBlock[] };
}

export interface PublicHomepage {
  publishedAt: string | null;
  blocks: Array<{ id: string; type: string; config: Record<string, unknown> }>;
}

export interface HomepagePreview {
  publishedAt: string | null;
  blocks: PublicHomepage['blocks'];
  issues: Array<{ blockId: string; message: string }>;
}

export interface RevisionSummary {
  id: string;
  version: number;
  createdAt: string;
  createdByUserId: string;
}

export interface ContentApi {
  draft: () => Promise<HomepageDraft>;
  saveDraft: (configuration: string, expectedVersion: number) => Promise<HomepageDraft>;
  publish: (expectedVersion: number) => Promise<HomepageDraft>;
  preview: () => Promise<HomepagePreview>;
  revisions: () => Promise<RevisionSummary[]>;
  restore: (revisionId: string, expectedVersion: number) => Promise<HomepageDraft>;
  upload: (
    file: Blob,
    fileName: string,
    altText: string,
  ) => Promise<{ id: string; altText: string }>;
  adminMediaBlob: (id: string) => Promise<Blob>;
  publicHomepage: () => Promise<PublicHomepage>;
}

export function createContentApi(client: ApiClient, csrf: CsrfMemory): ContentApi {
  async function send<T>(method: 'post' | 'put', path: string, body?: unknown): Promise<T> {
    await csrf.ensure(client);
    const run = () => (method === 'post' ? client.post<T>(path, body) : client.put<T>(path, body));
    try {
      return await run();
    } catch (error) {
      if (!(
        isApiError(error) &&
        error.status === 400 &&
        error.title === 'The request could not be validated.'
      )) {
        throw error;
      }
      csrf.clear();
      await csrf.ensure(client);
      return run();
    }
  }

  return {
    draft: () => client.get('/api/v1/admin/content/homepage'),
    saveDraft: (configuration, expectedVersion) =>
      send('put', '/api/v1/admin/content/homepage', { configuration, expectedVersion }),
    publish: (expectedVersion) =>
      send('post', '/api/v1/admin/content/homepage/publish', { expectedVersion }),
    preview: () => client.get('/api/v1/admin/content/homepage/preview'),
    revisions: () => client.get('/api/v1/admin/content/homepage/revisions'),
    restore: (revisionId, expectedVersion) =>
      send('post', `/api/v1/admin/content/homepage/revisions/${revisionId}/restore`, {
        expectedVersion,
      }),
    upload: (file, fileName, altText) => {
      const form = new FormData();
      form.set('file', file, fileName);
      form.set('altText', altText);
      return send('post', '/api/v1/admin/content/media', form);
    },
    adminMediaBlob: (id) => client.blob(`/api/v1/admin/content/media/${id}`),
    publicHomepage: () => client.get('/api/v1/content/homepage'),
  };
}
