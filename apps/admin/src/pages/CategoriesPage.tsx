import type { CategoryNode } from '@commerce/shared-types';
import { Button, Select, TextField } from '@commerce/ui';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { catalog } from '@/lib/api';
import { problemMessage } from '@/lib/problem';

function flatten(nodes: CategoryNode[], depth = 0): Array<CategoryNode & { depth: number }> {
  return nodes.flatMap((node) => [{ ...node, depth }, ...flatten(node.children ?? [], depth + 1)]);
}

function blockedParents(nodes: CategoryNode[], id: string): Set<string> {
  const blocked = new Set<string>([id]);
  const visit = (items: CategoryNode[], inside: boolean) => {
    for (const item of items) {
      const next = inside || item.id === id;
      if (next) blocked.add(item.id);
      visit(item.children ?? [], next);
    }
  };
  visit(nodes, false);
  return blocked;
}

export default function CategoriesPage() {
  const queryClient = useQueryClient();
  const categories = useQuery({
    queryKey: ['categories'],
    queryFn: () => catalog.adminCategories(),
  });
  const [message, setMessage] = useState<string>();
  const rows = flatten(categories.data ?? []);
  const create = useMutation({
    mutationFn: (form: FormData) =>
      catalog.createCategory({
        name: String(form.get('name') ?? ''),
        slug: String(form.get('slug') ?? '') || null,
        parentCategoryId: String(form.get('parentCategoryId') ?? '') || null,
        description: null,
        displayOrder: 0,
        isActive: true,
      }),
    onSuccess: async () => {
      setMessage('Category saved.');
      await queryClient.invalidateQueries({ queryKey: ['categories'] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });

  return (
    <div className="flex max-w-3xl flex-col gap-4">
      <h1 className="text-xl font-semibold sm:text-2xl">Categories</h1>
      {message ? <p role="status">{message}</p> : null}
      <form
        className="grid gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          create.mutate(new FormData(event.currentTarget));
        }}
      >
        <TextField name="name" label="Name" required />
        <TextField name="slug" label="Slug" />
        <Select name="parentCategoryId" label="Parent" defaultValue="">
          <option value="">No parent</option>
          {rows.map((row) => (
            <option key={row.id} value={row.id}>
              {`${'— '.repeat(row.depth)}${row.name}`}
            </option>
          ))}
        </Select>
        <Button type="submit">Save category</Button>
      </form>
      <ul>
        {rows.map((row) => (
          <li
            key={row.id}
            className="flex items-center justify-between gap-3 border-t border-border py-2"
          >
            <span style={{ paddingLeft: row.depth * 16 }}>{row.name}</span>
            <Button
              variant="secondary"
              onClick={() => {
                const parent = window.prompt('Parent category id, or blank to clear', '') ?? '';
                const blocked = blockedParents(categories.data ?? [], row.id);
                if (parent && blocked.has(parent)) {
                  setMessage('Choose a parent that is not this category or one of its children.');
                  return;
                }
                void catalog
                  .updateCategory(row.id, {
                    name: null,
                    slug: null,
                    description: null,
                    parentCategoryId: parent || null,
                    clearParent: parent.length === 0,
                  })
                  .then(() => queryClient.invalidateQueries({ queryKey: ['categories'] }))
                  .catch((error: unknown) => setMessage(problemMessage(error)));
              }}
            >
              Change parent
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}
