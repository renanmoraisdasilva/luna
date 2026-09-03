'use client';

type AddToCartButtonProps = {
  productId: string;
  quantity?: number;
  onAddToCart?: (productId: string, quantity: number) => void;
};

export default function AddToCartButton({
  productId,
  quantity = 1,
  onAddToCart,
}: AddToCartButtonProps) {
  return (
    <button
      className="mt-auto h-control w-full rounded-button bg-primary py-sm font-label-caps text-label-caps uppercase text-on-primary transition-colors hover:bg-primary-container hover:text-on-primary-container active:scale-[0.98]"
      type="button"
      onClick={() => onAddToCart?.(productId, quantity)}
    >
      Add to Cart
    </button>
  );
}
