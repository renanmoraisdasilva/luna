import { vi } from 'vitest';
import { catalogApi, getCategories, getProduct, getProducts } from '../../../lib/api/catalog';

describe('Catalog API client', () => {
  it('maps storefront product parameters to the Catalog contract', async () => {
    const get = vi.spyOn(catalogApi, 'get').mockResolvedValueOnce({ data: { items: [] } });

    await getProducts({ search: 'keyboard', category: 'electronics', page: 2, pageSize: 12 });

    expect(get).toHaveBeenCalledWith('/products', {
      params: { search: 'keyboard', category: 'electronics', Page: 2, PageSize: 12 },
    });
  });

  it('requests the first page of categories', async () => {
    const get = vi.spyOn(catalogApi, 'get').mockResolvedValueOnce({ data: { items: [] } });

    await getCategories();

    expect(get).toHaveBeenCalledWith('/categories', { params: { Page: 1, PageSize: 100 } });
  });

  it('requests a product by id', async () => {
    const get = vi.spyOn(catalogApi, 'get').mockResolvedValueOnce({ data: { id: 'product-1' } });

    await getProduct('product-1');

    expect(get).toHaveBeenCalledWith('/products/product-1');
  });

  it('returns null when a product does not exist', async () => {
    vi.spyOn(catalogApi, 'get').mockRejectedValueOnce({
      isAxiosError: true,
      response: { status: 404 },
    });

    await expect(getProduct('missing-product')).resolves.toBeNull();
  });

  it('rethrows non-404 product errors', async () => {
    const error = new Error('Catalog unavailable');
    vi.spyOn(catalogApi, 'get').mockRejectedValueOnce(error);

    await expect(getProduct('product-1')).rejects.toBe(error);
  });

  it('omits empty optional product filters', async () => {
    const get = vi.spyOn(catalogApi, 'get').mockResolvedValueOnce({ data: { items: [] } });

    await getProducts({ search: '', category: '', page: 1, pageSize: 12 });

    expect(get).toHaveBeenCalledWith('/products', {
      params: { search: undefined, category: undefined, Page: 1, PageSize: 12 },
    });
  });
});
