import { render, screen } from '@testing-library/react';
import CatalogToolbar from '../../../components/catalog/CatalogToolbar';
import type { CatalogCategory } from '../../../types/catalog';

const categories: CatalogCategory[] = [
  { id: 'electronics-id', name: 'Electronics', slug: 'electronics' },
  { id: 'home-id', name: 'Home', slug: 'home' },
  { id: 'accessories-id', name: 'Accessories', slug: 'accessories' },
];

describe('CatalogToolbar', () => {
  it('renders the presentational search, category, and sort controls', () => {
    render(<CatalogToolbar categories={categories} />);

    expect(screen.getByPlaceholderText('Search products...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'All' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Electronics' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Home' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Accessories' })).toBeInTheDocument();
    expect(screen.getByText('Sort by:')).toBeInTheDocument();
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });
});
