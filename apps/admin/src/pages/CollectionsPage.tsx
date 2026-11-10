import { Button, TextField } from '@commerce/ui';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { catalog } from '@/lib/api';
import { problemMessage } from '@/lib/problem';

export default function CollectionsPage() {
  const queryClient = useQueryClient();
  const collections = useQuery({
    queryKey: ['collections'],
    queryFn: () => catalog.adminCollections(),
  });
  const [message, setMessage] = useState<string>();
  const create = useMutation({
    mutationFn: (form: FormData) =>
      catalog.createCollection({
        name: String(form.get('name') ?? ''),
        slug: String(form.get('slug') ?? '') || null,
        description: String(form.get('description') ?? '') || null,
        displayOrder: 0,
        isActive: true,
      }),
    onSuccess: async () => {
      setMessage('Collection saved.');
      await queryClient.invalidateQueries({ queryKey: ['collections'] });
    },
    onError: (error) => setMessage(problemMessage(error)),
  });

  return (
    <div className="flex max-w-3xl flex-col gap-4">
      <h1 className="text-xl font-semibold sm:text-2xl">Collections</h1>
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
        <TextField name="description" label="Description" />
        <Button type="submit">Save collection</Button>
      </form>
      <ul>
        {collections.data?.map((collection) => (
          <li key={collection.id} className="border-t border-border py-2">
            {collection.name}
          </li>
        ))}
      </ul>
    </div>
  );
}
