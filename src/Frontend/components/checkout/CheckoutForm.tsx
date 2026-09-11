'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import axios from 'axios';
import { submitCheckout, type Cart, type CheckoutRequest } from '../../lib/api/orders';
import type { ShippingMethod } from '../../lib/api/shipping';
import type { CatalogProduct } from '../../types/catalog';
import { checkoutSchema, type CheckoutFormValues } from '../../lib/validation/checkout';
import { formatCurrency } from '../../lib/formatters/currency';

type CheckoutFormProps = {
  cart: Cart;
  products: Record<string, CatalogProduct | null>;
  shippingMethods: ShippingMethod[];
  email: string;
};

function createIdempotencyKey() {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return globalThis.crypto.randomUUID();
  }

  if (typeof globalThis.crypto?.getRandomValues === 'function') {
    const bytes = new Uint8Array(16);
    globalThis.crypto.getRandomValues(bytes);
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    return Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('').replace(
      /^(.{8})(.{4})(.{4})(.{4})(.{12})$/,
      '$1-$2-$3-$4-$5',
    );
  }

  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (character) => {
    const randomValue = Math.random() * 16 | 0;
    const value = character === 'x' ? randomValue : (randomValue & 0x3) | 0x8;
    return value.toString(16);
  });
}

function Field({
  id,
  label,
  error,
  ...props
}: React.InputHTMLAttributes<HTMLInputElement> & { id: string; label: string; error?: string }) {
  return (
    <div className="flex flex-col gap-xs">
      <label className="font-label-caps text-label-caps uppercase tracking-wider text-on-surface" htmlFor={id}>{label}</label>
      <input className="h-control rounded border border-outline-variant bg-surface-card px-md font-body-md text-body-md text-on-surface outline-none transition-colors focus:border-primary" id={id} aria-invalid={error ? true : undefined} aria-describedby={error ? `${id}-error` : undefined} {...props} />
      {error ? <p className="font-status-pill text-status-pill text-status-error" id={`${id}-error`} role="alert">{error}</p> : null}
    </div>
  );
}

