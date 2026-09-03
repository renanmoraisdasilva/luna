import { createShopUrl } from '../../../lib/catalog/url';

describe('shop URL state', () => {
  it('preserves filters when changing pages', () => {
    expect(createShopUrl('/shop', 'search=keyboard&category=electronics&page=1', { page: '2' }))
      .toBe('/shop?search=keyboard&category=electronics&page=2');
  });

  it('removes empty filters', () => {
    expect(createShopUrl('/shop', 'search=keyboard&category=electronics&page=3', { search: '', category: '', page: '1' }))
      .toBe('/shop?page=1');
  });
});
