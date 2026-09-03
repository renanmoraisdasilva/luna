import Link from 'next/link';
import Image from 'next/image';
import { formatCurrency } from '../../lib/formatters/currency';
import type { CatalogProduct } from '../../types/catalog';
import AddToCartButton from '../cart/AddToCartButton';

export default function ProductCard({ product }: { product: CatalogProduct }) {
  const image = [...product.images].sort((a, b) => a.displayOrder - b.displayOrder)[0];

  return (
    <article className="group relative flex flex-col overflow-hidden rounded-lg border border-border-standard bg-surface-card transition-shadow duration-200 hover:shadow-lg">
      <div className="relative h-[240px] w-full overflow-hidden rounded-t-lg bg-surface-container">
        {image ? (
          <Image className="h-full w-full rounded-t-lg object-cover transition-transform duration-500 group-hover:scale-105" src={image.imageUrl} alt={image.altText || product.name} width={300} height={300} />
        ) : (
          <div className="flex h-full items-center justify-center font-label-caps text-label-caps uppercase text-on-surface-variant">No image</div>
        )}
      </div>
      <div className="flex flex-grow flex-col p-lg">
        <Link className="mb-xs font-product-title text-product-title text-on-surface hover:text-primary" href={`/shop/products/${product.id}`}>
          {product.name}
        </Link>
        <p className="mb-lg font-body-md text-body-md text-on-surface-variant">{formatCurrency(product.currentPrice)}</p>
        <AddToCartButton productId={product.id} />
      </div>
    </article>
  );
}
