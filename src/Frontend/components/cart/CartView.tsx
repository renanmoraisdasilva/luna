'use client';

import Image from 'next/image';
import Link from 'next/link';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useCallback, useEffect } from 'react';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import Icon from '../layout/Icon';
import { getProduct } from '../../lib/api/catalog';
import { changeCartItemQuantity, getCart, removeCartItem } from '../../lib/api/orders';
import type { CartItem } from '../../lib/api/orders';
import { formatCurrency } from '../../lib/formatters/currency';

function getErrorStatus(error: unknown): number | undefined {
  if (typeof error !== 'object' || error === null || !('response' in error)) {
    return undefined;
  }

  const response = error.response;
  return typeof response === 'object' && response !== null && 'status' in response && typeof response.status === 'number'
    ? response.status
    : undefined;
}

function CartLoading() {
  return (
    <main className="mx-auto flex min-h-screen w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl" aria-busy="true">
      <div className="h-10 w-48 animate-pulse rounded bg-surface-container" />
      <div className="grid gap-xl lg:grid-cols-[minmax(0,1fr)_380px]">
        <div className="space-y-xl">
          {[1, 2].map((item) => <div className="h-[160px] animate-pulse rounded-lg bg-surface-container" key={item} />)}
        </div>
        <div className="h-[280px] animate-pulse rounded-lg bg-surface-container" />
      </div>
    </main>
  );
}

function getReturnUrl(pathname: string, searchParams: URLSearchParams): string {
  const query = searchParams.toString();
  return `${pathname}${query ? `?${query}` : ''}`;
}

