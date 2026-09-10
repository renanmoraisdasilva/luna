import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ProductCard from '../../../components/catalog/ProductCard';
import type { CatalogProduct } from '../../../types/catalog';

const product: CatalogProduct = {
  id: 'keyboard-1',
  sku: 'KB-001',
  name: 'Orbit Keyboard',
  description: 'A mechanical keyboard.',
  currentPrice: 129,
  categoryId: 'electronics-1',
  categoryName: 'Electronics',
  categorySlug: 'electronics',
  images: [
    { imageUrl: 'second.jpg', altText: 'Second image', displayOrder: 2 },
    { imageUrl: 'keyboard.jpg', altText: 'Orbit mechanical keyboard', displayOrder: 1 },
  ],
};

describe('ProductCard', () => {
  it('renders Catalog data, formats the price, and uses the first ordered image', () => {
    const queryClient = new QueryClient();
    render(<QueryClientProvider client={queryClient}><ProductCard product={product} /></QueryClientProvider>);

    expect(screen.getByText('Orbit Keyboard')).toBeInTheDocument();
    expect(screen.getByText('$129.00')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Orbit mechanical keyboard' })).toHaveAttribute('src', 'keyboard.jpg');
    expect(screen.getByRole('link', { name: 'Orbit Keyboard' })).toHaveAttribute('href', '/shop/products/keyboard-1');
    expect(screen.queryByText('Available')).not.toBeInTheDocument();
  });
  
  it('renders a fallback when the product has no images', () => {
    render(<QueryClientProvider client={new QueryClient()}><ProductCard product={{ ...product, images: [] }} /></QueryClientProvider>);
    
    expect(screen.getByText('No image')).toBeInTheDocument();
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('uses the product name when an image has no alt text', () => {
    render(<QueryClientProvider client={new QueryClient()}><ProductCard product={{ ...product, images: [{ ...product.images[0], altText: '' }] }} /></QueryClientProvider>);

    expect(screen.getByRole('img', { name: product.name })).toBeInTheDocument();
  });
});
