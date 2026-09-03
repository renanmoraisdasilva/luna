'use client';

import Image from 'next/image';
import { useState } from 'react';
import type { CatalogProductImage } from '../../types/catalog';

export default function ProductGallery({ name, images }: { name: string; images: CatalogProductImage[] }) {
  const orderedImages = [...images].sort((a, b) => a.displayOrder - b.displayOrder);
  const [selectedImage, setSelectedImage] = useState(orderedImages[0]);

  if (!selectedImage) {
    return <div className="flex aspect-[4/3] items-center justify-center rounded-lg border border-outline-variant bg-surface-container-lowest font-label-caps text-label-caps uppercase text-on-surface-variant">No image</div>;
  }

  return (
    <div className="flex flex-col gap-lg">
      <div className="relative aspect-[4/3] w-full overflow-hidden rounded-lg border border-outline-variant bg-surface-container-lowest">
        <Image className="object-cover object-center" src={selectedImage.imageUrl} alt={selectedImage.altText || name} fill priority sizes="(min-width: 1024px) 50vw, 100vw" />
      </div>
      {orderedImages.length > 1 ? (
        <div className="grid grid-cols-4 gap-sm">
          {orderedImages.map((image) => {
            const isSelected = image.imageUrl === selectedImage.imageUrl;

            return (
              <button
                className={`relative aspect-square overflow-hidden rounded-lg border bg-surface-container-lowest transition-all focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 ${isSelected ? 'border-primary' : 'border-outline-variant opacity-70 hover:border-primary hover:opacity-100'}`}
                type="button"
                onClick={() => setSelectedImage(image)}
                aria-label={`View ${image.altText || name}`}
                key={image.imageUrl}
              >
                <Image className="object-cover object-center" src={image.imageUrl} alt="" fill sizes="(min-width: 1024px) 12vw, 25vw" />
              </button>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}