export default function CartView() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const cartQuery = useQuery({
    queryKey: ['cart'],
    queryFn: getCart,
    retry: false,
  });
  const productQueries = useQueries({
    queries: (cartQuery.data?.items ?? []).map((item) => ({
      queryKey: ['product', item.productId],
      queryFn: () => getProduct(item.productId),
      staleTime: 60_000,
      retry: false,
    })),
  });

  const redirectToLogin = useCallback(() => {
    router.replace(`/login?returnUrl=${encodeURIComponent(getReturnUrl(pathname, searchParams))}`);
  }, [pathname, router, searchParams]);

  useEffect(() => {
    if (getErrorStatus(cartQuery.error) === 401) {
      redirectToLogin();
    }
  }, [cartQuery.error, redirectToLogin]);

  const quantityMutation = useMutation({
    mutationFn: ({ productId, quantity }: { productId: string; quantity: number }) => changeCartItemQuantity(productId, quantity),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cart'] }),
    onError: (error) => {
      if (getErrorStatus(error) === 401) {
        redirectToLogin();
      }
    },
  });
  const removeMutation = useMutation({
    mutationFn: removeCartItem,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cart'] }),
    onError: (error) => {
      if (getErrorStatus(error) === 401) {
        redirectToLogin();
      }
    },
  });

  if (cartQuery.isPending || productQueries.some((query) => query.isPending)) {
    return <CartLoading />;
  }

  if (cartQuery.isError) {
    return (
      <main className="mx-auto flex min-h-screen w-full max-w-[1280px] flex-grow items-center justify-center px-lg py-3xl md:px-xl">
        <section className="w-full max-w-lg rounded-lg border border-border-standard bg-surface-card p-2xl text-center">
          <h1 className="font-product-title text-product-title text-on-surface">We could not load your cart.</h1>
          <p className="mt-sm font-body-md text-body-md text-on-surface-variant">Please try again in a moment.</p>
          <button className="mt-xl h-control rounded-button bg-primary px-xl font-label-caps text-label-caps uppercase text-on-primary" type="button" onClick={() => cartQuery.refetch()}>Try again</button>
        </section>
      </main>
    );
  }

  const cart = cartQuery.data;
  const lines = cart.items.map((item, index) => ({ item, product: productQueries[index]?.data ?? null }));
  const hasUnavailableProduct = lines.some(({ product }) => product === null);
  const subtotal = lines.reduce((total, { item, product }) => total + (product?.currentPrice ?? 0) * item.quantity, 0);
  const isMutating = quantityMutation.isPending || removeMutation.isPending;
  const mutationError = quantityMutation.error ?? removeMutation.error;

  if (cart.items.length === 0) {
    return (
      <main className="mx-auto flex min-h-screen w-full max-w-[1280px] flex-grow items-center justify-center px-lg py-3xl md:px-xl">
        <section className="flex w-full max-w-lg flex-col items-center rounded-lg border border-dashed border-outline-variant bg-surface-container-lowest px-lg py-3xl text-center">
          <Icon name="shopping-bag" className="h-12 w-12 text-primary" />
          <h1 className="mt-lg font-headline-lg text-headline-lg-mobile text-on-background md:text-headline-lg">Your cart is empty</h1>
          <p className="mt-sm font-body-md text-body-md text-on-surface-variant">Browse the shop and add something you like.</p>
          <Link className="mt-xl flex h-control items-center rounded-button bg-primary px-xl font-label-caps text-label-caps uppercase text-on-primary" href="/shop">Continue shopping</Link>
        </section>
      </main>
    );
  }

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-[1280px] flex-grow flex-col gap-xl px-lg py-3xl md:px-xl">
      <h1 className="font-headline-lg text-headline-lg-mobile text-on-background md:text-headline-lg">Your Cart</h1>
      <div className="grid gap-xl lg:grid-cols-[minmax(0,1fr)_380px]">
        <div className="space-y-xl">
          {lines.map(({ item, product }) => (
            <article className="flex flex-col items-start gap-lg rounded-lg border border-border-standard bg-surface-card p-lg sm:flex-row sm:items-center" key={item.productId}>
              {product?.images[0] ? (
                <Image className="h-[120px] w-full rounded-lg border border-border-standard object-cover sm:w-[120px]" src={product.images[0].imageUrl} alt={product.images[0].altText || product.name} width={120} height={120} />
              ) : (
                <div className="flex h-[120px] w-full items-center justify-center rounded-lg border border-border-standard bg-surface-container-low font-label-caps text-label-caps uppercase text-on-surface-variant sm:w-[120px]">Unavailable</div>
              )}
              <div className="flex min-w-0 flex-grow flex-col">
                {product ? <Link className="font-product-title text-product-title text-on-surface hover:text-primary" href={`/shop/products/${product.id}`}>{product.name}</Link> : <h2 className="font-product-title text-product-title text-on-surface">Product unavailable</h2>}
                <p className="mt-xs font-body-md text-body-md text-on-surface-variant">{product ? formatCurrency(product.currentPrice) : 'Price unavailable'}</p>
              </div>
              <div className="flex w-full items-center justify-between gap-lg sm:w-auto sm:justify-start">
                <div className="flex h-control items-center rounded-button border border-outline-variant">
                  <button className="flex h-full items-center justify-center border-r border-outline-variant px-md text-on-surface-variant transition-colors hover:bg-surface-container-low disabled:cursor-not-allowed disabled:opacity-40" type="button" aria-label={`Decrease quantity for ${product?.name ?? 'unavailable product'}`} disabled={item.quantity <= 1 || isMutating} onClick={() => quantityMutation.mutate({ productId: item.productId, quantity: item.quantity - 1 })}><Icon name="remove" className="h-[18px] w-[18px]" /></button>
                  <span className="flex h-full w-[64px] items-center justify-center px-xl text-center font-body-md text-body-md text-on-surface" aria-live="polite">{item.quantity}</span>
                  <button className="flex h-full items-center justify-center border-l border-outline-variant px-md text-on-surface-variant transition-colors hover:bg-surface-container-low disabled:cursor-not-allowed disabled:opacity-40" type="button" aria-label={`Increase quantity for ${product?.name ?? 'unavailable product'}`} disabled={isMutating} onClick={() => quantityMutation.mutate({ productId: item.productId, quantity: item.quantity + 1 })}><Icon name="add" className="h-[18px] w-[18px]" /></button>
                </div>
                <button className="flex items-center justify-center rounded-button p-sm text-error transition-colors hover:bg-error-container disabled:cursor-not-allowed disabled:opacity-40" type="button" aria-label={`Remove ${product?.name ?? 'unavailable product'}`} disabled={isMutating} onClick={() => removeMutation.mutate(item.productId)}><Icon name="delete" className="h-5 w-5" /></button>
              </div>
            </article>
          ))}
          {hasUnavailableProduct ? <p className="font-status-pill text-status-pill text-status-error" role="alert">One or more products are no longer available. Remove them before checkout.</p> : null}
          {mutationError ? <p className="font-status-pill text-status-pill text-status-error" role="alert">{mutationError instanceof Error ? mutationError.message : 'We could not update your cart.'}</p> : null}
        </div>
        <aside className="w-full lg:w-[380px]">
          <div className="sticky top-[100px] rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm">
            <h2 className="mb-lg font-product-title text-product-title text-on-surface">Order Summary</h2>
            <div className="mb-xl space-y-md border-b border-border-standard pb-lg font-body-md text-body-md text-on-surface-variant">
              <div className="flex justify-between"><span>Subtotal</span><span>{formatCurrency(subtotal)}</span></div>
              <div className="flex justify-between"><span>Shipping</span><span>Calculated at checkout</span></div>
            </div>
            <div className="mb-xl flex items-center justify-between font-body-md text-body-md text-on-surface"><span className="font-medium">Estimated total</span><span className="font-semibold">{formatCurrency(subtotal)}</span></div>
            <div className="flex flex-col gap-md">
              <Link className={`flex h-control w-full items-center justify-center rounded-button bg-primary px-xl py-sm font-label-caps text-label-caps uppercase text-on-primary transition-colors hover:bg-primary-container ${hasUnavailableProduct ? 'pointer-events-none opacity-50' : ''}`} href={hasUnavailableProduct ? '#' : '/checkout'} aria-disabled={hasUnavailableProduct}>Proceed to checkout</Link>
              <Link className="flex h-control w-full items-center justify-center rounded-button border border-outline bg-surface-card px-xl py-sm font-label-caps text-label-caps uppercase text-on-surface transition-colors hover:bg-surface-container-low" href="/shop">Continue shopping</Link>
            </div>
          </div>
        </aside>
      </div>
    </main>
  );
}
