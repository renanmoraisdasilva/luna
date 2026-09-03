import { fireEvent, render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import type { CatalogCategory, CatalogProduct, PaginatedResponse } from '../../../types/catalog';

const mocks = vi.hoisted(() => ({
  getProducts: vi.fn(),
  getCategories: vi.fn(),
  push: vi.fn(),
  query: '',
}));

vi.mock('../../../lib/api/catalog', () => ({
  getProducts: mocks.getProducts,
  getCategories: mocks.getCategories,
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

async function renderShop(
  searchParams: Record<string, string> = {},
  products = response(),
) {
  mocks.getProducts.mockResolvedValue(products);
  mocks.getCategories.mockResolvedValue({ items: categories, page: 1, pageSize: 100, totalCount: categories.length });
  const page = await ShopPage({ searchParams: Promise.resolve(searchParams) });

  return render(page);
}

describe('ShopPage', () => {
  beforeEach(() => {
    mocks.getProducts.mockReset();
    mocks.getCategories.mockReset();
    mocks.push.mockReset();
    mocks.query = '';
  });

  it('renders products returned by the Catalog API', async () => {
    await renderShop();

    expect(screen.getByRole('heading', { name: 'Shop' })).toBeInTheDocument();
    expect(screen.getByText('Wireless Mechanical Keyboard')).toBeInTheDocument();
    expect(screen.getByText('$129.00')).toBeInTheDocument();
    expect(mocks.getProducts).toHaveBeenCalledWith({ search: '', category: '', page: 1, pageSize: 12 });
  });

  it('renders an empty result without pagination', async () => {
    await renderShop({}, response([], 0));

    expect(screen.getByText('No products found.')).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Pagination' })).not.toBeInTheDocument();
  });

  it('updates search through client-side navigation', async () => {
    await renderShop();

    const searchInput = screen.getByPlaceholderText('Search products...');
    fireEvent.change(searchInput, { target: { value: 'keyboard' } });
    fireEvent.submit(searchInput);

    expect(mocks.push).toHaveBeenLastCalledWith('/shop?search=keyboard&page=1');
  });

  it('preserves active filters in pagination links', async () => {
    mocks.query = 'search=keyboard&category=electronics&page=1';
    await renderShop({ search: 'keyboard', category: 'electronics', page: '1' }, { ...response([product], 24), page: 1 });

    expect(screen.getByRole('link', { name: 'Next' })).toHaveAttribute('href', '/shop?search=keyboard&category=electronics&page=2');
  });
});
