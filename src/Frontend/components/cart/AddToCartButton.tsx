'use client';

import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addCartItem } from '../../lib/api/orders';
import { getApiErrorMessage } from '../../lib/api/errors';

type AddToCartButtonProps = {
  productId: string;
  quantity?: number;
  className?: string;
};

export default function AddToCartButton({
  productId,
  quantity = 1,
  className = 'mt-auto h-control w-full rounded-button bg-primary py-sm font-label-caps text-label-caps uppercase text-on-primary transition-colors hover:bg-primary-container hover:text-on-primary-container active:scale-[0.98]',
}: AddToCartButtonProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const addItemMutation = useMutation({
    mutationFn: () => addCartItem({ productId, quantity }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cart'] }),
    onError: (error: unknown) => {
      const status = (error as { response?: { status?: number } }).response?.status;
      if (status === 401) {
        const query = searchParams.toString();
        const returnUrl = `${pathname}${query ? `?${query}` : ''}`;
        router.push(`/login?returnUrl=${encodeURIComponent(returnUrl)}`);
      }
    },
  });

  const failureStatus = addItemMutation.isError
    ? (addItemMutation.error as { response?: { status?: number } }).response?.status
    : undefined;
  const showFailure = addItemMutation.isError && failureStatus !== 401;

  return (
    <>
      <button
        className={className}
        type="button"
        onClick={() => addItemMutation.mutate()}
        disabled={addItemMutation.isPending}
      >
        {addItemMutation.isPending ? 'Adding...' : addItemMutation.isSuccess ? 'Added to Cart' : 'Add to Cart'}
      </button>
      {showFailure ? (
        <p className="mt-sm text-center font-status-pill text-status-pill text-status-error" role="alert">
          {getApiErrorMessage(addItemMutation.error, 'We could not add this item to your cart.')}
        </p>
      ) : null}
    </>
  );
}
