export type ShopUrlChanges = {
  search?: string;
  category?: string;
  page?: string;
};

export function createShopUrl(pathname: string, currentQuery: string, changes: ShopUrlChanges) {
  const params = new URLSearchParams(currentQuery);

  for (const [key, value] of Object.entries(changes)) {
    if (value) params.set(key, value);
    else params.delete(key);
  }

  const query = params.toString();
  return query ? `${pathname}?${query}` : pathname;
}
