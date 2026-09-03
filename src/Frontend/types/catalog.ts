export type CatalogProductImage = {
  imageUrl: string;
  altText: string;
  displayOrder: number;
};

export type CatalogProduct = {
  id: string;
  sku: string;
  name: string;
  description: string;
  currentPrice: number;
  categoryId: string;
  categoryName: string;
  categorySlug: string;
  images: CatalogProductImage[];
};

export type CatalogCategory = {
  id: string;
  name: string;
  slug: string;
};

export type PaginatedResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};