export default function CheckoutForm({ cart, products, shippingMethods, email }: CheckoutFormProps) {
  const { register, handleSubmit, control, formState: { errors, isSubmitting }, setError } = useForm<CheckoutFormValues>({
    resolver: zodResolver(checkoutSchema),
    defaultValues: { email, country: 'US', shippingMethodCode: shippingMethods[0]?.code ?? '', paymentMethod: '' },
    mode: 'onBlur',
    reValidateMode: 'onChange',
  });
  const router = useRouter();
  const [idempotencyKey] = useState(createIdempotencyKey);
  const selectedMethodCode = useWatch({ control, name: 'shippingMethodCode' });
  const selectedMethod = shippingMethods.find((method) => method.code === selectedMethodCode);
  const subtotal = cart.items.reduce((total, item) => total + (products[item.productId]?.currentPrice ?? 0) * item.quantity, 0);
  const shipping = selectedMethod?.cost ?? 0;
  const hasUnavailableProduct = cart.items.some((item) => !products[item.productId]);
  const checkoutMutation = useMutation({
    mutationFn: ({ request, idempotencyKey }: { request: CheckoutRequest; idempotencyKey: string }) => submitCheckout(request, idempotencyKey),
  });

  async function onSubmit(values: CheckoutFormValues) {
    try {
      const { state, email: _email, ...addressValues } = values;
      const response = await checkoutMutation.mutateAsync({
        request: { ...addressValues, stateOrProvince: state, currency: 'USD' },
        idempotencyKey,
      });
      router.replace(`/checkout/confirmation?orderId=${encodeURIComponent(response.orderId)}`);
    } catch (error) {
      const message = axios.isAxiosError<{ message?: string }>(error)
        ? error.response?.data?.message ?? 'We could not place your order.'
        : 'We could not place your order.';
      setError('root', { message });
    }
  }

  return (
    <form className="grid gap-2xl lg:grid-cols-[minmax(0,1fr)_380px]" onSubmit={handleSubmit(onSubmit)} noValidate>
      <div className="space-y-xl">
        <section className="rounded-lg border border-border-standard bg-surface-card p-xl">
          <h2 className="font-product-title text-product-title text-on-surface">Contact information</h2>
          <div className="mt-lg grid gap-lg sm:grid-cols-2">
            <Field id="email" label="Email address" type="email" autoComplete="email" error={errors.email?.message} {...register('email')} />
            <Field id="fullName" label="Full name" autoComplete="name" error={errors.fullName?.message} {...register('fullName')} />
          </div>
        </section>
        <section className="rounded-lg border border-border-standard bg-surface-card p-xl">
          <h2 className="font-product-title text-product-title text-on-surface">Payment</h2>
          <div className="mt-lg max-w-md"><Field id="paymentMethod" label="Payment method" placeholder="Test card" autoComplete="cc-number" error={errors.paymentMethod?.message} {...register('paymentMethod')} /></div>
        </section>
        <section className="rounded-lg border border-border-standard bg-surface-card p-xl">
          <h2 className="font-product-title text-product-title text-on-surface">Shipping address</h2>
          <div className="mt-lg grid gap-lg sm:grid-cols-2">
            <div className="sm:col-span-2"><Field id="addressLine1" label="Address" autoComplete="address-line1" error={errors.addressLine1?.message} {...register('addressLine1')} /></div>
            <div className="sm:col-span-2"><Field id="addressLine2" label="Apartment, suite, etc. (optional)" autoComplete="address-line2" {...register('addressLine2')} /></div>
            <Field id="city" label="City" autoComplete="address-level2" error={errors.city?.message} {...register('city')} />
            <Field id="state" label="State / province" autoComplete="address-level1" error={errors.state?.message} {...register('state')} />
            <Field id="postalCode" label="Postal code" autoComplete="postal-code" error={errors.postalCode?.message} {...register('postalCode')} />
            <Field id="country" label="Country code" placeholder="US" autoComplete="country" error={errors.country?.message} {...register('country')} />
          </div>
        </section>
        <section className="rounded-lg border border-border-standard bg-surface-card p-xl">
          <h2 className="font-product-title text-product-title text-on-surface">Shipping method</h2>
          <div className="mt-lg space-y-md">
            {shippingMethods.length === 0 ? <p className="font-body-md text-body-md text-on-surface-variant">Shipping methods are temporarily unavailable.</p> : shippingMethods.map((method) => (
              <label className="flex cursor-pointer items-start gap-md rounded border border-outline-variant p-md transition-colors has-[:checked]:border-primary has-[:checked]:bg-surface-container-low" key={method.id}>
                <input className="mt-1 h-4 w-4 accent-primary" type="radio" value={method.code} {...register('shippingMethodCode')} />
                <span className="flex flex-1 justify-between gap-md font-body-md text-body-md text-on-surface"><span><span className="block font-semibold">{method.name}</span><span className="text-on-surface-variant">Arrives in {method.estimatedDeliveryDays} days</span></span><span className="font-semibold">{formatCurrency(method.cost)}</span></span>
              </label>
            ))}
          </div>
          {errors.shippingMethodCode ? <p className="mt-sm font-status-pill text-status-pill text-status-error" role="alert">{errors.shippingMethodCode.message}</p> : null}
        </section>
      </div>
      <aside className="w-full lg:w-[380px]">
        <div className="sticky top-[100px] rounded-lg border border-border-standard bg-surface-card p-xl shadow-sm">
          <h2 className="mb-lg font-product-title text-product-title text-on-surface">Order summary</h2>
          <div className="space-y-lg border-b border-border-standard pb-lg">
            {cart.items.map((item) => {
              const product = products[item.productId];
              return <div className="flex gap-md" key={item.productId}>{product?.images[0] ? <Image className="h-16 w-16 rounded border border-border-standard object-cover" src={product.images[0].imageUrl} alt={product.images[0].altText || product.name} width={64} height={64} /> : null}<div className="min-w-0 flex-1 font-body-md text-body-md"><p className="truncate text-on-surface">{product?.name ?? 'Product unavailable'}</p><p className="text-on-surface-variant">Qty {item.quantity}</p></div><span className="font-body-md text-body-md text-on-surface">{product ? formatCurrency(product.currentPrice * item.quantity) : 'Unavailable'}</span></div>;
            })}
          </div>
          <div className="mt-lg space-y-md font-body-md text-body-md text-on-surface-variant"><div className="flex justify-between"><span>Subtotal</span><span>{formatCurrency(subtotal)}</span></div><div className="flex justify-between"><span>Shipping</span><span>{formatCurrency(shipping)}</span></div></div>
          <div className="mt-lg flex justify-between border-t border-border-standard pt-lg font-product-title text-product-title text-on-surface"><span>Total</span><span>{formatCurrency(subtotal + shipping)}</span></div>
          {errors.root ? <p className="mt-lg font-status-pill text-status-pill text-status-error" role="alert">{errors.root.message}</p> : null}
          {hasUnavailableProduct ? <p className="mt-lg font-status-pill text-status-pill text-status-error" role="alert">Remove unavailable products before checkout.</p> : null}
          <button className="mt-xl h-control w-full rounded-button bg-primary px-xl font-label-caps text-label-caps uppercase text-on-primary transition-colors hover:bg-primary-container disabled:cursor-not-allowed disabled:opacity-50" type="submit" disabled={isSubmitting || checkoutMutation.isPending || hasUnavailableProduct || shippingMethods.length === 0}>{checkoutMutation.isPending ? 'Placing order...' : 'Place order'}</button>
          <Link className="mt-md flex h-control w-full items-center justify-center rounded-button border border-outline bg-surface-card px-xl font-label-caps text-label-caps uppercase text-on-surface" href="/cart">Back to cart</Link>
        </div>
      </aside>
    </form>
  );
}
