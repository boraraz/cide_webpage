import { isApiError } from '@commerce/api-client';
import { Container } from '@commerce/ui';
import { useQuery } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import { ListingPage } from '@/features/catalog/ListingPage';
import { catalog } from '@/lib/api';
import { NotFoundPage } from './NotFoundPage';

export default function CollectionPage() {
  const { slug = '' } = useParams();
  const navigate = useNavigate();
  const collection = useQuery({
    queryKey: ['collection', slug],
    queryFn: () => catalog.collection(slug),
    enabled: slug.length > 0,
  });
  useEffect(() => {
    if (collection.data && collection.data.slug !== slug) {
      navigate(`/collection/${collection.data.slug}${window.location.search}`, { replace: true });
    }
  }, [collection.data, navigate, slug]);
  if (isApiError(collection.error) && collection.error.status === 404) return <NotFoundPage />;
  if (!collection.data) return <Container className="py-16">Loading</Container>;
  return (
    <ListingPage
      title={collection.data.name}
      description={collection.data.description}
      locked={{ collection: collection.data.slug }}
      path={`/collection/${collection.data.slug}`}
    />
  );
}
