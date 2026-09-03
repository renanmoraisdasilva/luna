import axios from 'axios';
import type {
  CatalogCategory,
  CatalogProduct,
  PaginatedResponse,
} from '../../types/catalog';

const catalogBaseUrl = typeof window === 'undefined'
  ? `${process.env.CATALOG_API_INTERNAL_URL ?? 'http://localhost:5002'}/api/v1/catalog`
  : '/api/services/catalog';

export const catalogApi = axios.create({
  baseURL: catalogBaseUrl,
  headers: { Accept: 'application/json' },
});

export type CatalogProductsParams = {
  search?: string;
  category?: string;
  page: number;
  pageSize: number;
};

export async function getProducts(
  params: CatalogProductsParams,
): Promise<PaginatedResponse<CatalogProduct>> {
  const response = await catalogApi.get<PaginatedResponse<CatalogProduct>>('/products', {
    params: {
      search: params.search || undefined,
      category: params.category || undefined,
      Page: params.page,
      PageSize: params.pageSize,
    },
  });

  return response.data;
}

export async function getCategories(): Promise<PaginatedResponse<CatalogCategory>> {
  const response = await catalogApi.get<PaginatedResponse<CatalogCategory>>('/categories', {
    params: { Page: 1, PageSize: 100 },
  });

  return response.data;
}
