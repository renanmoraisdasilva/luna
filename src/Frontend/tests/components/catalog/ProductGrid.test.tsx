import { render, screen } from '@testing-library/react';
import ProductGrid from '../../../components/catalog/ProductGrid';
import type { CatalogProduct } from '../../../types/catalog';

const product = (id: string, name: string, price: number): CatalogProduct => ({
  id,
  sku: id,
  name,
  description: `${name} description`,
  currentPrice: price,
  categoryId: 'category-1',
  categoryName: 'Electronics',
  categorySlug: 'electronics',
  images: [],
});

describe('ProductGrid', () => {
  it('renders every supplied Catalog product through product cards', () => {
    render(<ProductGrid products={[product('one', 'First Product', 10), product('two', 'Second Product', 20)]} />);

    expect(screen.getByText('First Product')).toBeInTheDocument();
    expect(screen.getByText('Second Product')).toBeInTheDocument();
    expect(screen.getAllByRole('article')).toHaveLength(2);
  });
});
