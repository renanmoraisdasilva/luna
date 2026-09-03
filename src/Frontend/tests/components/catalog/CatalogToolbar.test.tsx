import { render, screen } from '@testing-library/react';
import CatalogToolbar from '../../../components/catalog/CatalogToolbar';
import type { CatalogCategory } from '../../../types/catalog';

const categories: CatalogCategory[] = [
  { id: 'electronics-id', name: 'Electronics', slug: 'electronics' },
  { id: 'home-id', name: 'Home', slug: 'home' },
  { id: 'accessories-id', name: 'Accessories', slug: 'accessories' },
];

describe('CatalogToolbar', () => {
  it('renders the search, category links, and sort controls', () => {
    render(<CatalogToolbar categories={categories} />);

    expect(screen.getByPlaceholderText('Search products...')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'All' })).toHaveAttribute('href', '/shop?page=1');
    expect(screen.getByRole('link', { name: 'Electronics' })).toHaveAttribute('href', '/shop?category=electronics&page=1');
    expect(screen.getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/shop?category=home&page=1');
    expect(screen.getByRole('link', { name: 'Accessories' })).toHaveAttribute('href', '/shop?category=accessories&page=1');
  });
});
