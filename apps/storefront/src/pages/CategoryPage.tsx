import { useParams } from 'react-router';
import { CategoryListing } from '@/features/catalog/ListingPage';
import { NotFoundPage } from './NotFoundPage';

export default function CategoryPage() {
  const { slug } = useParams();
  if (!slug) return <NotFoundPage />;
  return <CategoryListing slug={slug} />;
}
