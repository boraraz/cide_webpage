import { Container } from '@commerce/ui';
import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { ListingPage } from '@/features/catalog/ListingPage';
import { usePageMetadata } from '@/seo/usePageMetadata';
import { pageMetadata } from '@/seo/metadata';

export default function SearchPage() {
  const [params] = useSearchParams();
  const initial = params.get('q') ?? '';
  usePageMetadata(
    pageMetadata({
      title: initial ? `${initial} · Search` : 'Search · Sportswear',
      description: 'Search the catalog.',
      canonicalPath: initial ? `/search?q=${encodeURIComponent(initial)}` : '/search',
    }),
  );
  return (
    <>
      <SearchForm key={initial} initial={initial} />
      {initial.length >= 2 ? (
        <ListingPage
          title={`Results for “${initial}”`}
          locked={{ q: initial }}
          path={`/search?q=${encodeURIComponent(initial)}`}
        />
      ) : (
        <Container className="py-10 text-sm text-muted-foreground">
          Enter at least two characters.
        </Container>
      )}
    </>
  );
}

function SearchForm({ initial }: { initial: string }) {
  const navigate = useNavigate();
  const [value, setValue] = useState(initial);
  return (
    <Container className="pt-8">
      <form
        role="search"
        onSubmit={(event) => {
          event.preventDefault();
          const query = value.trim();
          navigate(query ? `/search?q=${encodeURIComponent(query)}` : '/search');
        }}
      >
        <label className="flex flex-col gap-2 text-sm">
          Search
          <input
            value={value}
            onChange={(event) => setValue(event.target.value)}
            className="min-h-11 border border-border px-3"
          />
        </label>
        <div className="mt-3 flex gap-4">
          <button type="submit" className="min-h-11 text-sm tracking-editorial uppercase">
            Search
          </button>
          {initial ? (
            <button
              type="button"
              className="min-h-11 text-sm"
              onClick={() => {
                setValue('');
                navigate('/search');
              }}
            >
              Clear
            </button>
          ) : null}
        </div>
      </form>
    </Container>
  );
}
