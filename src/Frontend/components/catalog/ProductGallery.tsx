import type { CatalogProductImage } from '../../types/catalog';
import ProductImageSelector from './ProductImageSelector';

export default function ProductGallery({ name, images }: { name: string; images: CatalogProductImage[] }) {
  const orderedImages = [...images].sort((a, b) => a.displayOrder - b.displayOrder);

  if (!orderedImages[0]) {
    return <div className="flex aspect-[4/3] items-center justify-center rounded-lg border border-outline-variant bg-surface-container-lowest font-label-caps text-label-caps uppercase text-on-surface-variant">No image</div>;
  }

  return <ProductImageSelector name={name} images={orderedImages} />;
}
