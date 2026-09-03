import { fireEvent, render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import type { CatalogCategory, CatalogProduct, PaginatedResponse } from '../../../types/catalog';

const mocks = vi.hoisted(() => ({
  useProductsQuery: vi.fn(),
  useCategoriesQuery: vi.fn(),
  push: vi.fn(),
  query: '',
}));

vi.mock('../../../lib/queries/catalog', () => ({
  CATALOG_PAGE_SIZE: 12,
  useProductsQuery: mocks.useProductsQuery,
  useCategoriesQuery: mocks.useCategoriesQuery,
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/shop',
  useRouter: () => ({ push: mocks.push }),
  useSearchParams: () => new URLSearchParams(mocks.query),
}));

import ShopPage from '../../../app/(store)/shop/page';

const product: CatalogProduct = {
  id: 'keyboard-1',
  sku: 'KB-001',
  name: 'Wireless Mechanical Keyboard',
  description: 'A keyboard.',
  currentPrice: 129,
  categoryId: 'electronics-1',
  categoryName: 'Electronics',
  categorySlug: 'electronics',
  images: [{ imageUrl: 'keyboard.jpg', altText: 'Keyboard', displayOrder: 0 }],
};

const categories: CatalogCategory[] = [{ id: 'electronics-1', name: 'Electronics', slug: 'electronics' }];

function response(items: CatalogProduct[] = [product], totalCount = items.length): PaginatedResponse<CatalogProduct> {
  return { items, page: 1, pageSize: 12, totalCount };
}

function renderShop() {
  mocks.useCategoriesQuery.mockReturnValue({ data: { items: categories } });
  mocks.useProductsQuery.mockReturnValue({ isPending: false, isError: false, data: response(), refetch: vi.fn() });
  render(<ShopPage />);
}

describe('ShopPage', () => {
  beforeEach(() => {
    mocks.push.mockReset();
    mocks.query = '';
  });

  it('renders products returned by the Catalog query', () => {
    renderShop();

    expect(screen.getByRole('heading', { name: 'Shop' })).toBeInTheDocument();
    expect(screen.getByText('Wireless Mechanical Keyboard')).toBeInTheDocument();
    expect(screen.getByText('$129.00')).toBeInTheDocument();
  });

  it('renders loading, error, and empty states', () => {
    mocks.useCategoriesQuery.mockReturnValue({ data: { items: [] } });

    mocks.useProductsQuery.mockReturnValue({ isPending: true, isError: false, data: undefined });
    const { unmount } = render(<ShopPage />);
    expect(screen.getByRole('region', { name: 'Loading products' })).toBeInTheDocument();
    unmount();

    const refetch = vi.fn();
    mocks.useProductsQuery.mockReturnValue({ isPending: false, isError: true, data: undefined, refetch });
    render(<ShopPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(refetch).toHaveBeenCalledOnce();
  });

  it('renders an empty result without treating it as an error', () => {
    mocks.useCategoriesQuery.mockReturnValue({ data: { items: [] } });
    mocks.useProductsQuery.mockReturnValue({ isPending: false, isError: false, data: response([], 0), refetch: vi.fn() });

    render(<ShopPage />);

    expect(screen.getByText('No products found.')).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Pagination' })).not.toBeInTheDocument();
  });

  it('updates search and category in the URL while resetting the page', () => {
    renderShop();

    fireEvent.change(screen.getByPlaceholderText('Search products...'), { target: { value: 'keyboard' } });
    fireEvent.submit(screen.getByPlaceholderText('Search products...'));
    expect(mocks.push).toHaveBeenLastCalledWith('/shop?search=keyboard&page=1');

    fireEvent.click(screen.getByRole('button', { name: 'Electronics' }));
    expect(mocks.push).toHaveBeenLastCalledWith('/shop?category=electronics&page=1');
  });

  it('preserves active filters when changing page', () => {
    mocks.query = 'search=keyboard&category=electronics&page=1';
    mocks.useCategoriesQuery.mockReturnValue({ data: { items: categories } });
    mocks.useProductsQuery.mockReturnValue({ isPending: false, isError: false, data: response([product], 24), refetch: vi.fn() });

    render(<ShopPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));

    expect(mocks.push).toHaveBeenCalledWith('/shop?search=keyboard&category=electronics&page=2');
  });
});
