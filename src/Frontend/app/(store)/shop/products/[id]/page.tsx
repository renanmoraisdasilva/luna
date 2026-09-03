import { notFound } from 'next/navigation';
import Link from 'next/link';
import Footer from '../../../../../components/layout/Footer';
import Icon from '../../../../../components/layout/Icon';
import ProductGallery from '../../../../../components/catalog/ProductGallery';
import ProductPurchase from '../../../../../components/catalog/ProductPurchase';
import { getProduct } from '../../../../../lib/api/catalog';

type ProductPageProps = {
  params: Promise<{ id: string }>;
};

export default async function ProductPage({ params }: ProductPageProps) {
  const { id } = await params;
  const product = await getProduct(id);

  if (!product) {
    notFound();
  }

  return (
    <>
      <main className="mx-auto flex min-h-screen w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl">
        <nav aria-label="Breadcrumb" className="flex w-full items-center gap-sm font-label-caps text-label-caps text-on-surface-variant">
          <Link className="transition-colors hover:text-primary" href="/shop">Home</Link>
          <Icon name="chevron-right" className="h-4 w-4" />
          <Link className="transition-colors hover:text-primary" href={`/shop?category=${product.categorySlug}`}>{product.categoryName}</Link>
          <Icon name="chevron-right" className="h-4 w-4" />
          <span className="font-medium text-on-background">{product.name}</span>
        </nav>
        <section className="grid grid-cols-1 gap-xl lg:grid-cols-2 lg:gap-2xl">
          <ProductGallery name={product.name} images={product.images} />
          <ProductPurchase product={product} />
        </section>
        <section className="flex flex-col gap-lg border-t border-outline-variant pt-xl">
          <div className="flex flex-col gap-lg font-body-md text-body-md text-on-surface-variant">
            <p>{product.description}</p>
          </div>
        </section>
      </main>
      <Footer />
    </>
  );
}
