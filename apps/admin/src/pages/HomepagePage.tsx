import type { HomepageBlock } from '@commerce/api-client';
import { Button, Checkbox, Dialog, Select, TextField } from '@commerce/ui';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, useBlocker } from 'react-router';
import { content } from '@/lib/api';
import { problemMessage } from '@/lib/problem';

const types: HomepageBlock['type'][] = [
  'hero',
  'featuredProducts',
  'featuredCollection',
  'editorial',
  'banner',
  'spacer',
];

function emptyBlock(type: HomepageBlock['type']): HomepageBlock {
  const id = crypto.randomUUID();
  const config: Record<string, unknown> =
    type === 'spacer'
      ? { size: 'md' }
      : type === 'featuredProducts'
        ? { heading: 'Featured', productIds: [] }
        : type === 'featuredCollection'
          ? { heading: 'Collection', collectionId: '' }
          : {
              heading: 'Heading',
              body: type === 'hero' ? undefined : 'Body',
              mediaId: '',
              destination: '/',
            };
  return { id, type, visible: true, config };
}

export default function HomepagePage() {
  const queryClient = useQueryClient();
  const draft = useQuery({ queryKey: ['homepage'], queryFn: () => content.draft() });
  const revisions = useQuery({
    queryKey: ['homepage-revisions'],
    queryFn: () => content.revisions(),
  });
  const [edited, setEdited] = useState<HomepageBlock[] | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [message, setMessage] = useState<string>();
  const dirty = edited !== null;
  const blocks = edited ?? draft.data?.configuration.blocks ?? [];
  const version = draft.data?.draftVersion ?? 0;
  const blocker = useBlocker(dirty);

  function replace(next: HomepageBlock[] | ((current: HomepageBlock[]) => HomepageBlock[])) {
    setEdited((current) => {
      const base = current ?? draft.data?.configuration.blocks ?? [];
      return typeof next === 'function' ? next(base) : next;
    });
  }

  const save = useMutation({
    mutationFn: () => content.saveDraft(JSON.stringify({ blocks }), version),
    onSuccess: (saved) => {
      queryClient.setQueryData(['homepage'], saved);
      setEdited(null);
      setMessage('Draft saved. Publish uses this version.');
      void queryClient.invalidateQueries({ queryKey: ['homepage-revisions'] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });

  const publish = useMutation({
    mutationFn: () => content.publish(version),
    onSuccess: (saved) => {
      setConfirming(false);
      setMessage('Homepage published.');
      queryClient.setQueryData(['homepage'], saved);
      void queryClient.invalidateQueries({ queryKey: ['homepage-revisions'] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });

  const restore = useMutation({
    mutationFn: (revisionId: string) => content.restore(revisionId, version),
    onSuccess: (saved) => {
      queryClient.setQueryData(['homepage'], saved);
      setEdited(null);
      setMessage('Draft restored. The published homepage is unchanged.');
      void queryClient.invalidateQueries({ queryKey: ['homepage-revisions'] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });

  function patch(blockId: string, config: Record<string, unknown>) {
    replace((current) =>
      current.map((item) =>
        item.id === blockId ? { ...item, config: { ...item.config, ...config } } : item,
      ),
    );
  }

  function move(index: number, direction: -1 | 1) {
    const next = [...blocks];
    const target = index + direction;
    const current = next[index];
    const other = next[target];
    if (!current || !other) return;
    next[index] = other;
    next[target] = current;
    replace(next);
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold sm:text-2xl">Homepage</h1>
          <p className="text-sm text-muted-foreground">
            Draft version {version}. Saving does not publish.
          </p>
        </div>
        <Link to="/content/homepage/preview">Preview</Link>
      </div>
      {message ? <p role="status">{message}</p> : null}
      <div className="flex flex-wrap gap-2">
        {types.map((type) => (
          <Button
            key={type}
            variant="secondary"
            onClick={() => {
              replace((current) => [...current, emptyBlock(type)]);
            }}
          >
            Add {type}
          </Button>
        ))}
      </div>
      <ol className="flex flex-col gap-4">
        {blocks.map((block, index) => (
          <li key={block.id} className="border border-border p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <strong>{block.type}</strong>
              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="secondary"
                  aria-label={`Move block ${index + 1} up`}
                  onClick={() => move(index, -1)}
                >
                  Up
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  aria-label={`Move block ${index + 1} down`}
                  onClick={() => move(index, 1)}
                >
                  Down
                </Button>
              </div>
            </div>
            <Checkbox
              label="Visible"
              checked={block.visible}
              onChange={(event) => {
                replace((current) =>
                  current.map((item) =>
                    item.id === block.id ? { ...item, visible: event.target.checked } : item,
                  ),
                );
              }}
            />
            {block.type === 'spacer' ? (
              <Select
                label="Size"
                value={String(block.config.size ?? 'md')}
                onChange={(event) => {
                  replace((current) =>
                    current.map((item) =>
                      item.id === block.id
                        ? { ...item, config: { ...item.config, size: event.target.value } }
                        : item,
                    ),
                  );
                }}
              >
                <option value="sm">Small</option>
                <option value="md">Medium</option>
                <option value="lg">Large</option>
              </Select>
            ) : (
              <div className="grid gap-3">
                <TextField
                  label="Heading"
                  value={String(block.config.heading ?? '')}
                  onChange={(event) => patch(block.id, { heading: event.target.value })}
                />
                {block.type === 'editorial' || block.type === 'banner' ? (
                  <TextField
                    label="Body"
                    value={String(block.config.body ?? '')}
                    onChange={(event) => patch(block.id, { body: event.target.value })}
                  />
                ) : null}
                {block.type === 'hero' || block.type === 'editorial' || block.type === 'banner' ? (
                  <>
                    <TextField
                      label="Image id"
                      value={String(block.config.mediaId ?? '')}
                      onChange={(event) => patch(block.id, { mediaId: event.target.value })}
                    />
                    <TextField
                      label="Destination"
                      value={String(block.config.destination ?? '')}
                      onChange={(event) => patch(block.id, { destination: event.target.value })}
                    />
                  </>
                ) : null}
                {block.type === 'featuredProducts' ? (
                  <TextField
                    label="Product ids"
                    value={
                      Array.isArray(block.config.productIds)
                        ? block.config.productIds.join(', ')
                        : ''
                    }
                    onChange={(event) =>
                      patch(block.id, {
                        productIds: event.target.value
                          .split(',')
                          .map((value) => value.trim())
                          .filter(Boolean),
                      })
                    }
                  />
                ) : null}
                {block.type === 'featuredCollection' ? (
                  <TextField
                    label="Collection id"
                    value={String(block.config.collectionId ?? '')}
                    onChange={(event) => patch(block.id, { collectionId: event.target.value })}
                  />
                ) : null}
              </div>
            )}
          </li>
        ))}
      </ol>
      <div className="flex gap-2">
        <Button type="button" onClick={() => save.mutate()} loading={save.isPending}>
          Save draft
        </Button>
        <Button type="button" variant="secondary" onClick={() => setConfirming(true)}>
          Publish
        </Button>
      </div>
      <Dialog
        open={confirming}
        title="Publish homepage?"
        confirmLabel="Publish"
        onClose={() => setConfirming(false)}
        onConfirm={() => publish.mutate()}
      >
        The public homepage will use the latest saved draft, version {version}.
      </Dialog>
      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Revision history</h2>
        <ul className="flex flex-col gap-2">
          {(revisions.data ?? []).map((revision) => (
            <li
              key={revision.id}
              className="flex flex-wrap items-center justify-between gap-2 border border-border p-3"
            >
              <span>
                Version {revision.version}
                {revision.id === draft.data?.publishedRevisionId ? ' · published' : ''}
              </span>
              <Button
                type="button"
                variant="secondary"
                onClick={() => restore.mutate(revision.id)}
                loading={restore.isPending}
              >
                Restore as draft
              </Button>
            </li>
          ))}
        </ul>
      </section>
      <Dialog
        open={blocker.state === 'blocked'}
        title="Leave without saving?"
        confirmLabel="Leave"
        onClose={() => blocker.reset?.()}
        onConfirm={() => blocker.proceed?.()}
      >
        This homepage draft has unsaved changes.
      </Dialog>
    </div>
  );
}
