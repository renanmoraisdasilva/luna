'use client';

import { useState } from 'react';
import Icon from '../layout/Icon';
import AddToCartButton from '../cart/AddToCartButton';
import type { CatalogProduct } from '../../types/catalog';
import { formatCurrency } from '../../lib/formatters/currency';

export default function ProductPurchase({ product }: { product: CatalogProduct }) {
  const [quantity, setQuantity] = useState(1);

  return (
    <div className="flex flex-col gap-xl">
      <div className="flex flex-col gap-sm">
        <span className="inline-flex w-max items-center gap-xs rounded-full border border-outline-variant bg-surface-container-low px-md py-xs font-status-pill text-status-pill text-status-success">
          <span className="h-[8px] w-[8px] rounded-full bg-status-success" />
          Available
        </span>
        <h1 className="font-display text-display text-on-background">{product.name}</h1>
        <p className="font-headline-lg text-headline-lg text-on-surface-variant">{formatCurrency(product.currentPrice)}</p>
      </div>
      <p className="border-b border-outline-variant pb-xl font-body-md text-body-md text-on-surface-variant">{product.description}</p>
      <div className="flex flex-row gap-lg pt-sm">
        <div className="flex h-control w-max items-center rounded-button border border-outline-variant">
          <button className="flex h-full items-center justify-center border-r border-outline-variant px-md text-on-surface transition-colors hover:bg-surface-container-low" type="button" onClick={() => setQuantity((value) => Math.max(1, value - 1))} aria-label="Decrease quantity">
            <Icon name="remove" />
          </button>
          <span className="flex h-full w-[64px] items-center justify-center px-xl text-center font-body-md text-body-md text-on-surface">{quantity}</span>
          <button className="flex h-full items-center justify-center border-l border-outline-variant px-md text-on-surface transition-colors hover:bg-surface-container-low" type="button" onClick={() => setQuantity((value) => value + 1)} aria-label="Increase quantity">
            <Icon name="add" />
          </button>
        </div>
        <AddToCartButton
          productId={product.id}
          quantity={quantity}
          className="flex h-control min-w-0 flex-1 items-center justify-center gap-sm rounded-button bg-primary py-sm font-label-caps text-label-caps uppercase text-on-primary transition-colors hover:bg-primary-container hover:text-on-primary-container active:scale-[0.98]"
        />
      </div>
    </div>
  );
}
