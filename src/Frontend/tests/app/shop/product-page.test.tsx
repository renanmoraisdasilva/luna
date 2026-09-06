import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import type { CatalogProduct } from '../../../types/catalog';

const mocks = vi.hoisted(() => ({
  getProduct: vi.fn(),
}));

vi.mock('../../../lib/api/catalog', () => ({
  getProduct: mocks.getProduct,
}));

vi.mock('next/navigation', () => ({
  notFound: vi.fn(() => {
    throw new Error('NOT_FOUND');
  }),
  usePathname: () => '/shop/products/product-1',
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));

import ProductPage from '../../../app/(store)/shop/products/[id]/page';

const product: CatalogProduct = {
  id: 'product-1',
  sku: 'LUNA-001',
  name: 'Wireless Mechanical Keyboard',
  description: 'A low-profile mechanical keyboard.',
  currentPrice: 129,
  categoryId: 'electronics-1',
  categoryName: 'Electronics',
  categorySlug: 'electronics',
  images: [
    { imageUrl: 'keyboard-front.jpg', altText: 'Keyboard front', displayOrder: 0 },
    { imageUrl: 'keyboard-side.jpg', altText: 'Keyboard side', displayOrder: 1 },
  ],
};

describe('ProductPage', () => {
  beforeEach(() => {
    mocks.getProduct.mockReset();
  });

  it('renders the product details and gallery returned by the Catalog API', async () => {
    mocks.getProduct.mockResolvedValue(product);

    const page = await ProductPage({ params: Promise.resolve({ id: product.id }) });
    render(<QueryClientProvider client={new QueryClient()}>{page}</QueryClientProvider>);

    expect(mocks.getProduct).toHaveBeenCalledWith(product.id);
    expect(screen.getByRole('heading', { name: product.name })).toBeInTheDocument();
    expect(screen.getAllByText(product.description)).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'View Keyboard side' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add to Cart' })).toBeInTheDocument();
  });
});
