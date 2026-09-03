import type { CatalogProduct } from '../../types/catalog';
import ProductCard from './ProductCard';

export default function ProductGrid({ products }: { products: CatalogProduct[] }) {
  return (
    <section className="grid grid-cols-1 gap-lg sm:grid-cols-2 lg:grid-cols-4">
      {products.map((product) => <ProductCard key={product.id} product={product} />)}
    </section>
  );
}
