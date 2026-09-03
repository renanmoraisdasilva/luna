import CatalogToolbar from '../../../components/catalog/CatalogToolbar';
import Footer from '../../../components/layout/Footer';
import Pagination from '../../../components/catalog/Pagination';
import ProductGrid from '../../../components/catalog/ProductGrid';
import { getCategories, getProducts } from '../../../lib/api/catalog';
import { CATALOG_PAGE_SIZE } from '../../../lib/catalog/constants';

type ShopPageProps = {
  searchParams?: Promise<{
    search?: string;
    category?: string;
    page?: string;
  }>;
};

export default async function ShopPage({ searchParams }: ShopPageProps) {
  const params = searchParams ? await searchParams : {};
  const search = params.search ?? '';
  const category = params.category ?? '';
  const requestedPage = Number(params.page ?? '1');
  const page = Number.isInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1;
  const currentQuery = new URLSearchParams({
    ...(search ? { search } : {}),
    ...(category ? { category } : {}),
    ...(page > 1 ? { page: String(page) } : {}),
  }).toString();

  const [products, categories] = await Promise.all([
    getProducts({ search, category, page, pageSize: CATALOG_PAGE_SIZE }),
    getCategories(),
  ]);

  return (
    <>
      <main className="mx-auto flex min-h-screen w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl">
        <section className="flex flex-col gap-sm">
          <h1 className="font-headline-lg text-headline-lg-mobile text-on-background md:text-headline-lg">Shop</h1>
          <p className="font-body-md text-body-md text-on-surface-variant">Browse our collection of professional hardware and accessories.</p>
        </section>
        <CatalogToolbar search={search} category={category} categories={categories.items} />
        {products.items.length === 0 ? (
          <section className="flex min-h-[240px] flex-col items-center justify-center rounded-lg border border-dashed border-outline-variant bg-surface-container-lowest px-lg text-center">
            <h2 className="font-product-title text-product-title text-on-surface">No products found.</h2>
            <p className="mt-sm font-body-md text-body-md text-on-surface-variant">Try adjusting your search or category filter.</p>
          </section>
        ) : <ProductGrid products={products.items} />}
        {products.totalCount > 0 ? (
          <Pagination
            page={products.page || page}
            pageSize={products.pageSize || CATALOG_PAGE_SIZE}
            totalCount={products.totalCount}
            pathname="/shop"
            currentQuery={currentQuery}
          />
        ) : null}
      </main>
      <Footer />
    </>
  );
}
